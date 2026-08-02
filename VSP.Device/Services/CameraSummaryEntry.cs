namespace VSP.Device.Services;

public sealed class CameraSummaryEntry
{
    public Guid CameraId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string IpAddress { get; init; } = string.Empty;

    public DateTime Timestamp { get; init; }
}
