namespace VSP.Infrastructure.Settings;

/// <summary>The four v1.0 settings (Docs/V1.0_CUSTOMER_RELEASE_DEFINITION.md §2.4), as a single snapshot.</summary>
public sealed class AppSettings
{
    public required string RecordingPath { get; init; }

    public required int RetentionDays { get; init; }

    public required AppLanguage Language { get; init; }

    public required AppTheme Theme { get; init; }
}
