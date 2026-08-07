using VSP.Core.Configuration;
using VSP.Core.Logging;

namespace VSP.Infrastructure.Settings;

/// <summary>
/// Domain-level read/write API for the Settings UI. Delegates all actual file I/O to
/// <see cref="SettingsFileStore"/> (<c>VSP.Core.Configuration</c>) -- the same store
/// <c>VSP.Player.Recording.RecordingPathProvider</c> uses -- so <c>recording-settings.json</c>
/// has exactly one reader/writer implementation. Owns: defaulting for all four fields,
/// Retention Days bounds enforcement, and the Language/Theme persisted-string mappings. Per
/// Epic-016.
/// </summary>
public sealed class AppSettingsProvider
{
    private static readonly string DefaultConfigDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VSP");

    private const string EnglishLanguageCode = "en-US";
    private const string TraditionalChineseLanguageCode = "zh-TW";

    private readonly string _configDirectory;
    private readonly SettingsFileStore _store;

    public AppSettingsProvider() : this(DefaultConfigDirectory)
    {
    }

    /// <summary>Test seam: resolves against an arbitrary config directory instead of %LocalAppData%\VSP.</summary>
    internal AppSettingsProvider(string configDirectory)
    {
        _configDirectory = configDirectory;
        _store = new SettingsFileStore(configDirectory);
    }

    /// <summary>
    /// Never throws. Every field defaults independently -- a bad value in one field never
    /// discards a valid value in another (<see cref="SettingsFileStore.Load"/> already handles
    /// the whole-file-corrupt case; this handles the one-field-invalid case).
    /// </summary>
    public AppSettings Load()
    {
        var raw = _store.Load();

        return new AppSettings
        {
            RecordingPath = ResolveRecordingPath(raw.RecordingRoot),
            RetentionDays = ResolveRetentionDays(raw.RetentionDays),
            Language = ResolveLanguage(raw.Language),
            Theme = ResolveTheme(raw.Theme)
        };
    }

    /// <summary>
    /// Writes all four fields in one pass. Callers always pass a complete, current
    /// <see cref="AppSettings"/> snapshot (the Settings ViewModel loads once and keeps every
    /// field in memory), so this never needs to merge with what's on disk -- changing one field
    /// can never drop another.
    /// </summary>
    public void Save(AppSettings settings)
    {
        _store.Save(new SettingsFileContents
        {
            RecordingRoot = settings.RecordingPath,
            RetentionDays = settings.RetentionDays,
            Language = ToLanguageCode(settings.Language),
            Theme = settings.Theme.ToString()
        });
    }

    private string ResolveRecordingPath(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? RecordingRootDefaults.Compute(_configDirectory) : raw;

    private static int ResolveRetentionDays(int? raw)
    {
        if (raw is null)
        {
            return AppSettingsLimits.DefaultRetentionDays;
        }

        if (raw.Value < AppSettingsLimits.MinRetentionDays || raw.Value > AppSettingsLimits.MaxRetentionDays)
        {
            AppLog.Warning(
                $"Settings file has an out-of-range RetentionDays value ({raw.Value}); using the default ({AppSettingsLimits.DefaultRetentionDays}).");
            return AppSettingsLimits.DefaultRetentionDays;
        }

        return raw.Value;
    }

    private static AppLanguage ResolveLanguage(string? raw) => raw switch
    {
        null => AppLanguage.English,
        EnglishLanguageCode => AppLanguage.English,
        TraditionalChineseLanguageCode => AppLanguage.TraditionalChinese,
        _ => LogUnrecognizedLanguage(raw)
    };

    private static AppLanguage LogUnrecognizedLanguage(string raw)
    {
        AppLog.Warning($"Settings file has an unrecognized Language value ('{raw}'); using the default (English).");
        return AppLanguage.English;
    }

    private static string ToLanguageCode(AppLanguage language) => language switch
    {
        AppLanguage.TraditionalChinese => TraditionalChineseLanguageCode,
        _ => EnglishLanguageCode
    };

    private static AppTheme ResolveTheme(string? raw)
    {
        if (raw is null)
        {
            return AppTheme.System;
        }

        if (Enum.TryParse<AppTheme>(raw, ignoreCase: false, out var parsed))
        {
            return parsed;
        }

        AppLog.Warning($"Settings file has an unrecognized Theme value ('{raw}'); using the default (System).");
        return AppTheme.System;
    }
}
