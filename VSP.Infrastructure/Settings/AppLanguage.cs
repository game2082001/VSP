namespace VSP.Infrastructure.Settings;

/// <summary>
/// Persisted as a stable culture code -- "en-US" / "zh-TW" -- never as this enum's ordinal or
/// member name. See <see cref="AppSettingsProvider"/> for the mapping. Selecting
/// <see cref="TraditionalChinese"/> is a placeholder for v1.0: it persists correctly but has no
/// translation effect (no localized resources exist yet).
/// </summary>
public enum AppLanguage
{
    English,
    TraditionalChinese
}
