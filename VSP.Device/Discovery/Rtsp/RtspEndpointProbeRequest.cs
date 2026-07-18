namespace VSP.Device.Discovery.Rtsp;

public sealed class RtspEndpointProbeRequest
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public string? RtspUri { get; init; }

    public string? Host { get; init; }

    public int? Port { get; init; }

    public string? Path { get; init; }

    public string? Username { get; init; }

    public string? Password { get; init; }

    public TimeSpan Timeout { get; init; } = DefaultTimeout;
}
