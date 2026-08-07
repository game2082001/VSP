using System.Text.Json;
using VSP.Core.Logging;

namespace VSP.Core.Configuration;

/// <summary>
/// The one reader/writer for <c>recording-settings.json</c> under <c>%LocalAppData%\VSP</c> --
/// shared by <c>RecordingPathProvider</c> (<c>VSP.Player</c>) and <c>AppSettingsProvider</c>
/// (<c>VSP.Infrastructure</c>) so neither duplicates JSON parsing or file I/O, per Epic-016.
/// Domain-free: knows nothing about recording defaults, retention bounds, or the
/// <c>AppTheme</c>/<c>AppLanguage</c> enums -- those stay owned by its two callers.
/// </summary>
public sealed class SettingsFileStore
{
    private const string ConfigFileName = "recording-settings.json";

    private static readonly string DefaultConfigDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VSP");

    private readonly string _configDirectory;

    public SettingsFileStore() : this(DefaultConfigDirectory)
    {
    }

    /// <summary>
    /// Resolves against an arbitrary config directory instead of %LocalAppData%\VSP. Public,
    /// not a test-only seam: <c>RecordingPathProvider</c> and <c>AppSettingsProvider</c> both
    /// forward their own directory (their own production default, or their own test seam's
    /// directory) through to this constructor -- that forwarding is this type's normal contract,
    /// not an internal implementation detail.
    /// </summary>
    public SettingsFileStore(string configDirectory)
    {
        _configDirectory = configDirectory;
    }

    /// <summary>
    /// Never throws. A missing file returns an all-null <see cref="SettingsFileContents"/>
    /// (silently, not logged -- absence is the normal pre-Epic-016 state). Unparseable JSON is
    /// logged once at <see cref="LogLevel.Warning"/> and also falls back to an all-null result.
    /// </summary>
    public SettingsFileContents Load()
    {
        var configFilePath = Path.Combine(_configDirectory, ConfigFileName);

        if (!File.Exists(configFilePath))
        {
            return new SettingsFileContents();
        }

        try
        {
            var json = File.ReadAllText(configFilePath);
            return JsonSerializer.Deserialize<SettingsFileContents>(json) ?? new SettingsFileContents();
        }
        catch (Exception ex)
        {
            AppLog.Warning($"Could not read settings file '{configFilePath}' -- using defaults.", ex);
            return new SettingsFileContents();
        }
    }

    /// <summary>
    /// Writes atomically: serializes to a uniquely-named temp file in the same directory, then
    /// replaces the real file with a single same-volume <see cref="File.Move(string, string, bool)"/>
    /// (overwrite). A failure partway through the write never leaves a truncated or partially
    /// written <c>recording-settings.json</c> -- at worst, an orphaned temp file, which this
    /// method also best-effort cleans up.
    /// </summary>
    public void Save(SettingsFileContents contents)
    {
        Directory.CreateDirectory(_configDirectory);

        var configFilePath = Path.Combine(_configDirectory, ConfigFileName);
        var tempFilePath = Path.Combine(_configDirectory, $".{ConfigFileName}.{Guid.NewGuid():N}.tmp");

        var json = JsonSerializer.Serialize(contents, new JsonSerializerOptions { WriteIndented = true });

        try
        {
            File.WriteAllText(tempFilePath, json);
            File.Move(tempFilePath, configFilePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                }
                catch
                {
                    // Best-effort cleanup of the temp file if the move itself failed -- matches
                    // the codebase's existing best-effort cleanup convention (e.g.
                    // FileLogger.PurgeOldFiles).
                }
            }
        }
    }
}
