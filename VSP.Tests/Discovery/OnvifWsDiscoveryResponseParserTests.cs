using System.Net;
using VSP.Device.Discovery.Onvif;
using Xunit;

namespace VSP.Tests.Discovery;

public class OnvifWsDiscoveryResponseParserTests
{
    private readonly OnvifWsDiscoveryResponseParser _parser = new();

    [Fact]
    public void Parse_ReturnsEmptyForMalformedXml()
    {
        var results = _parser.Parse("<not-valid");

        Assert.Empty(results);
    }

    [Fact]
    public void Parse_ExtractsEndpointMetadataAndScopes()
    {
        var results = _parser.Parse(
            """
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"
                        xmlns:a="http://schemas.xmlsoap.org/ws/2004/08/addressing"
                        xmlns:d="http://schemas.xmlsoap.org/ws/2005/04/discovery"
                        xmlns:dn="http://www.onvif.org/ver10/network/wsdl">
              <s:Body>
                <d:ProbeMatches>
                  <d:ProbeMatch>
                    <a:EndpointReference>
                      <a:Address>urn:uuid:device-1</a:Address>
                    </a:EndpointReference>
                    <d:Types>dn:NetworkVideoTransmitter</d:Types>
                    <d:Scopes>onvif://www.onvif.org/name/Front%20Door onvif://www.onvif.org/location/Lobby onvif://www.onvif.org/hardware/Model-X</d:Scopes>
                    <d:XAddrs>http://192.168.1.10/onvif/device_service</d:XAddrs>
                  </d:ProbeMatch>
                </d:ProbeMatches>
              </s:Body>
            </s:Envelope>
            """,
            new IPEndPoint(IPAddress.Parse("192.168.1.200"), 3702));

        var result = Assert.Single(results);
        Assert.Equal("urn:uuid:device-1", result.EndpointReferenceAddress);
        Assert.Equal("http://192.168.1.10/onvif/device_service", result.DiscoveryEndpoint);
        Assert.Equal("192.168.1.10", result.IpAddress);
        Assert.Equal("192.168.1.200", result.RemoteAddress);
        Assert.Equal("Front Door", result.Name);
        Assert.Equal("Lobby", result.Location);
        Assert.Equal("Model-X", result.Model);
        Assert.Null(result.Manufacturer);
        Assert.Single(result.Types);
        Assert.Single(result.XAddrs);
        Assert.Equal(3, result.Scopes.Count);
    }

    [Fact]
    public void Parse_UsesRemoteAddressWhenXAddrIsMissing()
    {
        var results = _parser.Parse(
            """
            <Envelope xmlns:a="http://schemas.xmlsoap.org/ws/2004/08/addressing"
                      xmlns:d="http://schemas.xmlsoap.org/ws/2005/04/discovery">
              <Body>
                <d:ProbeMatches>
                  <d:ProbeMatch>
                    <a:EndpointReference>
                      <a:Address>urn:uuid:device-2</a:Address>
                    </a:EndpointReference>
                    <d:Scopes>onvif://www.onvif.org/name/Camera-2</d:Scopes>
                  </d:ProbeMatch>
                </d:ProbeMatches>
              </Body>
            </Envelope>
            """,
            new IPEndPoint(IPAddress.Parse("10.0.0.20"), 3702));

        var result = Assert.Single(results);
        Assert.Null(result.DiscoveryEndpoint);
        Assert.Equal("10.0.0.20", result.IpAddress);
        Assert.Equal("10.0.0.20", result.RemoteAddress);
    }

    [Fact]
    public void Parse_ReturnsMultipleProbeMatches()
    {
        var results = _parser.Parse(
            """
            <Envelope xmlns:a="http://schemas.xmlsoap.org/ws/2004/08/addressing"
                      xmlns:d="http://schemas.xmlsoap.org/ws/2005/04/discovery">
              <Body>
                <d:ProbeMatches>
                  <d:ProbeMatch>
                    <a:EndpointReference><a:Address>urn:uuid:one</a:Address></a:EndpointReference>
                    <d:XAddrs>http://192.168.1.11/onvif/device_service</d:XAddrs>
                  </d:ProbeMatch>
                  <d:ProbeMatch>
                    <a:EndpointReference><a:Address>urn:uuid:two</a:Address></a:EndpointReference>
                    <d:XAddrs>http://192.168.1.12/onvif/device_service</d:XAddrs>
                  </d:ProbeMatch>
                </d:ProbeMatches>
              </Body>
            </Envelope>
            """);

        Assert.Equal(2, results.Count);
        Assert.Equal("urn:uuid:one", results[0].EndpointReferenceAddress);
        Assert.Equal("urn:uuid:two", results[1].EndpointReferenceAddress);
    }
}
