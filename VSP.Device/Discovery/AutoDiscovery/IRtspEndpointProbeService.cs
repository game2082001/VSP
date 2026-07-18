using VSP.Device.Discovery.Rtsp;

namespace VSP.Device.Discovery.AutoDiscovery;

public interface IRtspEndpointProbeService
{
    Task<RtspEndpointProbeResult> ProbeAsync(
        RtspEndpointProbeRequest? request = null,
        CancellationToken cancellationToken = default);
}
