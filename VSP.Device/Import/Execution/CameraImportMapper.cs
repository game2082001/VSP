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
            ConnectionType = ResolveConnectionType(row.Brand),
            Model = row.Model,
            Location = row.Location
        };
    }

    private static CameraBrand ParseBrand(string brand)
    {
        return Enum.TryParse<CameraBrand>(brand, ignoreCase: true, out var parsedBrand)
            ? parsedBrand
            : CameraBrand.Unknown;
    }

    private static DeviceConnectionType ResolveConnectionType(string brand)
    {
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
}
