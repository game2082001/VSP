using VSP.Domain.Enums;

namespace VSP.Device.Cameras.Factory;

public sealed class ApprovedDriverReference
{
    public string DriverId { get; init; } = string.Empty;

    public DeviceConnectionType ConnectionType { get; init; } = DeviceConnectionType.Unknown;

    public bool IsApproved { get; init; }

    public string? DriverDisplayName { get; init; }
}
