using VSP.Player.Recording;
using Xunit;

namespace VSP.Tests.Player;

public class RecordingRetentionServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"vsp-retention-test-{Guid.NewGuid():N}");
    private readonly DateTime _now = new(2026, 8, 27, 12, 0, 0);

    [Fact]
    public void Run_ExpiredValidVspRecording_DeletesFile()
    {
        var file = CreateRecording(Guid.NewGuid(), "20260820_115959");
        var service = CreateService();

        var result = service.Run(_root, retentionDays: 7);

        Assert.Equal(1, result.CandidatesScanned);
        Assert.Equal(1, result.DeletedFiles);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void Run_NonExpiredRecording_KeepsFile()
    {
        var file = CreateRecording(Guid.NewGuid(), "20260821_120001");
        var service = CreateService();

        var result = service.Run(_root, retentionDays: 7);

        Assert.Equal(1, result.CandidatesScanned);
        Assert.Equal(0, result.DeletedFiles);
        Assert.Equal(1, result.SkippedFiles);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void Run_ExactCutoffBoundary_KeepsFile()
    {
        var file = CreateRecording(Guid.NewGuid(), "20260820_120000");
        var service = CreateService();

        var result = service.Run(_root, retentionDays: 7);

        Assert.Equal(1, result.CandidatesScanned);
        Assert.Equal(0, result.DeletedFiles);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void Run_MultipleCameraGuidDirectories_DeletesOnlyExpiredCandidates()
    {
        var cameraA = Guid.NewGuid();
        var cameraB = Guid.NewGuid();
        var expiredA = CreateRecording(cameraA, "20260819_090000");
        var freshB = CreateRecording(cameraB, "20260827_090000");
        var service = CreateService();

        var result = service.Run(_root, retentionDays: 7);

        Assert.Equal(2, result.CandidatesScanned);
        Assert.Equal(1, result.DeletedFiles);
        Assert.False(File.Exists(expiredA));
        Assert.True(File.Exists(freshB));
    }

    [Fact]
    public void ListOwnedCandidates_InvalidCameraDirectory_IsIgnored()
    {
        var invalidDirectory = Path.Combine(_root, "not-a-camera-id");
        Directory.CreateDirectory(invalidDirectory);
        File.WriteAllBytes(Path.Combine(invalidDirectory, "20260819_090000_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.mp4"), []);

        var candidates = RecordingRetentionService.ListOwnedCandidates(_root);

        Assert.Empty(candidates);
    }

    [Fact]
    public void ListOwnedCandidates_UnrelatedMp4NonMp4AndMalformedNames_AreIgnored()
    {
        var cameraDirectory = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(cameraDirectory);
        File.WriteAllBytes(Path.Combine(cameraDirectory, "clip.mp4"), []);
        File.WriteAllBytes(Path.Combine(cameraDirectory, "20260819_090000_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.txt"), []);
        File.WriteAllBytes(Path.Combine(cameraDirectory, "20260819_090000_not-a-guid.mp4"), []);

        var candidates = RecordingRetentionService.ListOwnedCandidates(_root);

        Assert.Empty(candidates);
    }

    [Fact]
    public void ListOwnedCandidates_LegacyFlatRootRecording_IsIgnored()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllBytes(Path.Combine(_root, "20260819_090000_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.mp4"), []);

        var candidates = RecordingRetentionService.ListOwnedCandidates(_root);

        Assert.Empty(candidates);
    }

    [Fact]
    public void TryClassify_PathOutsideRoot_IsRejected()
    {
        var outsideRoot = Path.Combine(Path.GetTempPath(), $"vsp-retention-outside-{Guid.NewGuid():N}");
        var outsideFile = Path.Combine(outsideRoot, Guid.NewGuid().ToString("N"), "20260819_090000_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(outsideFile)!);
        File.WriteAllBytes(outsideFile, []);

        try
        {
            Assert.False(RecordingRetentionOwnership.TryClassify(_root, outsideFile, out _));
        }
        finally
        {
            Directory.Delete(outsideRoot, recursive: true);
        }
    }

    [Fact]
    public void Run_MissingRoot_IsSafeNoOpAndDoesNotCreateDirectory()
    {
        var service = CreateService();

        var result = service.Run(_root, retentionDays: 7);

        Assert.Equal(RecordingRetentionResult.Empty, result);
        Assert.False(Directory.Exists(_root));
    }

    [Fact]
    public void Run_DeletionFailure_ContinuesWithOtherFiles()
    {
        var failingFile = CreateRecording(Guid.NewGuid(), "20260818_090000");
        var deletedFile = CreateRecording(Guid.NewGuid(), "20260818_100000");
        var service = CreateService(path =>
        {
            if (path == failingFile)
            {
                throw new IOException("simulated locked file");
            }

            File.Delete(path);
        });

        var result = service.Run(_root, retentionDays: 7);

        Assert.Equal(2, result.CandidatesScanned);
        Assert.Equal(1, result.DeletedFiles);
        Assert.Equal(1, result.FailedFiles);
        Assert.True(File.Exists(failingFile));
        Assert.False(File.Exists(deletedFile));
    }

    [Fact]
    public void Run_RepeatedPass_IsIdempotent()
    {
        CreateRecording(Guid.NewGuid(), "20260818_090000");
        var service = CreateService();

        var first = service.Run(_root, retentionDays: 7);
        var second = service.Run(_root, retentionDays: 7);

        Assert.Equal(1, first.DeletedFiles);
        Assert.Equal(0, second.CandidatesScanned);
        Assert.Equal(0, second.DeletedFiles);
    }

    [Fact]
    public void Run_LocalTimeCutoff_UsesInjectedLocalClock()
    {
        var expired = CreateRecording(Guid.NewGuid(), "20260309_013000");
        var retained = CreateRecording(Guid.NewGuid(), "20260309_023000");
        var service = new RecordingRetentionService(() => new DateTime(2026, 3, 10, 2, 0, 0));

        var result = service.Run(_root, retentionDays: 1);

        Assert.Equal(2, result.CandidatesScanned);
        Assert.Equal(1, result.DeletedFiles);
        Assert.False(File.Exists(expired));
        Assert.True(File.Exists(retained));
    }

    [Fact]
    public void ListOwnedCandidates_ReparsePointDirectory_IsIgnoredWhenPlatformAllowsCreation()
    {
        Directory.CreateDirectory(_root);
        var targetDirectory = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        var linkDirectory = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(targetDirectory);
        File.WriteAllBytes(Path.Combine(targetDirectory, "20260819_090000_aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.mp4"), []);

        try
        {
            Directory.CreateSymbolicLink(linkDirectory, targetDirectory);
        }
        catch (Exception) when (OperatingSystem.IsWindows())
        {
            return;
        }

        var candidates = RecordingRetentionService.ListOwnedCandidates(_root);

        Assert.Single(candidates);
        Assert.Equal(targetDirectory, Path.GetDirectoryName(candidates[0].FilePath));
    }

    private RecordingRetentionService CreateService(Action<string>? deleteFile = null) =>
        new(() => _now, deleteFile);

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
                Directory.Delete(_root, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
