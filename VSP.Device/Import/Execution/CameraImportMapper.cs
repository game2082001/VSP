using VSP.Device.Import.Preview;
using VSP.Domain.Entities;
using VSP.Domain.Enums;

namespace VSP.Device.Import.Execution;

public class CameraImportMapper
{
    public Camera Map(ImportPreviewRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        return new Camera
        {
            Name = row.Name,
            IpAddress = row.IPAddress,
            Brand = ParseBrand(row.Brand),
            ConnectionType = ResolveConnectionType(row.ConnectionType, row.Brand),
            Model = row.Model,
            Location = row.Location,
            HttpPort = ParsePort(row.HttpPort, 80),
            RtspPort = ParsePort(row.RtspPort, 554),
            SdkPort = ParsePort(row.SdkPort, 8000),
            Username = row.Username,
            RtspUrl = row.RtspUrl
        };
    }

    private static CameraBrand ParseBrand(string brand)
    {
        return Enum.TryParse<CameraBrand>(brand, ignoreCase: true, out var parsedBrand)
            ? parsedBrand
            : CameraBrand.Unknown;
    }

    private static DeviceConnectionType ResolveConnectionType(string connectionType, string brand)
    {
        if (TryParseNamedConnectionType(connectionType, out var parsedConnectionType))
        {
            return parsedConnectionType;
        }

        if (string.Equals(brand, CameraBrand.RTSP.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return DeviceConnectionType.RTSP;
        }

        if (string.Equals(brand, CameraBrand.ONVIF.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return DeviceConnectionType.ONVIF;
        }

        return DeviceConnectionType.Unknown;
    }

    private static bool TryParseNamedConnectionType(string connectionType, out DeviceConnectionType parsedConnectionType)
    {
        var trimmedConnectionType = connectionType.Trim();

        foreach (var name in Enum.GetNames<DeviceConnectionType>())
        {
            if (string.Equals(name, trimmedConnectionType, StringComparison.OrdinalIgnoreCase))
            {
                parsedConnectionType = Enum.Parse<DeviceConnectionType>(name);
                return true;
            }
        }

        parsedConnectionType = DeviceConnectionType.Unknown;
        return false;
    }

    private static int ParsePort(string value, int defaultValue)
    {
        return int.TryParse(value, out var port)
            ? port
            : defaultValue;
    }
}
