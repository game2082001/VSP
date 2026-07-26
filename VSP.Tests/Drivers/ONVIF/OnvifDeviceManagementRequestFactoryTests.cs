using System.Xml.Linq;
using VSP.Device.Drivers.ONVIF;
using Xunit;

namespace VSP.Tests.Drivers.ONVIF;

public class OnvifDeviceManagementRequestFactoryTests
{
    private readonly OnvifDeviceManagementRequestFactory _factory = new();

    [Fact]
    public void BuildGetSystemDateAndTimeRequest_ContainsRequestElement()
    {
        var xml = _factory.BuildGetSystemDateAndTimeRequest();
        var document = XDocument.Parse(xml);

        Assert.Contains(document.Descendants(), e => e.Name.LocalName == "GetSystemDateAndTime");
    }

    [Fact]
    public void BuildGetSystemDateAndTimeRequest_HasNoSecurityHeader()
    {
        var xml = _factory.BuildGetSystemDateAndTimeRequest();
        var document = XDocument.Parse(xml);

        Assert.DoesNotContain(document.Descendants(), e => e.Name.LocalName == "Security");
    }

    [Fact]
    public void BuildGetDeviceInformationRequest_WithUsername_IncludesSecurityHeaderWithMatchingUsername()
    {
        var xml = _factory.BuildGetDeviceInformationRequest("admin", "secret");
        var document = XDocument.Parse(xml);

        Assert.Contains(document.Descendants(), e => e.Name.LocalName == "Security");
        var username = document.Descendants().First(e => e.Name.LocalName == "Username").Value;
        Assert.Equal("admin", username);
    }

    [Fact]
    public void BuildGetDeviceInformationRequest_WithoutUsername_HasNoSecurityHeader()
    {
        var xml = _factory.BuildGetDeviceInformationRequest(null, null);
        var document = XDocument.Parse(xml);

        Assert.DoesNotContain(document.Descendants(), e => e.Name.LocalName == "Security");
        Assert.Contains(document.Descendants(), e => e.Name.LocalName == "GetDeviceInformation");
    }
}
