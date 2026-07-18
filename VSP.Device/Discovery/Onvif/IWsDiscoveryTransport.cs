using System.Net;

namespace VSP.Device.Discovery.Onvif;

public interface IWsDiscoveryTransport
{
    IAsyncEnumerable<WsDiscoveryTransportMessage> ProbeAsync(
        string payload,
        IPEndPoint multicastEndpoint,
        CancellationToken cancellationToken);
}
