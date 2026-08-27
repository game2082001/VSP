using System.IO;
using VSP.Core.Logging;

namespace VSP.Player.Recording;

internal sealed class RecordingRetentionService
{
    private readonly Func<DateTime> _localClock;
    private readonly Action<string> _deleteFile;

    public RecordingRetentionService()
        : this(static () => DateTime.Now, static path => File.Delete(path))
    {
    }

    internal RecordingRetentionService(Func<DateTime> localClock, Action<string>? deleteFile = null)
    {
        _localClock = localClock ?? throw new ArgumentNullException(nameof(localClock));
        _deleteFile = deleteFile ?? (static path => File.Delete(path));
    }

    public RecordingRetentionResult Run(string recordingRoot, int retentionDays)
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

        foreach (var file in EnumerateOwnedCandidates(recordingRoot))
        {
            scanned++;

            if (file.RecordedAt >= cutoff)
            {
                skipped++;
                continue;
            }

            try
            {
                _deleteFile(file.FilePath);
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
        EnumerateOwnedCandidates(recordingRoot).ToList();

    private static IEnumerable<RecordingRetentionCandidate> EnumerateOwnedCandidates(string recordingRoot)
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
}
