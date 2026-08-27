using VSP.Player.Recording;
using Xunit;

namespace VSP.Tests.Player;

public class RecordingRetentionCoordinatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"vsp-retention-runtime-test-{Guid.NewGuid():N}");
    private readonly DateTime _now = new(2026, 8, 27, 12, 0, 0);

    [Fact]
    public async Task StartupTrigger_UsesPersistedRetentionDaysForRuntimeCutoff()
    {
        var expired = CreateRecording(Guid.NewGuid(), "20260819_115959");
        var boundary = CreateRecording(Guid.NewGuid(), "20260820_120000");
        var coordinator = CreateCoordinator(retentionDays: 7);

        await coordinator.RunStartupAsync();

        Assert.False(File.Exists(expired));
        Assert.True(File.Exists(boundary));
    }

    [Fact]
    public async Task RecordingCompletionTrigger_RunsAfterFinalizedCameraAndTargetsThatCamera()
    {
        var completedCamera = Guid.NewGuid();
        var otherCamera = Guid.NewGuid();
        var completedCameraFile = CreateRecording(completedCamera, "20260819_090000");
        var otherCameraFile = CreateRecording(otherCamera, "20260819_090000");
        var coordinator = CreateCoordinator(retentionDays: 7);

        await coordinator.RunRecordingCompletedAsync(completedCamera);

        Assert.False(File.Exists(completedCameraFile));
        Assert.True(File.Exists(otherCameraFile));
    }

    [Fact]
    public async Task PeriodicTrigger_DeletesExpiredRecordings()
    {
        var expired = CreateRecording(Guid.NewGuid(), "20260819_090000");
        var coordinator = CreateCoordinator(retentionDays: 7);

        await coordinator.RunPeriodicAsync();

        Assert.False(File.Exists(expired));
    }

    [Fact]
    public async Task Start_SchedulesStartupAndPeriodicCleanupUntilDisposed()
    {
        var startupFile = CreateRecording(Guid.NewGuid(), "20260819_090000");
        var coordinator = new RecordingRetentionCoordinator(
            () => new RecordingRetentionSettings(_root, 7),
            new RecordingRetentionService(() => _now),
            TimeSpan.FromMilliseconds(25));

        coordinator.Start();
        Assert.True(await WaitUntilDeletedAsync(startupFile));

        var periodicFile = CreateRecording(Guid.NewGuid(), "20260819_100000");
        Assert.True(await WaitUntilDeletedAsync(periodicFile));

        coordinator.Dispose();
        var afterDisposeFile = CreateRecording(Guid.NewGuid(), "20260819_110000");
        await Task.Delay(75);

        Assert.True(File.Exists(afterDisposeFile));
    }

    [Fact]
    public async Task OverlappingTriggers_AreSingleFlight()
    {
        var file = CreateRecording(Guid.NewGuid(), "20260819_090000");
        using var deleteEntered = new ManualResetEventSlim();
        using var allowDelete = new ManualResetEventSlim();
        var service = new RecordingRetentionService(
            () => _now,
            deleteFile: path =>
            {
                deleteEntered.Set();
                Assert.True(allowDelete.Wait(TimeSpan.FromSeconds(5)));
                File.Delete(path);
            });
        var coordinator = CreateCoordinator(service, retentionDays: 7);

        var first = coordinator.RunStartupAsync();
        Assert.True(deleteEntered.Wait(TimeSpan.FromSeconds(5)));

        var second = coordinator.RunPeriodicAsync();
        allowDelete.Set();

        await Task.WhenAll(first, second);

        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task ActiveRecordingOrPlaybackUsage_IsStillProtectedAfterRuntimeWiring()
    {
        var file = CreateRecording(Guid.NewGuid(), "20260819_090000");
        var usageTracker = new RecordingFileUsageTracker();
        var service = new RecordingRetentionService(() => _now, usageTracker);
        var coordinator = CreateCoordinator(service, retentionDays: 7);

        using (usageTracker.Register(file))
        {
            await coordinator.RunStartupAsync();
        }

        Assert.True(File.Exists(file));

        await coordinator.RunStartupAsync();

        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task MissingRoot_IsSafeNoOpAndDoesNotCreateDirectory()
    {
        var missingRoot = Path.Combine(_root, "missing");
        var coordinator = new RecordingRetentionCoordinator(
            () => new RecordingRetentionSettings(missingRoot, 7),
            new RecordingRetentionService(() => _now),
            TimeSpan.FromDays(1),
            static work => work());

        await coordinator.RunStartupAsync();

        Assert.False(Directory.Exists(missingRoot));
    }

    [Fact]
    public async Task RetentionFailure_IsIsolatedFromTrigger()
    {
        var coordinator = new RecordingRetentionCoordinator(
            () => throw new InvalidOperationException("settings unavailable"),
            new RecordingRetentionService(() => _now),
            TimeSpan.FromDays(1),
            static work => work());

        await coordinator.RunStartupAsync();
    }

    [Fact]
    public async Task DisposedCoordinator_DoesNotRunFutureCleanup()
    {
        var expired = CreateRecording(Guid.NewGuid(), "20260819_090000");
        var coordinator = CreateCoordinator(retentionDays: 7);

        coordinator.Dispose();
        await coordinator.RunPeriodicAsync();

        Assert.True(File.Exists(expired));
    }

    private RecordingRetentionCoordinator CreateCoordinator(int retentionDays) =>
        CreateCoordinator(new RecordingRetentionService(() => _now), retentionDays);

    private RecordingRetentionCoordinator CreateCoordinator(RecordingRetentionService service, int retentionDays) =>
        new(
            () => new RecordingRetentionSettings(_root, retentionDays),
            service,
            TimeSpan.FromDays(1),
            static work => Task.Run(work));

    private string CreateRecording(Guid cameraId, string timestamp)
    {
        var cameraDirectory = Path.Combine(_root, cameraId.ToString("N"));
        Directory.CreateDirectory(cameraDirectory);

        var file = Path.Combine(cameraDirectory, $"{timestamp}_{Guid.NewGuid():N}.mp4");
        File.WriteAllBytes(file, []);
        return file;
    }

    private static async Task<bool> WaitUntilDeletedAsync(string path)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (!File.Exists(path))
            {
                return true;
            }

            await Task.Delay(10);
        }

        return false;
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
