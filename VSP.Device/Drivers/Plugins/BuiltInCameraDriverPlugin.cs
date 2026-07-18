using VSP.Device.Drivers.Dahua;
using VSP.Device.Drivers.Hikvision;
using VSP.Device.Drivers.ONVIF;
using VSP.Device.Drivers.RTSP;
using VSP.Device.Drivers.Settings;
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
            static () => new HikvisionIsapiCameraDriver(),
            CreateHikvisionSettings()),
        new DriverDescriptor(
            "dahua.netsdk",
            "Dahua NetSDK Driver",
            DeviceConnectionType.DahuaNetSDK,
            static () => new DahuaNetSdkCameraDriver(),
            CreateDahuaSettings()),
        new DriverDescriptor(
            "generic.onvif",
            "Generic ONVIF Driver",
            DeviceConnectionType.ONVIF,
            static () => new OnvifCameraDriver(),
            CreateOnvifSettings()),
        new DriverDescriptor(
            "generic.rtsp",
            "Generic RTSP Driver",
            DeviceConnectionType.RTSP,
            static () => new RtspCameraDriver(),
            CreateRtspSettings())
    ];

    public string PluginId => "vsp.builtin.camera";

    public string DisplayName => "Built-in Camera Drivers";

    public IReadOnlyList<DriverDescriptor> GetDriverDescriptors()
    {
        return DriverDescriptors;
    }

    private static DriverSettingsDefinition CreateHikvisionSettings()
    {
        return new DriverSettingsDefinition(
        [
            CreatePortSetting(DriverSettingKey.HttpPort, "HTTP Port", 80),
            CreatePortSetting(DriverSettingKey.SdkPort, "SDK Port", 8000),
            new DriverSettingDefinition(DriverSettingKey.Username, "Username"),
            new DriverSettingDefinition(DriverSettingKey.Password, "Password", isSensitive: true)
        ]);
    }

    private static DriverSettingsDefinition CreateDahuaSettings()
    {
        return new DriverSettingsDefinition(
        [
            CreatePortSetting(DriverSettingKey.HttpPort, "HTTP Port", 80),
            CreatePortSetting(DriverSettingKey.SdkPort, "SDK Port", 8000),
            new DriverSettingDefinition(DriverSettingKey.Username, "Username"),
            new DriverSettingDefinition(DriverSettingKey.Password, "Password", isSensitive: true)
        ]);
    }

    private static DriverSettingsDefinition CreateOnvifSettings()
    {
        return new DriverSettingsDefinition(
        [
            CreatePortSetting(DriverSettingKey.HttpPort, "HTTP Port", 80),
            new DriverSettingDefinition(DriverSettingKey.Username, "Username"),
            new DriverSettingDefinition(DriverSettingKey.Password, "Password", isSensitive: true)
        ]);
    }

    private static DriverSettingsDefinition CreateRtspSettings()
    {
        return new DriverSettingsDefinition(
        [
            CreatePortSetting(DriverSettingKey.RtspPort, "RTSP Port", 554),
            new DriverSettingDefinition(DriverSettingKey.Username, "Username"),
            new DriverSettingDefinition(DriverSettingKey.Password, "Password", isSensitive: true),
            new DriverSettingDefinition(DriverSettingKey.RtspUrl, "RTSP URL", isRequired: true, defaultValue: string.Empty)
        ]);
    }

    private static DriverSettingDefinition CreatePortSetting(
        DriverSettingKey key,
        string displayName,
        int defaultValue)
    {
        return new DriverSettingDefinition(key, displayName, defaultValue: defaultValue);
    }
}
