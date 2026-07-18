using System.Net;

namespace VSP.Device.Discovery.Onvif;

public sealed class WsDiscoveryTransportMessage
{
    public required string Payload { get; init; }

    public IPEndPoint? RemoteEndPoint { get; init; }
}
