using VSP.Device.Drivers.Abstractions;
using VSP.Domain.Enums;

namespace VSP.Device.Drivers;

public sealed class DriverDescriptor
{
    public DriverDescriptor(
        string driverId,
        string displayName,
        DeviceConnectionType connectionType,
        Func<ICameraDriver> factory)
    {
        if (string.IsNullOrWhiteSpace(driverId))
        {
            throw new ArgumentException("DriverId must not be empty.", nameof(driverId));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("DisplayName must not be empty.", nameof(displayName));
        }

        ArgumentNullException.ThrowIfNull(factory);

        DriverId = driverId;
        DisplayName = displayName;
        ConnectionType = connectionType;
        Factory = factory;
    }

    public string DriverId { get; }

    public string DisplayName { get; }

    public DeviceConnectionType ConnectionType { get; }

    public Func<ICameraDriver> Factory { get; }
}
