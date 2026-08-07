using System.IO;
using VSP.Core.Logging;
using VSP.Infrastructure.Settings;

namespace VSP.UI.Validation;

/// <summary>Pure validators for the Settings screen, matching <see cref="CameraValidator"/>'s shape.</summary>
public static class SettingsValidator
{
    public static bool IsValidRecordingPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            _ = Path.GetFullPath(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsValidRetentionDays(int days)
    {
        return days >= AppSettingsLimits.MinRetentionDays && days <= AppSettingsLimits.MaxRetentionDays;
    }

    /// <summary>
    /// Writes a uniquely-named probe file into <paramref name="path"/> to determine whether it
    /// is writable, then unconditionally attempts to remove it in a <c>finally</c> block --
    /// never leaves a permanent file behind. The write-access verdict is fixed before cleanup
    /// runs, so a cleanup failure can never change it. If cleanup itself fails, that failure is
    /// caught, logged once at <see cref="AppLog.Warning(string, Exception?)"/>, and swallowed --
    /// never rethrown, never escalated, never leaves the application in a failed state (Epic-016
    /// Approval Record). The verdict itself is not logged here -- the caller decides what a
    /// <c>false</c> result means.
    /// </summary>
    public static bool HasWriteAccess(string path)
    {
        var probeFilePath = Path.Combine(path, $".vsp-write-test-{Guid.NewGuid():N}.tmp");
        bool canWrite;

        try
        {
            File.WriteAllText(probeFilePath, string.Empty);
            canWrite = true;
        }
        catch
        {
            canWrite = false;
        }
        finally
        {
            try
            {
                if (File.Exists(probeFilePath))
                {
                    File.Delete(probeFilePath);
                }
            }
            catch (Exception ex)
            {
                AppLog.Warning($"Could not remove the write-access probe file '{probeFilePath}'.", ex);
            }
        }

        return canWrite;
    }
}
