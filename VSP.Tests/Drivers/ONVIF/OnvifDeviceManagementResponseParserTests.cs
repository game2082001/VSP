using VSP.Device.Drivers.ONVIF;
using Xunit;

namespace VSP.Tests.Drivers.ONVIF;

public class OnvifDeviceManagementResponseParserTests
{
    private readonly OnvifDeviceManagementResponseParser _parser = new();

    private const string SystemDateAndTimeResponse =
        """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body>
            <tds:GetSystemDateAndTimeResponse xmlns:tds="http://www.onvif.org/ver10/device/wsdl">
              <tds:SystemDateAndTime/>
            </tds:GetSystemDateAndTimeResponse>
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

    private const string DeviceInformationResponse =
        """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body>
            <tds:GetDeviceInformationResponse xmlns:tds="http://www.onvif.org/ver10/device/wsdl">
              <tds:Manufacturer>Contoso</tds:Manufacturer>
              <tds:Model>CX-100</tds:Model>
              <tds:FirmwareVersion>1.2.3</tds:FirmwareVersion>
              <tds:SerialNumber>SN12345</tds:SerialNumber>
            </tds:GetDeviceInformationResponse>
          </s:Body>
        </s:Envelope>
        """;

    private const string PartialDeviceInformationResponse =
        """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body>
            <tds:GetDeviceInformationResponse xmlns:tds="http://www.onvif.org/ver10/device/wsdl">
              <tds:Manufacturer>Contoso</tds:Manufacturer>
              <tds:Model>CX-100</tds:Model>
            </tds:GetDeviceInformationResponse>
          </s:Body>
        </s:Envelope>
        """;

    [Fact]
    public void IsSuccessfulResponse_WithWellFormedResponse_ReturnsTrue()
    {
        Assert.True(_parser.IsSuccessfulResponse(SystemDateAndTimeResponse));
    }

    [Fact]
    public void IsSuccessfulResponse_WithFault_ReturnsFalse()
    {
        Assert.False(_parser.IsSuccessfulResponse(FaultResponse));
    }

    [Fact]
    public void IsSuccessfulResponse_WithMalformedXml_ReturnsFalse()
    {
        Assert.False(_parser.IsSuccessfulResponse("<not-xml"));
    }

    [Fact]
    public void IsSuccessfulResponse_WithNullOrEmpty_ReturnsFalse()
    {
        Assert.False(_parser.IsSuccessfulResponse(null));
        Assert.False(_parser.IsSuccessfulResponse(string.Empty));
    }

    [Fact]
    public void ParseDeviceInformation_WithFullResponse_ReturnsAllFields()
    {
        var info = _parser.ParseDeviceInformation(DeviceInformationResponse);

        Assert.NotNull(info);
        Assert.Equal("Contoso", info!.Manufacturer);
        Assert.Equal("CX-100", info.Model);
        Assert.Equal("1.2.3", info.FirmwareVersion);
        Assert.Equal("SN12345", info.SerialNumber);
    }

    [Fact]
    public void ParseDeviceInformation_WithPartialResponse_ReturnsAvailableFieldsAndNullForMissing()
    {
        var info = _parser.ParseDeviceInformation(PartialDeviceInformationResponse);

        Assert.NotNull(info);
        Assert.Equal("Contoso", info!.Manufacturer);
        Assert.Equal("CX-100", info.Model);
        Assert.Null(info.FirmwareVersion);
        Assert.Null(info.SerialNumber);
    }

    [Fact]
    public void ParseDeviceInformation_WithFault_ReturnsNull()
    {
        Assert.Null(_parser.ParseDeviceInformation(FaultResponse));
    }

    [Fact]
    public void ParseDeviceInformation_WithMalformedXml_ReturnsNull()
    {
        Assert.Null(_parser.ParseDeviceInformation("<not-xml"));
    }

    [Fact]
    public void ParseDeviceInformation_WithUnrelatedResponse_ReturnsNull()
    {
        Assert.Null(_parser.ParseDeviceInformation(SystemDateAndTimeResponse));
    }
}
