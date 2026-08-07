namespace VSP.Core.Configuration;

/// <summary>
/// The literal, permissive shape of <c>recording-settings.json</c> -- nullable, no domain
/// enums, no defaults, no validation. A missing property deserializes to <c>null</c>; unknown
/// properties are ignored by <see cref="System.Text.Json.JsonSerializer"/>'s default behavior.
/// Consumed by both <c>VSP.Player.Recording.RecordingPathProvider</c> and
/// <c>VSP.Infrastructure.Settings.AppSettingsProvider</c> via <see cref="SettingsFileStore"/> --
/// the one shared reader/writer for this file, per Epic-016.
/// </summary>
public sealed class SettingsFileContents
{
    public string? RecordingRoot { get; init; }

    public int? RetentionDays { get; init; }

    public string? Language { get; init; }

    public string? Theme { get; init; }
}
