using VSP.Device.Drivers.Abstractions;
using VSP.Domain.Entities;

namespace VSP.Device.Drivers.RTSP;

public class RtspCameraDriver : ICameraDriver
{
    public string DriverId => "generic.rtsp";

    public string DisplayName => "Generic RTSP Driver";

    public DeviceCapability Capability { get; } = new()
    {
        SupportsLiveView = true,
        SupportsSnapshot = true,
        SupportsPlayback = false,
        SupportsPTZ = false,
        SupportsAudio = false,
        SupportsEvent = false,
        SupportsDiscovery = false
    };

    public bool TestConnection(Camera camera)
    {
        // TODO: Milestone 3.7 Connection Test
        return false;
    }

    public bool StartLive(Camera camera)
    {
        // TODO: Milestone 4 Live View
        return false;
    }

    public bool StopLive(Camera camera)
    {
        // TODO: Milestone 4 Live View
        return false;
    }

    public bool Snapshot(Camera camera)
    {
        // TODO: Milestone 4 Snapshot
        return false;
    }
}