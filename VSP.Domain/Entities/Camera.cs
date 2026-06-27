using VSP.Domain.Enums;

namespace VSP.Domain.Entities;

public class Camera
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";

    public string IpAddress { get; set; } = "";

    public CameraBrand Brand { get; set; } = CameraBrand.Unknown;

    public DeviceConnectionType ConnectionType { get; set; } = DeviceConnectionType.Unknown;

    public string Model { get; set; } = "";

    public string Location { get; set; } = "";

    public int HttpPort { get; set; } = 80;

    public int RtspPort { get; set; } = 554;

    public int SdkPort { get; set; } = 8000;

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    public string RtspUrl { get; set; } = "";

    public CameraStatus Status { get; set; } = CameraStatus.Offline;

    public bool Recording { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.Now;

    public DateTime LastModifyTime { get; set; } = DateTime.Now;
}