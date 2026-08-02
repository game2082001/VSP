using System.IO;

namespace VSP.Player.Recording;

/// <summary>One recorded file, as found on disk -- not a database row (no catalog/index exists).</summary>
public sealed record RecordingFileInfo(string FilePath, DateTime RecordedAt);

/// <summary>
/// Lists a camera's recordings for Playback (Epic-012). Deliberately the smallest possible
/// seam, matching <see cref="RecordingPathProvider"/>'s own philosophy: a direct filesystem
/// listing under the camera's recording subfolder, newest first -- no database, no in-memory
/// index. Search/retention/storage management remain separate, not-yet-built capabilities.
/// </summary>
public static class RecordingCatalog
{
    private const string RecordingFileExtension = "*.mp4";
    private const string TimestampPrefixFormat = "yyyyMMdd_HHmmss";

    public static IReadOnlyList<RecordingFileInfo> ListRecordings(Guid cameraId)
        => ListRecordings(RecordingPathProvider.GetCameraRecordingDirectory(cameraId));

    /// <summary>Test seam: lists an arbitrary directory instead of resolving one from a camera id.</summary>
    internal static IReadOnlyList<RecordingFileInfo> ListRecordings(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory.GetFiles(directory, RecordingFileExtension)
            .Select(path => new RecordingFileInfo(path, ParseRecordedAt(path)))
            .OrderByDescending(recording => recording.RecordedAt)
            .ToList();
    }

    private static DateTime ParseRecordedAt(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        var prefix = name.Length >= TimestampPrefixFormat.Length ? name[..TimestampPrefixFormat.Length] : name;

        return DateTime.TryParseExact(prefix, TimestampPrefixFormat, null, System.Globalization.DateTimeStyles.None, out var parsed)
            ? parsed
            : File.GetLastWriteTime(filePath);
    }
}
