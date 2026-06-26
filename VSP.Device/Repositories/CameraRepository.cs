using VSP.Device.Interfaces;
using VSP.Domain.Entities;

namespace VSP.Device.Repositories;

public class CameraRepository : ICameraRepository
{
    private readonly List<Camera> _cameras = new();

    public IEnumerable<Camera> GetAll()
    {
        return _cameras;
    }

    public Camera? GetById(Guid id)
    {
        return _cameras.FirstOrDefault(x => x.Id == id);
    }

    public void Add(Camera camera)
    {
        _cameras.Add(camera);
    }

    public void Update(Camera camera)
    {
        var index = _cameras.FindIndex(x => x.Id == camera.Id);
        if (index >= 0)
            _cameras[index] = camera;
    }

    public void Delete(Guid id)
    {
        _cameras.RemoveAll(x => x.Id == id);
    }
}