using VSP.Device.Drivers.Abstractions;
using VSP.Device.Drivers.Dahua;
using VSP.Device.Drivers.Hikvision;
using VSP.Device.Drivers.ONVIF;
using VSP.Device.Drivers.RTSP;
using VSP.Domain.Enums;

namespace VSP.Device.Drivers;

public static class DriverFactory
{
    public static ICameraDriver CreateCameraDriver(DeviceConnectionType connectionType)
    {
        return connectionType switch
        {
            DeviceConnectionType.HikvisionISAPI => new HikvisionIsapiCameraDriver(),

            DeviceConnectionType.DahuaNetSDK => new DahuaNetSdkCameraDriver(),

            DeviceConnectionType.ONVIF => new OnvifCameraDriver(),

            DeviceConnectionType.RTSP => new RtspCameraDriver(),

            _ => new RtspCameraDriver()
        };
    }
}