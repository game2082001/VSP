using System.Net;
using System.Runtime.CompilerServices;
using VSP.Device.Discovery.Onvif;
using Xunit;

namespace VSP.Tests.Discovery;

public class OnvifDiscoveryServiceTests
{
    [Fact]
    public async Task DiscoverAsync_UsesExpectedProbeAndMulticastEndpoint()
    {
        string? capturedPayload = null;
        IPEndPoint? capturedEndpoint = null;

        var service = new OnvifDiscoveryService(
            new FakeWsDiscoveryTransport((payload, endpoint, _) =>
            {
                capturedPayload = payload;
                capturedEndpoint = endpoint;
                return EmptyAsync();
            }));

        var results = await service.DiscoverAsync();

        Assert.Empty(results);
        Assert.NotNull(capturedPayload);
        Assert.Contains("dn:NetworkVideoTransmitter", capturedPayload);
        Assert.Contains("urn:schemas-xmlsoap-org:ws:2005:04:discovery", capturedPayload);
        Assert.NotNull(capturedEndpoint);
        Assert.Equal("239.255.255.250", capturedEndpoint!.Address.ToString());
        Assert.Equal(3702, capturedEndpoint.Port);
    }

    [Fact]
    public async Task DiscoverAsync_ReturnsCollectedResultsWhenTimeoutOccurs()
    {
        var service = new OnvifDiscoveryService(
            new FakeWsDiscoveryTransport((_, _, cancellationToken) => TimeoutAfterOneMessageAsync(cancellationToken)));

        var results = await service.DiscoverAsync(new OnvifDiscoveryRequest
        {
            Timeout = TimeSpan.FromMilliseconds(50)
        });

        var result = Assert.Single(results);
        Assert.Equal("urn:uuid:timeout-device", result.EndpointReferenceAddress);
    }

    [Fact]
    public async Task DiscoverAsync_PropagatesUserCancellation()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var service = new OnvifDiscoveryService(
            new FakeWsDiscoveryTransport((_, _, cancellationToken) => WaitForeverAsync(cancellationToken)));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.DiscoverAsync(cancellationToken: cancellationTokenSource.Token));
    }

    [Fact]
    public async Task DiscoverAsync_DeduplicatesByEndpointReferenceAndFillsMissingFields()
    {
        var firstResponse = CreateProbeMatchResponse(
            "urn:uuid:device-1",
            "http://192.168.1.10/onvif/device_service",
            "onvif://www.onvif.org/name/Front%20Door");
        var secondResponse = CreateProbeMatchResponse(
            "urn:uuid:device-1",
            "http://192.168.1.10/onvif/device_service",
            "onvif://www.onvif.org/location/Lobby onvif://www.onvif.org/hardware/Model-X");

        var service = new OnvifDiscoveryService(
            new FakeWsDiscoveryTransport((_, _, _) => FromMessagesAsync(
            [
                new WsDiscoveryTransportMessage
                {
                    Payload = firstResponse,
                    RemoteEndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.10"), 3702)
                },
                new WsDiscoveryTransportMessage
                {
                    Payload = secondResponse,
                    RemoteEndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.10"), 3702)
                }
            ])));

        var results = await service.DiscoverAsync();

        var result = Assert.Single(results);
        Assert.Equal("Front Door", result.Name);
        Assert.Equal("Lobby", result.Location);
        Assert.Equal("Model-X", result.Model);
    }

    [Fact]
    public async Task DiscoverAsync_DeduplicatesByNormalizedXAddrWhenEndpointReferenceIsMissing()
    {
        var firstResponse = CreateProbeMatchWithoutEndpointReference(
            "http://192.168.1.15/onvif/device_service",
            "onvif://www.onvif.org/name/Camera-A");
        var secondResponse = CreateProbeMatchWithoutEndpointReference(
            "HTTP://192.168.1.15/onvif/device_service",
            "onvif://www.onvif.org/location/Warehouse");

        var service = new OnvifDiscoveryService(
            new FakeWsDiscoveryTransport((_, _, _) => FromMessagesAsync(
            [
                new WsDiscoveryTransportMessage
                {
                    Payload = firstResponse,
                    RemoteEndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.15"), 3702)
                },
                new WsDiscoveryTransportMessage
                {
                    Payload = secondResponse,
                    RemoteEndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.15"), 3702)
                }
            ])));

        var results = await service.DiscoverAsync();

        var result = Assert.Single(results);
        Assert.Equal("Camera-A", result.Name);
        Assert.Equal("Warehouse", result.Location);
    }

    [Fact]
    public async Task DiscoverAsync_DeduplicatesByRemoteAddressWhenNoEndpointInformationExists()
    {
        var payload = """
                      <Envelope xmlns:d="http://schemas.xmlsoap.org/ws/2005/04/discovery">
                        <Body>
                          <d:ProbeMatches>
                            <d:ProbeMatch>
                              <d:Scopes>onvif://www.onvif.org/name/RemoteOnly</d:Scopes>
                            </d:ProbeMatch>
                          </d:ProbeMatches>
                        </Body>
                      </Envelope>
                      """;

        var service = new OnvifDiscoveryService(
            new FakeWsDiscoveryTransport((_, _, _) => FromMessagesAsync(
            [
                new WsDiscoveryTransportMessage
                {
                    Payload = payload,
                    RemoteEndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.25"), 3702)
                },
                new WsDiscoveryTransportMessage
                {
                    Payload = payload,
                    RemoteEndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.25"), 3702)
                }
            ])));

        var results = await service.DiscoverAsync();

        var result = Assert.Single(results);
        Assert.Equal("192.168.1.25", result.RemoteAddress);
        Assert.Equal("RemoteOnly", result.Name);
    }

    private static async IAsyncEnumerable<WsDiscoveryTransportMessage> EmptyAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    private static async IAsyncEnumerable<WsDiscoveryTransportMessage> FromMessagesAsync(
        IReadOnlyList<WsDiscoveryTransportMessage> messages,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return message;
            await Task.Yield();
        }
    }

    private static async IAsyncEnumerable<WsDiscoveryTransportMessage> TimeoutAfterOneMessageAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new WsDiscoveryTransportMessage
        {
            Payload = CreateProbeMatchResponse(
                "urn:uuid:timeout-device",
                "http://192.168.1.30/onvif/device_service",
                "onvif://www.onvif.org/name/TimeoutCamera"),
            RemoteEndPoint = new IPEndPoint(IPAddress.Parse("192.168.1.30"), 3702)
        };

        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
    }

    private static async IAsyncEnumerable<WsDiscoveryTransportMessage> WaitForeverAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        yield break;
    }

    private static string CreateProbeMatchResponse(string endpointReferenceAddress, string xaddr, string scopes)
    {
        return
            $"""
             <Envelope xmlns:a="http://schemas.xmlsoap.org/ws/2004/08/addressing"
                       xmlns:d="http://schemas.xmlsoap.org/ws/2005/04/discovery">
               <Body>
                 <d:ProbeMatches>
                   <d:ProbeMatch>
                     <a:EndpointReference>
                       <a:Address>{endpointReferenceAddress}</a:Address>
                     </a:EndpointReference>
                     <d:Scopes>{scopes}</d:Scopes>
                     <d:XAddrs>{xaddr}</d:XAddrs>
                   </d:ProbeMatch>
                 </d:ProbeMatches>
               </Body>
             </Envelope>
             """;
    }

    private static string CreateProbeMatchWithoutEndpointReference(string xaddr, string scopes)
    {
        return
            $"""
             <Envelope xmlns:d="http://schemas.xmlsoap.org/ws/2005/04/discovery">
               <Body>
                 <d:ProbeMatches>
                   <d:ProbeMatch>
                     <d:Scopes>{scopes}</d:Scopes>
                     <d:XAddrs>{xaddr}</d:XAddrs>
                   </d:ProbeMatch>
                 </d:ProbeMatches>
               </Body>
             </Envelope>
             """;
    }

    private sealed class FakeWsDiscoveryTransport(
        Func<string, IPEndPoint, CancellationToken, IAsyncEnumerable<WsDiscoveryTransportMessage>> probeHandler)
        : IWsDiscoveryTransport
    {
        public IAsyncEnumerable<WsDiscoveryTransportMessage> ProbeAsync(
            string payload,
            IPEndPoint multicastEndpoint,
            CancellationToken cancellationToken)
        {
            return probeHandler(payload, multicastEndpoint, cancellationToken);
        }
    }
}
