using VSP.Device.Drivers.Abstractions;
using VSP.Device.Drivers.RTSP;
using VSP.Domain.Enums;

namespace VSP.Device.Drivers;

public static class DriverFactory
{
    private static readonly DriverRegistry DefaultRegistry = DriverRegistry.CreateDefault();

    public static ICameraDriver CreateCameraDriver(DeviceConnectionType connectionType)
    {
        return DefaultRegistry.CreateCameraDriver(connectionType) ?? new RtspCameraDriver();
    }

    public static bool IsDriverImplemented(DeviceConnectionType connectionType)
    {
        return connectionType switch
        {
            DeviceConnectionType.HikvisionISAPI => false,
            DeviceConnectionType.HikvisionSDK => false,
            DeviceConnectionType.DahuaNetSDK => false,
            DeviceConnectionType.ONVIF => false,
            DeviceConnectionType.RTSP => true,
            DeviceConnectionType.AxisVAPIX => false,
            _ => false
        };
    }
}
