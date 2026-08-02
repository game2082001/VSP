using System.IO;
using System.Text.Json;

namespace VSP.Player.Recording;

/// <summary>
/// Resolves the RecordingRoot from a single config-file-backed setting so it is never
/// hardcoded into <c>MediaController</c> -- reads <c>recording-settings.json</c> under
/// <c>%LocalAppData%\VSP</c> if present, otherwise falls back to a fixed default. No Settings
/// UI, no folder browsing, no SQLite: intentionally the smallest seam that avoids hardcoding
/// the root for this and future Epics, per the approved Epic-011 scope.
/// </summary>
internal static class RecordingPathProvider
{
    private const string ConfigFileName = "recording-settings.json";
    private const string RecordingsFolderName = "Recordings";

    private static readonly string DefaultConfigDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VSP");

    public static string GetRecordingRoot() => GetRecordingRoot(DefaultConfigDirectory);

    /// <summary>Test seam: resolves against an arbitrary config directory instead of %LocalAppData%\VSP.</summary>
    internal static string GetRecordingRoot(string configDirectory)
    {
        var defaultRoot = Path.Combine(configDirectory, RecordingsFolderName);
        var configFilePath = Path.Combine(configDirectory, ConfigFileName);

        var root = ReadConfiguredRoot(configFilePath) ?? defaultRoot;
        Directory.CreateDirectory(root);
        return root;
    }

    /// <summary>
    /// Recordings are organized per camera (Epic-012) so Playback can list "this camera's
    /// recordings" -- a subfolder of the recording root named by the camera's id.
    /// </summary>
    public static string GetCameraRecordingDirectory(Guid cameraId) => GetCameraRecordingDirectory(DefaultConfigDirectory, cameraId);

    /// <summary>Test seam: resolves against an arbitrary config directory instead of %LocalAppData%\VSP.</summary>
    internal static string GetCameraRecordingDirectory(string configDirectory, Guid cameraId)
    {
        var root = GetRecordingRoot(configDirectory);
        var cameraDirectory = Path.Combine(root, cameraId.ToString("N"));
        Directory.CreateDirectory(cameraDirectory);
        return cameraDirectory;
    }

    private static string? ReadConfiguredRoot(string configFilePath)
    {
        try
        {
            if (!File.Exists(configFilePath))
            {
                return null;
            }

            var json = File.ReadAllText(configFilePath);
            var settings = JsonSerializer.Deserialize<RecordingSettingsFile>(json);
            return string.IsNullOrWhiteSpace(settings?.RecordingRoot) ? null : settings.RecordingRoot;
        }
        catch (Exception)
        {
            // A malformed or unreadable config falls back to the default root rather than
            // failing recording outright -- the same best-effort convention already used by
            // MediaController's shutdown/cleanup paths.
            return null;
        }
    }

    private sealed class RecordingSettingsFile
    {
        public string? RecordingRoot { get; set; }
    }
}
