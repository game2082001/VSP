using System.Net.Sockets;

namespace VSP.Device.Discovery.NetworkScan;

public sealed class TcpNetworkReachabilityProbe : INetworkReachabilityProbe
{
    public async Task<NetworkReachabilityClassification> ProbeAsync(
        NetworkScanTarget target,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (target.Kind != NetworkScanTargetKind.HostPort || string.IsNullOrWhiteSpace(target.Host) || target.Port is null)
        {
            return NetworkReachabilityClassification.UnknownFailure;
        }

        try
        {
            using var tcpClient = new TcpClient();
            await tcpClient.ConnectAsync(target.Host, target.Port.Value, cancellationToken);

            return NetworkReachabilityClassification.Reachable;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (SocketException)
        {
            return NetworkReachabilityClassification.Unreachable;
        }
        catch (IOException)
        {
            return NetworkReachabilityClassification.Unreachable;
        }
    }
}
