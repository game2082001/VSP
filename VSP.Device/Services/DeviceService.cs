using VSP.Device.Interfaces;
using VSP.Domain.Entities;

namespace VSP.Device.Services;

public class DeviceService
{
    private readonly ICameraRepository _cameraRepository;

    public DeviceService(ICameraRepository cameraRepository)
    {
        _cameraRepository = cameraRepository;
    }

    public IEnumerable<Camera> GetAllCameras()
    {
        return _cameraRepository.GetAll();
    }

    public void AddCamera(Camera camera)
    {
        _cameraRepository.Add(camera);
    }

    public void DeleteCamera(Guid id)
    {
        _cameraRepository.Delete(id);
    }
}