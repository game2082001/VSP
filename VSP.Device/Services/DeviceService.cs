using VSP.Domain.Entities;
using VSP.Infrastructure.Database;
using VSP.Infrastructure.Repositories;

namespace VSP.Device.Services;

public class DeviceService
{
    private readonly SQLiteCameraRepository _cameraRepository;

    public DeviceService()
    {
        var databaseService = new DatabaseService();
        _cameraRepository = new SQLiteCameraRepository(databaseService);
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
}