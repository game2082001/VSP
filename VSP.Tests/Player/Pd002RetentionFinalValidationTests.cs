using VSP.Core.Logging;
using VSP.Player.Recording;
using VSP.Tests.Logging;
using Xunit;

namespace VSP.Tests.Player;

[Collection("AppLog")]
public sealed class Pd002RetentionFinalValidationTests : IDisposable
{
    private const string SentinelSecret = "PD002D-SENTINEL-camera-secret";
    private const string SentinelRtspUserInfo = "pd002d-user:pd002d-password@";

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"vsp-pd002d-retention-{Guid.NewGuid():N}");
    private readonly DateTime _now = new(2026, 8, 27, 12, 0, 0);

    [Fact]
    public void RetentionPolicy_FinalRegressionMatrix_ProtectsOwnedAndUnownedRecordingFiles()
    {
        var expired = CreateRecording(Guid.NewGuid(), "20260819_115959");
        var boundary = CreateRecording(Guid.NewGuid(), "20260820_120000");
        var fresh = CreateRecording(Guid.NewGuid(), "20260820_120001");
        var legacyFlatRoot = Path.Combine(_root, "20260819_090000_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.mp4");
        var invalidCameraDirectory = Path.Combine(_root, "not-a-camera");
        var malformed = Path.Combine(Path.GetDirectoryName(fresh)!, "clip.mp4");
        var nonMp4 = Path.Combine(Path.GetDirectoryName(fresh)!, "20260819_090000_bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.txt");
        Directory.CreateDirectory(invalidCameraDirectory);
        File.WriteAllBytes(legacyFlatRoot, []);
        File.WriteAllBytes(Path.Combine(invalidCameraDirectory, "20260819_090000_cccccccccccccccccccccccccccccccc.mp4"), []);
        File.WriteAllBytes(malformed, []);
        File.WriteAllBytes(nonMp4, []);
        var service = new RecordingRetentionService(() => _now);

        var result = service.Run(_root, retentionDays: 7);
        var secondResult = service.Run(_root, retentionDays: 7);

        Assert.Equal(3, result.CandidatesScanned);
        Assert.Equal(1, result.DeletedFiles);
        Assert.Equal(2, result.SkippedFiles);
        Assert.Equal(0, result.FailedFiles);
        Assert.False(File.Exists(expired));
        Assert.True(File.Exists(boundary));
        Assert.True(File.Exists(fresh));
        Assert.True(File.Exists(legacyFlatRoot));
        Assert.True(File.Exists(malformed));
        Assert.True(File.Exists(nonMp4));
        Assert.Equal(2, secondResult.CandidatesScanned);
        Assert.Equal(0, secondResult.DeletedFiles);
    }

    [Fact]
    public async Task RuntimeRetention_StressOverlappingTriggers_RemainsSingleFlightAndIdempotent()
    {
        var files = Enumerable.Range(0, 20)
            .Select(_ => CreateRecording(Guid.NewGuid(), "20260819_090000"))
            .ToArray();
        var deleteCalls = 0;
        var service = new RecordingRetentionService(
            () => _now,
            deleteFile: path =>
            {
                Interlocked.Increment(ref deleteCalls);
                File.Delete(path);
            });
        var coordinator = new RecordingRetentionCoordinator(
            () => new RecordingRetentionSettings(_root, 7),
            service,
            TimeSpan.FromDays(1),
            static work => Task.Run(work));

        var triggers = Enumerable.Range(0, 40)
            .Select(index => index % 3 == 0
                ? coordinator.RunRecordingCompletedAsync(Guid.NewGuid())
                : index % 3 == 1
                    ? coordinator.RunStartupAsync()
                    : coordinator.RunPeriodicAsync())
            .ToArray();

        await Task.WhenAll(triggers);
        await coordinator.RunStartupAsync();

        Assert.All(files, file => Assert.False(File.Exists(file)));
        Assert.Equal(files.Length, deleteCalls);
    }

    [Fact]
    public async Task RuntimeRetention_ActiveRecordingAndPlaybackRemainProtectedUntilUsageReleased()
    {
        var activeRecording = CreateRecording(Guid.NewGuid(), "20260819_090000");
        var activePlayback = CreateRecording(Guid.NewGuid(), "20260819_100000");
        var usageTracker = new RecordingFileUsageTracker();
        var service = new RecordingRetentionService(() => _now, usageTracker);
        var coordinator = new RecordingRetentionCoordinator(
            () => new RecordingRetentionSettings(_root, 7),
            service,
            TimeSpan.FromDays(1),
            static work => work());

        using (usageTracker.Register(activeRecording))
        using (usageTracker.Register(activePlayback))
        {
            await coordinator.RunStartupAsync();
        }

        Assert.True(File.Exists(activeRecording));
        Assert.True(File.Exists(activePlayback));

        await coordinator.RunStartupAsync();

        Assert.False(File.Exists(activeRecording));
        Assert.False(File.Exists(activePlayback));
    }

    [Fact]
    public async Task RuntimeRetention_MissingRootAndFailure_DoNotEscapeTrigger()
    {
        var missingRoot = Path.Combine(_root, "offline-root");
        var settingsLoads = 0;
        var coordinator = new RecordingRetentionCoordinator(
            () =>
            {
                settingsLoads++;
                return settingsLoads == 1
                    ? new RecordingRetentionSettings(missingRoot, 7)
                    : throw new UnauthorizedAccessException("offline volume");
            },
            new RecordingRetentionService(() => _now),
            TimeSpan.FromDays(1),
            static work => work());

        await coordinator.RunStartupAsync();
        await coordinator.RunPeriodicAsync();

        Assert.False(Directory.Exists(missingRoot));
    }

    [Fact]
    public void RetentionLogging_DoesNotExposeCredentialsRtspUserInfoOrFullPaths()
    {
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        var cameraId = Guid.NewGuid();
        var failingFile = CreateRecording(cameraId, "20260819_090000");
        var service = new RecordingRetentionService(
            () => _now,
            deleteFile: _ => throw new IOException($"simulated failure {SentinelSecret} {SentinelRtspUserInfo}"));

        var result = service.Run(_root, retentionDays: 7);

        Assert.Equal(1, result.FailedFiles);
        Assert.All(recorder.Calls, call =>
        {
            Assert.DoesNotContain(SentinelSecret, call.Message);
            Assert.DoesNotContain(SentinelRtspUserInfo, call.Message);
            Assert.DoesNotContain(_root, call.Message);
            Assert.DoesNotContain(SentinelSecret, call.Exception?.ToString() ?? string.Empty);
            Assert.DoesNotContain(SentinelRtspUserInfo, call.Exception?.ToString() ?? string.Empty);
        });
        Assert.True(File.Exists(failingFile));
    }

    [Fact]
    public void WindowsReadOnlyFileFailure_ContinuesWithoutDeletingOtherExpiredFiles()
    {
        var readOnlyFile = CreateRecording(Guid.NewGuid(), "20260819_090000");
        var normalFile = CreateRecording(Guid.NewGuid(), "20260819_100000");
        File.SetAttributes(readOnlyFile, File.GetAttributes(readOnlyFile) | FileAttributes.ReadOnly);
        var service = new RecordingRetentionService(() => _now);

        try
        {
            var result = service.Run(_root, retentionDays: 7);

            Assert.Equal(2, result.CandidatesScanned);
            Assert.Equal(1, result.DeletedFiles);
            Assert.Equal(1, result.FailedFiles);
            Assert.True(File.Exists(readOnlyFile));
            Assert.False(File.Exists(normalFile));
        }
        finally
        {
            if (File.Exists(readOnlyFile))
            {
                File.SetAttributes(readOnlyFile, File.GetAttributes(readOnlyFile) & ~FileAttributes.ReadOnly);
            }
        }
    }

    [Fact]
    public void ReparsePointDirectory_IsNotFollowedOrDeletedWhenPlatformAllowsCreation()
    {
        Directory.CreateDirectory(_root);
        var targetDirectory = Path.Combine(Path.GetTempPath(), $"vsp-pd002d-reparse-target-{Guid.NewGuid():N}");
        var linkDirectory = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(targetDirectory);
        var targetFile = Path.Combine(targetDirectory, "20260819_090000_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.mp4");
        File.WriteAllBytes(targetFile, []);

        try
        {
            Directory.CreateSymbolicLink(linkDirectory, targetDirectory);
        }
        catch (Exception) when (OperatingSystem.IsWindows())
        {
            Directory.Delete(targetDirectory, recursive: true);
            return;
        }

        try
        {
            var result = new RecordingRetentionService(() => _now).Run(_root, retentionDays: 7);

            Assert.Equal(0, result.CandidatesScanned);
            Assert.True(File.Exists(targetFile));
        }
        finally
        {
            if (Directory.Exists(targetDirectory))
            {
                Directory.Delete(targetDirectory, recursive: true);
            }
        }
    }

    private string CreateRecording(Guid cameraId, string timestamp)
    {
        var cameraDirectory = Path.Combine(_root, cameraId.ToString("N"));
        Directory.CreateDirectory(cameraDirectory);

        var file = Path.Combine(cameraDirectory, $"{timestamp}_{Guid.NewGuid():N}.mp4");
        File.WriteAllBytes(file, []);
        return file;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
                {
                    File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
                }

                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
