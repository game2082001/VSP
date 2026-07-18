namespace VSP.Device.Discovery.Rtsp;

public sealed class RtspResponseClassificationResult
{
    public bool ReceivedAnyBytes { get; init; }

    public bool IsRtspResponse { get; init; }

    public int? StatusCode { get; init; }

    public string? ReasonPhrase { get; init; }

    public RtspProbeResponseClassification Classification { get; init; }

    public string? WwwAuthenticate { get; init; }

    public string? RawStatusLine { get; init; }
}
