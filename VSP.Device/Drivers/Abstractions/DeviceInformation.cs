namespace VSP.Device.Drivers.Abstractions;

public sealed class DeviceInformation
{
    public string? Manufacturer { get; init; }

    public string? Model { get; init; }

    public string? FirmwareVersion { get; init; }

    public string? SerialNumber { get; init; }
}
