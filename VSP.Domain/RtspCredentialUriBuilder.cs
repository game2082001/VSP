namespace VSP.Domain;

/// <summary>
/// Attaches (or preserves) RTSP userinfo credentials on a runtime playback URI -- the one
/// credential channel FFmpeg's RTSP client actually supports (there is no separate
/// username/password parameter anywhere in VSP.Player; <c>RtspMediaSession</c> opens whatever
/// URL string it is given). Used identically by both the ONVIF-resolved and manually-configured
/// RTSP Live View paths so their authentication behavior is identical. Never persisted --
/// callers must treat the result as runtime-only and must never write it back to
/// <c>Camera.RtspUrl</c>.
///
/// Precedence when the source URI already carries userinfo (e.g. a manually-typed
/// <c>rtsp://user:pass@host</c> URL predating this feature): an explicitly configured
/// <c>Camera.Username</c> replaces it entirely -- built fresh from Scheme/Host/Port/
/// PathAndQuery/Fragment, never from the source URI's own UserInfo, so old and new credentials
/// can never concatenate into something like <c>user:pass@user:pass@host</c>. Only when
/// <c>Camera.Username</c> is blank is the existing userinfo, if any, preserved untouched for
/// backward compatibility with cameras already configured that way.
/// </summary>
public static class RtspCredentialUriBuilder
{
    public static Uri Build(Uri baseUri, string? username, string? password)
    {
        ArgumentNullException.ThrowIfNull(baseUri);

        if (string.IsNullOrWhiteSpace(username))
        {
            return baseUri;
        }

        var escapedUser = Uri.EscapeDataString(username);
        var escapedPassword = Uri.EscapeDataString(password ?? string.Empty);

        var candidate =
            $"{baseUri.Scheme}://{escapedUser}:{escapedPassword}@{baseUri.Host}:{baseUri.Port}{baseUri.PathAndQuery}{baseUri.Fragment}";

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var authenticatedUri))
        {
            // Uri.EscapeDataString should always produce a parseable URI; surfacing a clear
            // exception here is safer than silently falling back to an unauthenticated URI.
            throw new InvalidOperationException("Failed to construct an authenticated RTSP URI.");
        }

        return authenticatedUri;
    }
}
