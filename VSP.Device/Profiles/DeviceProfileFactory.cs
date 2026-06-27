using VSP.Domain.Entities;
using VSP.Domain.Enums;

namespace VSP.Device.Profiles;

public static class DeviceProfileFactory
{
    public static List<DeviceProfile> GetProfiles(CameraBrand brand)
    {
        return brand switch
        {
            CameraBrand.Hikvision => new List<DeviceProfile>
            {
                HikvisionISAPI(),
                ONVIF(),
                RTSP()
            },

            CameraBrand.Dahua => new List<DeviceProfile>
            {
                DahuaNetSDK(),
                ONVIF(),
                RTSP()
            },

            CameraBrand.ONVIF => new List<DeviceProfile>
            {
                ONVIF()
            },

            CameraBrand.RTSP => new List<DeviceProfile>
            {
                RTSP()
            },

            _ => new List<DeviceProfile>
            {
                RTSP()
            }
        };
    }

    private static DeviceProfile HikvisionISAPI()
    {
        return new DeviceProfile
        {
            Name = "Hikvision ISAPI",
            Brand = CameraBrand.Hikvision,
            ConnectionType = DeviceConnectionType.HikvisionISAPI,
            ShowHttpPort = true,
            ShowRtspPort = false,
            ShowSdkPort = true,
            ShowRtspUrl = false,
            Capability = new DeviceCapability
            {
                SupportsLiveView = true,
                SupportsPlayback = true,
                SupportsPTZ = true,
                SupportsSnapshot = true,
                SupportsEvent = true
            }
        };
    }

    private static DeviceProfile DahuaNetSDK()
    {
        return new DeviceProfile
        {
            Name = "Dahua NetSDK",
            Brand = CameraBrand.Dahua,
            ConnectionType = DeviceConnectionType.DahuaNetSDK,
            ShowHttpPort = true,
            ShowRtspPort = false,
            ShowSdkPort = true,
            ShowRtspUrl = false,
            Capability = new DeviceCapability
            {
                SupportsLiveView = true,
                SupportsPlayback = true,
                SupportsPTZ = true,
                SupportsSnapshot = true,
                SupportsEvent = true
            }
        };
    }

    private static DeviceProfile ONVIF()
    {
        return new DeviceProfile
        {
            Name = "ONVIF",
            Brand = CameraBrand.ONVIF,
            ConnectionType = DeviceConnectionType.ONVIF,
            ShowHttpPort = true,
            ShowRtspPort = false,
            ShowSdkPort = false,
            ShowRtspUrl = false,
            Capability = new DeviceCapability
            {
                SupportsLiveView = true,
                SupportsPTZ = true,
                SupportsSnapshot = true,
                SupportsDiscovery = true
            }
        };
    }

    private static DeviceProfile RTSP()
    {
        return new DeviceProfile
        {
            Name = "RTSP",
            Brand = CameraBrand.RTSP,
            ConnectionType = DeviceConnectionType.RTSP,
            ShowHttpPort = false,
            ShowRtspPort = true,
            ShowSdkPort = false,
            ShowRtspUrl = true,
            Capability = new DeviceCapability
            {
                SupportsLiveView = true,
                SupportsSnapshot = true
            }
        };
    }
}