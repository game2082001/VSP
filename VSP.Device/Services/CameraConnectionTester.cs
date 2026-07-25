using VSP.Device.Drivers;
using VSP.Domain.Entities;

namespace VSP.Device.Services;

public sealed class CameraConnectionTester : ICameraConnectionTester
{
    public CameraConnectionTestResult Test(Camera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        var driver = DriverFactory.CreateCameraDriver(camera.ConnectionType);
        var isSuccess = driver.TestConnection(camera);

        return new CameraConnectionTestResult(camera.Id, isSuccess);
    }
}
