using VSP.Domain.Entities;

namespace VSP.Device.Services;

public class DeviceService
{
    private readonly List<Camera> _cameras = new();

    public DeviceService()
    {
        _cameras.Add(new Camera
        {
            Name = "Cam01",
            IpAddress = "192.168.1.101",
            Brand = "Hikvision",
            RtspUrl = "rtsp://192.168.1.101"
        });

        _cameras.Add(new Camera
        {
            Name = "Cam02",
            IpAddress = "192.168.1.102",
            Brand = "Dahua",
            RtspUrl = "rtsp://192.168.1.102"
        });
    }

    public IEnumerable<Camera> GetAllCameras()
    {
        return _cameras;
    }

    public void AddCamera(Camera camera)
    {
        _cameras.Add(camera);
    }

    public void DeleteCamera(Guid id)
    {
        _cameras.RemoveAll(x => x.Id == id);
    }
}