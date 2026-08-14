namespace VSP.Device.Drivers.ONVIF;

/// <summary>
/// The one place ONVIF media-profile selection happens -- must not be duplicated or
/// re-decided elsewhere (e.g. in LiveViewViewModel).
///
/// Temporary policy for this RC1 defect fix: select the first profile token returned by
/// <c>GetProfiles</c>. ONVIF does not guarantee ordering or that every profile is
/// stream-capable, so this is a deliberately simple, deterministic placeholder -- not a
/// "best" or "main stream" selection. Building real profile/stream (Main/Sub) selection UI
/// is out of scope for this Task; when that work happens, this is the only method that
/// should need to change.
/// </summary>
public static class OnvifMediaProfileSelector
{
    public static string? SelectProfile(IReadOnlyList<string> profileTokens)
    {
        ArgumentNullException.ThrowIfNull(profileTokens);

        return profileTokens.Count > 0 ? profileTokens[0] : null;
    }
}
