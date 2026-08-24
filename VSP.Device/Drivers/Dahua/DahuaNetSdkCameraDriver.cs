using VSP.Device.Drivers.Abstractions;
using VSP.Domain.Entities;

namespace VSP.Device.Drivers.Dahua;

public class DahuaNetSdkCameraDriver : ICameraDriver
{
    public string DriverId => "dahua.netsdk";

    public string DisplayName => "Dahua NetSDK Driver";

    public DeviceCapability Capability { get; } = new()
    {
        SupportsLiveView = true,
        SupportsSnapshot = true,
        SupportsPlayback = true,
        SupportsPTZ = true,
        SupportsAudio = false,
        SupportsEvent = true,
        SupportsDiscovery = true
    };

    public bool TestConnection(Camera camera, CameraCredentials credentials)
    {
        return false;
    }

    public DeviceInformation? GetDeviceInformation(Camera camera, CameraCredentials credentials)
    {
        return null;
    }

    public bool StartLive(Camera camera)
    {
        return false;
    }

    public bool StopLive(Camera camera)
    {
        return false;
    }

    public bool Snapshot(Camera camera)
    {
        return false;
    }
}
