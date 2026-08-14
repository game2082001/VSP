using VSP.Device.Drivers.ONVIF;
using Xunit;

namespace VSP.Tests.Drivers.ONVIF;

public class OnvifMediaServiceResponseParserTests
{
    private readonly OnvifMediaServiceResponseParser _parser = new();

    private const string GetCapabilitiesResponse =
        """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body>
            <tds:GetCapabilitiesResponse xmlns:tds="http://www.onvif.org/ver10/device/wsdl">
              <tds:Capabilities xmlns:tt="http://www.onvif.org/ver10/schema">
                <tt:Media>
                  <tt:XAddr>http://192.168.0.89:1025/onvif/Media</tt:XAddr>
                </tt:Media>
              </tds:Capabilities>
            </tds:GetCapabilitiesResponse>
          </s:Body>
        </s:Envelope>
        """;

    private const string GetCapabilitiesResponseWithoutMedia =
        """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body>
            <tds:GetCapabilitiesResponse xmlns:tds="http://www.onvif.org/ver10/device/wsdl">
              <tds:Capabilities xmlns:tt="http://www.onvif.org/ver10/schema">
                <tt:Device>
                  <tt:XAddr>http://192.168.0.89:1025/onvif/device_service</tt:XAddr>
                </tt:Device>
              </tds:Capabilities>
            </tds:GetCapabilitiesResponse>
          </s:Body>
        </s:Envelope>
        """;

    private const string GetProfilesResponse =
        """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body>
            <trt:GetProfilesResponse xmlns:trt="http://www.onvif.org/ver10/media/wsdl">
              <trt:Profiles token="Profile_1" fixed="true">
                <tt:Name xmlns:tt="http://www.onvif.org/ver10/schema">MainStream</tt:Name>
              </trt:Profiles>
              <trt:Profiles token="Profile_2" fixed="true">
                <tt:Name xmlns:tt="http://www.onvif.org/ver10/schema">SubStream</tt:Name>
              </trt:Profiles>
            </trt:GetProfilesResponse>
          </s:Body>
        </s:Envelope>
        """;

    private const string GetProfilesResponseEmpty =
        """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body>
            <trt:GetProfilesResponse xmlns:trt="http://www.onvif.org/ver10/media/wsdl"/>
          </s:Body>
        </s:Envelope>
        """;

    private const string GetStreamUriResponse =
        """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body>
            <trt:GetStreamUriResponse xmlns:trt="http://www.onvif.org/ver10/media/wsdl">
              <trt:MediaUri xmlns:tt="http://www.onvif.org/ver10/schema">
                <tt:Uri>rtsp://192.168.0.89:1025/Streaming/Channels/101</tt:Uri>
                <tt:InvalidAfterConnect>false</tt:InvalidAfterConnect>
              </trt:MediaUri>
            </trt:GetStreamUriResponse>
          </s:Body>
        </s:Envelope>
        """;

    private const string FaultResponse =
        """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body>
            <s:Fault>
              <s:Reason><s:Text>Sender not Authorized</s:Text></s:Reason>
            </s:Fault>
          </s:Body>
        </s:Envelope>
        """;

    private const string MalformedXml = "this is not xml <<<";

    [Fact]
    public void HasFault_WithFaultResponse_ReturnsTrue()
    {
        Assert.True(_parser.HasFault(FaultResponse));
    }

    [Fact]
    public void HasFault_WithMalformedXml_ReturnsTrue()
    {
        Assert.True(_parser.HasFault(MalformedXml));
    }

    [Fact]
    public void HasFault_WithSuccessfulResponse_ReturnsFalse()
    {
        Assert.False(_parser.HasFault(GetCapabilitiesResponse));
    }

    [Fact]
    public void ParseMediaXAddr_WithSuccessfulResponse_ExtractsMediaXAddr()
    {
        var xAddr = _parser.ParseMediaXAddr(GetCapabilitiesResponse);

        Assert.Equal("http://192.168.0.89:1025/onvif/Media", xAddr);
    }

    [Fact]
    public void ParseMediaXAddr_WithoutMediaCapability_ReturnsNull()
    {
        var xAddr = _parser.ParseMediaXAddr(GetCapabilitiesResponseWithoutMedia);

        Assert.Null(xAddr);
    }

    [Fact]
    public void ParseMediaXAddr_WithFaultResponse_ReturnsNull()
    {
        Assert.Null(_parser.ParseMediaXAddr(FaultResponse));
    }

    [Fact]
    public void ParseMediaXAddr_WithMalformedXml_ReturnsNull()
    {
        Assert.Null(_parser.ParseMediaXAddr(MalformedXml));
    }

    [Fact]
    public void ParseProfileTokens_WithSuccessfulResponse_ExtractsTokensInOrder()
    {
        var tokens = _parser.ParseProfileTokens(GetProfilesResponse);

        Assert.Equal(["Profile_1", "Profile_2"], tokens);
    }

    [Fact]
    public void ParseProfileTokens_WithNoProfiles_ReturnsEmpty()
    {
        var tokens = _parser.ParseProfileTokens(GetProfilesResponseEmpty);

        Assert.Empty(tokens);
    }

    [Fact]
    public void ParseProfileTokens_WithFaultResponse_ReturnsEmpty()
    {
        Assert.Empty(_parser.ParseProfileTokens(FaultResponse));
    }

    [Fact]
    public void ParseProfileTokens_WithMalformedXml_ReturnsEmpty()
    {
        Assert.Empty(_parser.ParseProfileTokens(MalformedXml));
    }

    [Fact]
    public void ParseStreamUri_WithSuccessfulResponse_ExtractsUri()
    {
        var uri = _parser.ParseStreamUri(GetStreamUriResponse);

        Assert.Equal("rtsp://192.168.0.89:1025/Streaming/Channels/101", uri);
    }

    [Fact]
    public void ParseStreamUri_WithFaultResponse_ReturnsNull()
    {
        Assert.Null(_parser.ParseStreamUri(FaultResponse));
    }

    [Fact]
    public void ParseStreamUri_WithMalformedXml_ReturnsNull()
    {
        Assert.Null(_parser.ParseStreamUri(MalformedXml));
    }
}
