using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace VSP.Player.Recording;

internal static partial class RecordingRetentionOwnership
{
    private const string RecordingExtension = ".mp4";
    private const string TimestampFormat = "yyyyMMdd_HHmmss";

    public static bool TryClassify(string recordingRoot, string filePath, out RecordingRetentionCandidate candidate)
    {
        candidate = default!;

        if (string.IsNullOrWhiteSpace(recordingRoot) || string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        string normalizedRoot;
        string normalizedFile;
        try
        {
            normalizedRoot = NormalizeDirectoryRoot(recordingRoot);
            normalizedFile = Path.GetFullPath(filePath);
        }
        catch
        {
            return false;
        }

        if (!IsUnderRoot(normalizedRoot, normalizedFile))
        {
            return false;
        }

        if (!string.Equals(Path.GetExtension(normalizedFile), RecordingExtension, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var cameraDirectory = Path.GetDirectoryName(normalizedFile);
        if (string.IsNullOrWhiteSpace(cameraDirectory))
        {
            return false;
        }

        var relativeCameraDirectory = Path.GetRelativePath(normalizedRoot, cameraDirectory);
        if (relativeCameraDirectory.Contains(Path.DirectorySeparatorChar) ||
            relativeCameraDirectory.Contains(Path.AltDirectorySeparatorChar) ||
            relativeCameraDirectory == "." ||
            !Guid.TryParseExact(relativeCameraDirectory, "N", out var cameraId))
        {
            return false;
        }

        var name = Path.GetFileName(normalizedFile);
        var match = VspRecordingFileName().Match(name);
        if (!match.Success)
        {
            return false;
        }

        if (!DateTime.TryParseExact(
                match.Groups["timestamp"].Value,
                TimestampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var recordedAt))
        {
            return false;
        }

        candidate = new RecordingRetentionCandidate(normalizedFile, cameraId, recordedAt);
        return true;
    }

    public static bool IsReparsePoint(FileSystemInfo info)
    {
        try
        {
            return info.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch
        {
            return true;
        }
    }

    private static string NormalizeDirectoryRoot(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.EndsWith(Path.DirectorySeparatorChar) || fullPath.EndsWith(Path.AltDirectorySeparatorChar)
            ? fullPath
            : fullPath + Path.DirectorySeparatorChar;
    }

    private static bool IsUnderRoot(string normalizedRoot, string normalizedFile) =>
        normalizedFile.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"^(?<timestamp>\d{8}_\d{6})_[0-9a-fA-F]{32}\.mp4$", RegexOptions.CultureInvariant)]
    private static partial Regex VspRecordingFileName();
}
