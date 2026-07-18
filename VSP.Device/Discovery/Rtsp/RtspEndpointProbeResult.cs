namespace VSP.Device.Discovery.Rtsp;

public sealed class RtspEndpointProbeResult
{
    public string NormalizedEndpointUri { get; init; } = string.Empty;

    public string Host { get; init; } = string.Empty;

    public int Port { get; init; }

    public string RequestMethod { get; init; } = "OPTIONS";

    public TimeSpan ResponseTime { get; init; }

    public bool TcpConnectionSucceeded { get; init; }

    public bool ReceivedAnyBytes { get; init; }

    public bool IsRtspResponse { get; init; }

    public int? RtspStatusCode { get; init; }

    public string? RtspReasonPhrase { get; init; }

    public RtspProbeResponseClassification ResponseClassification { get; init; }

    public string? WwwAuthenticate { get; init; }

    public bool TimeoutOccurred { get; init; }

    public string? RawStatusLine { get; init; }

    public string? ErrorCategory { get; init; }

    public string? ErrorMessage { get; init; }
}
