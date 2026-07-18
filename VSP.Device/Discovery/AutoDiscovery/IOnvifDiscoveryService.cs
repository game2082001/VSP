using VSP.Device.Discovery.Onvif;

namespace VSP.Device.Discovery.AutoDiscovery;

public interface IOnvifDiscoveryService
{
    Task<IReadOnlyList<OnvifDiscoveryResult>> DiscoverAsync(
        OnvifDiscoveryRequest? request = null,
        CancellationToken cancellationToken = default);
}
