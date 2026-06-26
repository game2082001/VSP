using VSP.Domain.Enums;

namespace VSP.Domain.Entities;

public class Camera
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = "";

    public string IpAddress { get; set; } = "";

    public CameraBrand Brand { get; set; } = "";

    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    public int HttpPort { get; set; } = 80;

    public int RtspPort { get; set; } = 554;

    public string RtspUrl { get; set; } = "";

    public CameraStatus Status { get; set; }

    public bool Recording { get; set; }

    public DateTime CreateTime { get; set; } = DateTime.Now;
}