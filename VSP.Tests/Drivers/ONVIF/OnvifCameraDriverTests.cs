using VSP.Device.Drivers.ONVIF;
using Xunit;
using EntityCamera = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Drivers.ONVIF;

public class OnvifCameraDriverTests
{
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

    [Fact]
    public void TestConnection_WithSuccessfulResponse_ReturnsTrue()
    {
        using var server = new LoopbackHttpTestServer(SystemDateAndTimeResponse);
        var driver = new OnvifCameraDriver();
        var camera = CreateCamera(server.Port);

        var result = driver.TestConnection(camera);

        Assert.True(result);
    }

    [Fact]
    public void TestConnection_WithFaultResponse_ReturnsFalse()
    {
        using var server = new LoopbackHttpTestServer(FaultResponse);
        var driver = new OnvifCameraDriver();
        var camera = CreateCamera(server.Port);

        var result = driver.TestConnection(camera);

        Assert.False(result);
    }

    [Fact]
    public void TestConnection_WithNoListeningServer_ReturnsFalse()
    {
        var driver = new OnvifCameraDriver();
        var camera = CreateCamera(port: 1);

        var result = driver.TestConnection(camera);

        Assert.False(result);
    }

    [Fact]
    public void TestConnection_WithMissingIpAddress_ReturnsFalse()
    {
        var driver = new OnvifCameraDriver();
        var camera = CreateCamera(80);
        camera.IpAddress = "";

        var result = driver.TestConnection(camera);

        Assert.False(result);
    }

    [Fact]
    public void GetDeviceInformation_WithSuccessfulResponse_ReturnsPopulatedFields()
    {
        using var server = new LoopbackHttpTestServer(DeviceInformationResponse);
        var driver = new OnvifCameraDriver();
        var camera = CreateCamera(server.Port);

        var info = driver.GetDeviceInformation(camera);

        Assert.NotNull(info);
        Assert.Equal("Contoso", info!.Manufacturer);
        Assert.Equal("CX-100", info.Model);
        Assert.Equal("1.2.3", info.FirmwareVersion);
        Assert.Equal("SN12345", info.SerialNumber);
    }

    [Fact]
    public void GetDeviceInformation_WithFaultResponse_ReturnsNull()
    {
        using var server = new LoopbackHttpTestServer(FaultResponse);
        var driver = new OnvifCameraDriver();
        var camera = CreateCamera(server.Port);

        var info = driver.GetDeviceInformation(camera);

        Assert.Null(info);
    }

    [Fact]
    public void GetDeviceInformation_WithCredentials_SendsWsSecurityHeaderInRequest()
    {
        using var server = new LoopbackHttpTestServer(DeviceInformationResponse);
        var driver = new OnvifCameraDriver();
        var camera = CreateCamera(server.Port);
        camera.Username = "admin";
        camera.Password = "secret";

        driver.GetDeviceInformation(camera);

        Assert.True(server.WaitForRequest(TimeSpan.FromSeconds(3)));
        Assert.Contains("UsernameToken", server.ReceivedRequest);
        Assert.Contains("admin", server.ReceivedRequest);
    }

    [Fact]
    public void GetDeviceInformation_WithoutCredentials_DoesNotSendSecurityHeader()
    {
        using var server = new LoopbackHttpTestServer(DeviceInformationResponse);
        var driver = new OnvifCameraDriver();
        var camera = CreateCamera(server.Port);
        camera.Username = "";

        driver.GetDeviceInformation(camera);

        Assert.True(server.WaitForRequest(TimeSpan.FromSeconds(3)));
        Assert.DoesNotContain("UsernameToken", server.ReceivedRequest);
    }

    [Fact]
    public void GetDeviceInformation_WithMissingIpAddress_ReturnsNull()
    {
        var driver = new OnvifCameraDriver();
        var camera = CreateCamera(80);
        camera.IpAddress = "";

        var info = driver.GetDeviceInformation(camera);

        Assert.Null(info);
    }

    [Fact]
    public void Capability_DeclaresDeviceInformationSupport()
    {
        var driver = new OnvifCameraDriver();

        Assert.True(driver.Capability.SupportsDeviceInformation);
    }

    private static EntityCamera CreateCamera(int port)
    {
        return new EntityCamera
        {
            IpAddress = "127.0.0.1",
            HttpPort = port,
            Username = "admin",
            Password = "secret"
        };
    }
}
