using System.IO;
using VSP.Core.Configuration;

namespace VSP.Player.Recording;

/// <summary>
/// Resolves the RecordingRoot from a single config-file-backed setting so it is never
/// hardcoded into <c>MediaController</c> -- reads <c>recording-settings.json</c> under
/// <c>%LocalAppData%\VSP</c> if present, otherwise falls back to a fixed default. No Settings
/// UI, no folder browsing, no SQLite: intentionally the smallest seam that avoids hardcoding
/// the root for this and future Epics, per the approved Epic-011 scope.
///
/// File I/O for <c>recording-settings.json</c> is delegated to <see cref="SettingsFileStore"/>
/// (<c>VSP.Core.Configuration</c>), shared with <c>VSP.Infrastructure.Settings.AppSettingsProvider</c>'s
/// Settings UI so the file has exactly one reader/writer implementation, per Epic-016. This
/// class's public API and every observable behavior -- default resolution, blank/malformed-value
/// fallback, directory creation -- are unchanged; only this internal implementation detail
/// changed (Epic-016 Approval Record).
/// </summary>
internal static class RecordingPathProvider
{
    private static readonly string DefaultConfigDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VSP");

    public static string GetRecordingRoot() => GetRecordingRoot(DefaultConfigDirectory);

    /// <summary>Test seam: resolves against an arbitrary config directory instead of %LocalAppData%\VSP.</summary>
    internal static string GetRecordingRoot(string configDirectory)
    {
        var defaultRoot = RecordingRootDefaults.Compute(configDirectory);

        var root = ReadConfiguredRoot(configDirectory) ?? defaultRoot;
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

    private static string? ReadConfiguredRoot(string configDirectory)
    {
        try
        {
            var configuredRoot = new SettingsFileStore(configDirectory).Load().RecordingRoot;
            return string.IsNullOrWhiteSpace(configuredRoot) ? null : configuredRoot;
        }
        catch (Exception)
        {
            // A malformed or unreadable config falls back to the default root rather than
            // failing recording outright -- the same best-effort convention already used by
            // MediaController's shutdown/cleanup paths. SettingsFileStore.Load() already
            // catches and logs malformed/unreadable files itself; this catch is a defensive
            // backstop only, preserving this method's original never-throws contract exactly.
            return null;
        }
    }
}
