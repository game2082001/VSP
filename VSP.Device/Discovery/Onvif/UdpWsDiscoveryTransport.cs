using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace VSP.Device.Discovery.Onvif;

public sealed class UdpWsDiscoveryTransport : IWsDiscoveryTransport
{
    public async IAsyncEnumerable<WsDiscoveryTransportMessage> ProbeAsync(
        string payload,
        IPEndPoint multicastEndpoint,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        ArgumentNullException.ThrowIfNull(multicastEndpoint);

        using var udpClient = new UdpClient(AddressFamily.InterNetwork);
        udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, 0));

        var buffer = Encoding.UTF8.GetBytes(payload);
        await udpClient.SendAsync(buffer, multicastEndpoint, cancellationToken);

        while (true)
        {
            var result = await udpClient.ReceiveAsync(cancellationToken);

            yield return new WsDiscoveryTransportMessage
            {
                Payload = Encoding.UTF8.GetString(result.Buffer),
                RemoteEndPoint = result.RemoteEndPoint
            };
        }
    }
}
