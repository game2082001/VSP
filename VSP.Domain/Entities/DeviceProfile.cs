using VSP.Domain.Enums;

namespace VSP.Domain.Entities;

public class DeviceProfile
{
    public string Name { get; set; } = "";

    public CameraBrand Brand { get; set; } = CameraBrand.Unknown;

    public DeviceConnectionType ConnectionType { get; set; } = DeviceConnectionType.Unknown;

    public bool ShowHttpPort { get; set; }

    public bool ShowRtspPort { get; set; }

    public bool ShowSdkPort { get; set; }

    public bool ShowRtspUrl { get; set; }

    public bool ShowUsername { get; set; } = true;

    public bool ShowPassword { get; set; } = true;

    public DeviceCapability Capability { get; set; } = new();
}