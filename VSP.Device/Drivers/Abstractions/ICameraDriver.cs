using VSP.Domain.Entities;

namespace VSP.Device.Drivers.Abstractions;

public interface ICameraDriver : IDeviceDriver
{
    bool StartLive(Camera camera);

    bool StopLive(Camera camera);

    bool Snapshot(Camera camera);
}