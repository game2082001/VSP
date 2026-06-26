using VSP.Device.Interfaces;
using VSP.Domain.Entities;
using VSP.Domain.Enums;

namespace VSP.Device.Repositories;

public class CameraRepository : ICameraRepository
{
    private readonly List<Camera> _cameras = new();

    public CameraRepository()
    {
        _cameras.Add(new Camera
        {
            Name = "Cam01",
            IpAddress = "192.168.1.101",
            Brand = CameraBrand.Hikvision,
            RtspUrl = "rtsp://192.168.1.101"
        });

        _cameras.Add(new Camera
        {
            Name = "Cam02",
            IpAddress = "192.168.1.102",
            Brand = CameraBrand.Dahua,
            RtspUrl = "rtsp://192.168.1.102"
        });
    }

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
        {
            _cameras[index] = camera;
        }
    }

    public void Delete(Guid id)
    {
        _cameras.RemoveAll(x => x.Id == id);
    }
}