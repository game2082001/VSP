using System.IO;
using VSP.Core.Logging;

namespace VSP.Player.Recording;

internal sealed class RecordingRetentionService
{
    private readonly Func<DateTime> _localClock;
    private readonly Action<string> _deleteFile;
    private readonly RecordingFileUsageTracker _usageTracker;

    public RecordingRetentionService()
        : this(static () => DateTime.Now, RecordingFileUsageTracker.Shared, static path => File.Delete(path))
    {
    }

    internal RecordingRetentionService(
        Func<DateTime> localClock,
        RecordingFileUsageTracker? usageTracker = null,
        Action<string>? deleteFile = null)
    {
        _localClock = localClock ?? throw new ArgumentNullException(nameof(localClock));
        _usageTracker = usageTracker ?? RecordingFileUsageTracker.Shared;
        _deleteFile = deleteFile ?? (static path => File.Delete(path));
    }

    public RecordingRetentionResult Run(string recordingRoot, int retentionDays)
    {
        return RunCore(recordingRoot, retentionDays, cameraId: null);
    }

    internal RecordingRetentionResult RunForCamera(string recordingRoot, Guid cameraId, int retentionDays)
    {
        return RunCore(recordingRoot, retentionDays, cameraId);
    }

    private RecordingRetentionResult RunCore(string recordingRoot, int retentionDays, Guid? cameraId)
    {
        if (string.IsNullOrWhiteSpace(recordingRoot) || retentionDays < 1 || !Directory.Exists(recordingRoot))
        {
            return RecordingRetentionResult.Empty;
        }

        var cutoff = _localClock().AddDays(-retentionDays);
        var scanned = 0;
        var deleted = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var file in EnumerateOwnedCandidates(recordingRoot, cameraId))
        {
            scanned++;

            if (file.RecordedAt >= cutoff)
            {
                skipped++;
                continue;
            }

            try
            {
                var deleteAttempt = _usageTracker.TryDeleteIfNotInUse(file.FilePath, _deleteFile);
                if (deleteAttempt == RecordingFileDeleteAttempt.SkippedInUse)
                {
                    skipped++;
                    continue;
                }

                deleted++;
            }
            catch (Exception ex)
            {
                failed++;
                AppLog.Warning(
                    $"Recording retention skipped file '{Path.GetFileName(file.FilePath)}' for camera {file.CameraId:N}: {ex.GetType().Name}.");
            }
        }

        return new RecordingRetentionResult(scanned, deleted, skipped, failed);
    }

    internal static IReadOnlyList<RecordingRetentionCandidate> ListOwnedCandidates(string recordingRoot) =>
        EnumerateOwnedCandidates(recordingRoot, cameraId: null).ToList();

    private static IEnumerable<RecordingRetentionCandidate> EnumerateOwnedCandidates(string recordingRoot, Guid? cameraId)
    {
        if (string.IsNullOrWhiteSpace(recordingRoot) || !Directory.Exists(recordingRoot))
        {
            yield break;
        }

        DirectoryInfo root;
        try
        {
            root = new DirectoryInfo(recordingRoot);
            if (RecordingRetentionOwnership.IsReparsePoint(root))
            {
                yield break;
            }
        }
        catch
        {
            yield break;
        }

        foreach (var cameraDirectory in EnumerateCameraDirectories(root, cameraId))
        {
            if (RecordingRetentionOwnership.IsReparsePoint(cameraDirectory) ||
                !Guid.TryParseExact(cameraDirectory.Name, "N", out _))
            {
                continue;
            }

            IEnumerable<FileInfo> files;
            try
            {
                files = cameraDirectory.EnumerateFiles("*", SearchOption.TopDirectoryOnly).ToList();
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                if (RecordingRetentionOwnership.IsReparsePoint(file))
                {
                    continue;
                }

                if (RecordingRetentionOwnership.TryClassify(recordingRoot, file.FullName, out var candidate))
                {
                    yield return candidate;
                }
            }
        }
    }

    private static IEnumerable<DirectoryInfo> EnumerateCameraDirectories(DirectoryInfo root, Guid? cameraId)
    {
        if (cameraId.HasValue)
        {
            var cameraDirectory = new DirectoryInfo(Path.Combine(root.FullName, cameraId.Value.ToString("N")));
            if (cameraDirectory.Exists)
            {
                yield return cameraDirectory;
            }

            yield break;
        }

        IEnumerable<DirectoryInfo> cameraDirectories;
        try
        {
            cameraDirectories = root.EnumerateDirectories("*", SearchOption.TopDirectoryOnly).ToList();
        }
        catch
        {
            yield break;
        }

        foreach (var cameraDirectory in cameraDirectories)
        {
            yield return cameraDirectory;
        }
    }
}
