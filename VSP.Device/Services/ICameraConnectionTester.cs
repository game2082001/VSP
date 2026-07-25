using VSP.Domain.Entities;

namespace VSP.Device.Services;

public interface ICameraConnectionTester
{
    CameraConnectionTestResult Test(Camera camera);
}
