using VSP.Device.Drivers.Dahua;
using VSP.Device.Drivers.Hikvision;
using VSP.Device.Drivers.ONVIF;
using VSP.Device.Drivers.RTSP;
using VSP.Domain.Enums;

namespace VSP.Device.Drivers.Plugins;

public sealed class BuiltInCameraDriverPlugin : IDriverPlugin
{
    private static readonly IReadOnlyList<DriverDescriptor> DriverDescriptors =
    [
        new DriverDescriptor(
            "hikvision.isapi",
            "Hikvision ISAPI Driver",
            DeviceConnectionType.HikvisionISAPI,
            static () => new HikvisionIsapiCameraDriver()),
        new DriverDescriptor(
            "dahua.netsdk",
            "Dahua NetSDK Driver",
            DeviceConnectionType.DahuaNetSDK,
            static () => new DahuaNetSdkCameraDriver()),
        new DriverDescriptor(
            "generic.onvif",
            "Generic ONVIF Driver",
            DeviceConnectionType.ONVIF,
            static () => new OnvifCameraDriver()),
        new DriverDescriptor(
            "generic.rtsp",
            "Generic RTSP Driver",
            DeviceConnectionType.RTSP,
            static () => new RtspCameraDriver())
    ];

    public string PluginId => "vsp.builtin.camera";

    public string DisplayName => "Built-in Camera Drivers";

    public IReadOnlyList<DriverDescriptor> GetDriverDescriptors()
    {
        return DriverDescriptors;
    }
}
