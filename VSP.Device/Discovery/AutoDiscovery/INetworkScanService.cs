using VSP.Device.Discovery.NetworkScan;

namespace VSP.Device.Discovery.AutoDiscovery;

public interface INetworkScanService
{
    Task<IReadOnlyList<NetworkScanResult>> ScanAsync(
        NetworkScanRequest? request = null,
        CancellationToken cancellationToken = default);
}
