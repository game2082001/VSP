using VSP.Device.Drivers.Abstractions;
using VSP.Domain.Entities;

namespace VSP.Device.Drivers.ONVIF;

public class OnvifCameraDriver : ICameraDriver
{
    public string DriverId => "generic.onvif";

    public string DisplayName => "Generic ONVIF Driver";

    public DeviceCapability Capability { get; } = new()
    {
        SupportsLiveView = true,
        SupportsSnapshot = true,
        SupportsPlayback = false,
        SupportsPTZ = true,
        SupportsAudio = false,
        SupportsEvent = false,
        SupportsDiscovery = true
    };

    public bool TestConnection(Camera camera)
    {
        return false;
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