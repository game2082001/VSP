namespace VSP.Device.Discovery.NetworkScan;

public interface INetworkReachabilityProbe
{
    Task<NetworkReachabilityClassification> ProbeAsync(
        NetworkScanTarget target,
        CancellationToken cancellationToken);
}
