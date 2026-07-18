using VSP.Device.Discovery.NetworkScan;
using VSP.Device.Discovery.Onvif;
using VSP.Device.Discovery.Rtsp;

namespace VSP.Device.Discovery.AutoDiscovery;

public sealed class AutoDiscoveryRequest
{
    public bool EnableOnvifDiscovery { get; init; }

    public bool EnableNetworkScan { get; init; }

    public bool EnableRtspEndpointProbe { get; init; }

    public OnvifDiscoveryRequest? OnvifDiscoveryRequest { get; init; }

    public NetworkScanRequest? NetworkScanRequest { get; init; }

    public IReadOnlyList<RtspEndpointProbeRequest> RtspEndpointProbeRequests { get; init; } =
        Array.Empty<RtspEndpointProbeRequest>();
}
