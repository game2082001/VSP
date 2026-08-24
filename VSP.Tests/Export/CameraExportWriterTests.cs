using VSP.Device.Export;
using VSP.Device.Import;
using VSP.Domain.Enums;
using EntityCamera = VSP.Domain.Entities.Camera;
using Xunit;

namespace VSP.Tests.Export;

public class CameraExportWriterTests
{
    [Fact]
    public void Write_WithNoCameras_ProducesOnlyHeaderRow()
    {
        var csv = CameraExportWriter.Write(Array.Empty<EntityCamera>());

        var lines = SplitLines(csv);
        Assert.Single(lines);
        Assert.Equal("Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Connection Type,RTSP URL,Location", lines[0]);
    }

    [Fact]
    public void Write_WithCamera_ProducesMatchingDataRow()
    {
        var camera = new EntityCamera
        {
            Name = "Front Door",
            Brand = CameraBrand.Hikvision,
            Model = "DS-2CD",
            IpAddress = "192.168.1.10",
            HttpPort = 80,
            RtspPort = 554,
            SdkPort = 8000,
            Username = "admin",
            Password = "secret",
            ConnectionType = DeviceConnectionType.ONVIF,
            RtspUrl = "",
            Location = "Entrance"
        };

        var csv = CameraExportWriter.Write(new[] { camera });

        var lines = SplitLines(csv);
        Assert.Equal(2, lines.Count);
        Assert.Equal("Front Door,Hikvision,DS-2CD,192.168.1.10,80,554,8000,admin,ONVIF,,Entrance", lines[1]);
        Assert.DoesNotContain("secret", csv);
    }

    [Fact]
    public void Write_WithCommaInField_QuotesTheField()
    {
        var camera = new EntityCamera { Name = "Lobby, North", IpAddress = "10.0.0.1" };

        var csv = CameraExportWriter.Write(new[] { camera });

        Assert.Contains("\"Lobby, North\"", csv);
    }

    [Fact]
    public void Write_RoundTripsThroughCsvImportParser()
    {
        var camera = new EntityCamera
        {
            Name = "Roundtrip Cam",
            Brand = CameraBrand.Dahua,
            Model = "IPC",
            IpAddress = "10.1.1.1",
            HttpPort = 8080,
            RtspPort = 555,
            SdkPort = 8001,
            Username = "user",
            Password = "pass",
            ConnectionType = DeviceConnectionType.HikvisionSDK,
            RtspUrl = "rtsp://10.1.1.1/live",
            Location = "Roof"
        };

        var csv = CameraExportWriter.Write(new[] { camera });

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv));
        var parser = new CsvImportParser();
        var rows = parser.Parse(stream).ToList();

        var row = Assert.Single(rows);
        Assert.Equal("Roundtrip Cam", row.Values["Name"]);
        Assert.Equal("Dahua", row.Values["Brand"]);
        Assert.Equal("10.1.1.1", row.Values["IP Address"]);
        Assert.Equal("user", row.Values["Username"]);
        Assert.Equal("8080", row.Values["HTTP Port"]);
        Assert.Equal("555", row.Values["RTSP Port"]);
        Assert.Equal("8001", row.Values["SDK Port"]);
        Assert.Equal("HikvisionSDK", row.Values["Connection Type"]);
        Assert.Equal("rtsp://10.1.1.1/live", row.Values["RTSP URL"]);
        Assert.False(row.Values.ContainsKey("Password"));
    }

    private static List<string> SplitLines(string text)
    {
        return text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ToList();
    }
}
