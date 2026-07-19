using VSP.Domain.Enums;

namespace VSP.Device.Cameras.Factory;

public sealed class CameraInitializationData
{
    public string? Name { get; init; }

    public string? IpAddress { get; init; }

    public CameraBrand? Brand { get; init; }

    public string? Model { get; init; }

    public string? Location { get; init; }

    public int? HttpPort { get; init; }

    public int? RtspPort { get; init; }

    public int? SdkPort { get; init; }

    public string? RtspUrl { get; init; }

    public IReadOnlyCollection<string> UnsupportedDataKeys { get; init; } = Array.Empty<string>();
}
