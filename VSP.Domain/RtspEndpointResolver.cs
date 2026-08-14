namespace VSP.Domain;

/// <summary>
/// Resolves the effective RTSP connection endpoint for a camera. An explicit port already
/// embedded in <c>RtspUrl</c> always wins (preserves legacy records and anything CameraDetail
/// has already normalized); <c>rtspPort</c> is used only as the fallback when the URL has none.
/// </summary>
public static class RtspEndpointResolver
{
    public static bool TryResolve(string? rtspUrl, int rtspPort, out Uri effectiveUri)
    {
        if (!Uri.TryCreate(rtspUrl, UriKind.Absolute, out var parsedUri))
        {
            effectiveUri = null!;
            return false;
        }

        if (parsedUri.Port > 0)
        {
            effectiveUri = parsedUri;
            return true;
        }

        var builder = new UriBuilder(parsedUri) { Port = rtspPort };
        effectiveUri = builder.Uri;
        return true;
    }
}
