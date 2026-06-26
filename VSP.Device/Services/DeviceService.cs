using VSP.Device.Interfaces;
using VSP.Device.Repositories;
using VSP.Domain.Entities;

namespace VSP.Device.Services;

public class DeviceService
{
    private readonly ICameraRepository _cameraRepository;

    public DeviceService()
    {
        _cameraRepository = new CameraRepository();
    }

    public IEnumerable<Camera> GetAllCameras()
    {
        return _cameraRepository.GetAll();
    }

    public void AddCamera(Camera camera)
    {
        _cameraRepository.Add(camera);
    }

    public void UpdateCamera(Camera camera)
    {
        _cameraRepository.Update(camera);
    }

    public void DeleteCamera(Guid id)
    {
        _cameraRepository.Delete(id);
    }

    public Camera? GetCamera(Guid id)
    {
        return _cameraRepository.GetById(id);
    }
}