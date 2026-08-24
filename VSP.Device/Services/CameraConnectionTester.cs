using VSP.Device.Drivers;
using VSP.Device.Interfaces;
using VSP.Device.Repositories;
using VSP.Domain.Entities;

namespace VSP.Device.Services;

public sealed class CameraConnectionTester : ICameraConnectionTester
{
    private readonly ICameraRepository _cameraRepository;

    public CameraConnectionTester()
        : this(new CameraRepository())
    {
    }

    public CameraConnectionTester(ICameraRepository cameraRepository)
    {
        _cameraRepository = cameraRepository;
    }

    public CameraConnectionTestResult Test(Camera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        var driver = DriverFactory.CreateCameraDriver(camera.ConnectionType);
        var isSuccess = driver.TestConnection(camera, _cameraRepository.GetCredentials(camera.Id));

        return new CameraConnectionTestResult(camera.Id, isSuccess);
    }
}
