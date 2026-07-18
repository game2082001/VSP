namespace VSP.Device.Discovery.Rtsp;

public sealed class RtspRequestFactory
{
    private const int DefaultPort = 554;
    private const string UserAgent = "VSP RTSP Probe";

    public Uri CreateEndpointUri(RtspEndpointProbeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.IsNullOrWhiteSpace(request.RtspUri))
        {
            if (!Uri.TryCreate(request.RtspUri, UriKind.Absolute, out var endpointUri))
            {
                throw new ArgumentException("RTSP URI is invalid.", nameof(request));
            }

            if (!endpointUri.Scheme.Equals("rtsp", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("RTSP URI must use the rtsp scheme.", nameof(request));
            }

            if (string.IsNullOrWhiteSpace(endpointUri.Host))
            {
                throw new ArgumentException("RTSP URI must include a host.", nameof(request));
            }

            return endpointUri;
        }

        if (string.IsNullOrWhiteSpace(request.Host))
        {
            throw new ArgumentException("Host is required when RTSP URI is not provided.", nameof(request));
        }

        var builder = new UriBuilder("rtsp", request.Host.Trim(), request.Port ?? DefaultPort)
        {
            Path = NormalizePath(request.Path)
        };

        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            builder.UserName = request.Username;
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            builder.Password = request.Password;
        }

        return builder.Uri;
    }

    public string CreateOptionsRequest(Uri endpointUri, int cSeq = 1)
    {
        ArgumentNullException.ThrowIfNull(endpointUri);

        var requestTarget = GetRequestTarget(endpointUri);

        return
            $"OPTIONS {requestTarget} RTSP/1.0\r\n" +
            $"CSeq: {cSeq}\r\n" +
            $"User-Agent: {UserAgent}\r\n" +
            "\r\n";
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        return path.StartsWith("/", StringComparison.Ordinal) ? path : $"/{path}";
    }

    private static string GetRequestTarget(Uri endpointUri)
    {
        var pathAndQuery = endpointUri.PathAndQuery;
        if (string.IsNullOrWhiteSpace(pathAndQuery) || pathAndQuery == "/")
        {
            return "*";
        }

        return endpointUri.ToString();
    }
}
