using VSP.Device.Interfaces;
using VSP.Domain.Entities;

namespace VSP.Device.Services;

public class DeviceService
{
    private readonly ICameraRepository _repository;

    public DeviceService(ICameraRepository repository)
    {
        _repository = repository;
    }

    public IEnumerable<Camera> GetAllCamera()
    {
        return _repository.GetAll();
    }

    public void AddCamera(Camera camera)
    {
        _repository.Add(camera);
    }

    public void DeleteCamera(Guid id)
    {
        _repository.Delete(id);
    }
}