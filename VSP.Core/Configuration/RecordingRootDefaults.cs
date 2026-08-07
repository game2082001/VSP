namespace VSP.Core.Configuration;

/// <summary>
/// The one formula for "what recording root applies when none is configured" -- a pure
/// function, no side effects, no directory creation. Shared by
/// <c>VSP.Player.Recording.RecordingPathProvider</c> (which creates the directory itself, as
/// part of its own existing contract) and <c>VSP.Infrastructure.Settings.AppSettingsProvider</c>
/// (which only needs the value for display) so the default is computed identically wherever
/// it's needed, per Epic-016.
/// </summary>
public static class RecordingRootDefaults
{
    private const string RecordingsFolderName = "Recordings";

    public static string Compute(string configDirectory) => Path.Combine(configDirectory, RecordingsFolderName);
}
