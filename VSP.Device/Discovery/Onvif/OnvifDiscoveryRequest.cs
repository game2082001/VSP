namespace VSP.Device.Discovery.Onvif;

public sealed class OnvifDiscoveryRequest
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(3);

    public TimeSpan Timeout { get; init; } = DefaultTimeout;
}
