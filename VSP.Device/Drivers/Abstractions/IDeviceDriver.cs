using VSP.Domain.Entities;

namespace VSP.Device.Drivers.Abstractions;

public interface IDeviceDriver
{
    string DriverId { get; }

    string DisplayName { get; }

    DeviceCapability Capability { get; }

    bool TestConnection(Camera camera);
}