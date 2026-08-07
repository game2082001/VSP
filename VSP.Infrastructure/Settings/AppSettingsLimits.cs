namespace VSP.Infrastructure.Settings;

/// <summary>
/// Retention Days bounds -- defined once, used both by <see cref="AppSettingsProvider"/> (to
/// default/clamp an out-of-range persisted value) and by <c>VSP.UI.Validation.SettingsValidator</c>
/// (to validate interactive input). One set of numbers, not two.
/// </summary>
public static class AppSettingsLimits
{
    public const int MinRetentionDays = 1;

    /// <summary>10 years -- a generous upper sanity bound against corrupt/fat-finger values, not
    /// a claim that 10 years of retention is realistic or enforced (no retention behavior exists
    /// yet; this Epic only persists the value).</summary>
    public const int MaxRetentionDays = 3650;

    public const int DefaultRetentionDays = 30;
}
