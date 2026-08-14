using System.Xml.Linq;
using VSP.Device.Drivers.ONVIF;
using Xunit;

namespace VSP.Tests.Drivers.ONVIF;

public class OnvifMediaServiceRequestFactoryTests
{
    private readonly OnvifMediaServiceRequestFactory _factory = new();

    [Fact]
    public void BuildGetCapabilitiesRequest_ContainsRequestElementAndMediaCategory()
    {
        var xml = _factory.BuildGetCapabilitiesRequest(null, null);
        var document = XDocument.Parse(xml);

        Assert.Contains(document.Descendants(), e => e.Name.LocalName == "GetCapabilities");
        var category = document.Descendants().First(e => e.Name.LocalName == "Category").Value;
        Assert.Equal("Media", category);
    }

    [Fact]
    public void BuildGetCapabilitiesRequest_WithoutUsername_HasNoSecurityHeader()
    {
        var xml = _factory.BuildGetCapabilitiesRequest(null, null);
        var document = XDocument.Parse(xml);

        Assert.DoesNotContain(document.Descendants(), e => e.Name.LocalName == "Security");
    }

    [Fact]
    public void BuildGetCapabilitiesRequest_WithUsername_IncludesSecurityHeaderWithMatchingUsername()
    {
        var xml = _factory.BuildGetCapabilitiesRequest("admin", "secret");
        var document = XDocument.Parse(xml);

        Assert.Contains(document.Descendants(), e => e.Name.LocalName == "Security");
        var username = document.Descendants().First(e => e.Name.LocalName == "Username").Value;
        Assert.Equal("admin", username);
    }

    [Fact]
    public void BuildGetProfilesRequest_ContainsRequestElement()
    {
        var xml = _factory.BuildGetProfilesRequest(null, null);
        var document = XDocument.Parse(xml);

        Assert.Contains(document.Descendants(), e => e.Name.LocalName == "GetProfiles");
    }

    [Fact]
    public void BuildGetProfilesRequest_WithUsername_IncludesSecurityHeader()
    {
        var xml = _factory.BuildGetProfilesRequest("admin", "secret");
        var document = XDocument.Parse(xml);

        Assert.Contains(document.Descendants(), e => e.Name.LocalName == "Security");
    }

    [Fact]
    public void BuildGetStreamUriRequest_ContainsProfileTokenAndRtspTransport()
    {
        var xml = _factory.BuildGetStreamUriRequest("Profile_1", null, null);
        var document = XDocument.Parse(xml);

        Assert.Contains(document.Descendants(), e => e.Name.LocalName == "GetStreamUri");
        var profileToken = document.Descendants().First(e => e.Name.LocalName == "ProfileToken").Value;
        Assert.Equal("Profile_1", profileToken);
        var protocolValue = document.Descendants().First(e => e.Name.LocalName == "Protocol").Value;
        Assert.Equal("RTSP", protocolValue);
    }

    [Fact]
    public void BuildGetStreamUriRequest_WithUsername_IncludesSecurityHeader()
    {
        var xml = _factory.BuildGetStreamUriRequest("Profile_1", "admin", "secret");
        var document = XDocument.Parse(xml);

        Assert.Contains(document.Descendants(), e => e.Name.LocalName == "Security");
    }

    [Fact]
    public void BuildGetStreamUriRequest_WithBlankProfileToken_Throws()
    {
        Assert.Throws<ArgumentException>(() => _factory.BuildGetStreamUriRequest("", null, null));
    }
}
