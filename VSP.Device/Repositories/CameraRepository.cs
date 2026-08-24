using VSP.Device.Interfaces;
using VSP.Domain.Entities;
using VSP.Infrastructure.Database;
using VSP.Infrastructure.Repositories;

namespace VSP.Device.Repositories;

public class CameraRepository : ICameraRepository
{
    private readonly SQLiteCameraRepository _cameraRepository;

    public CameraRepository()
        : this(new SQLiteCameraRepository(new DatabaseService()))
    {
    }

    public CameraRepository(SQLiteCameraRepository cameraRepository)
    {
        _cameraRepository = cameraRepository;
    }

    public IEnumerable<Camera> GetAll()
    {
        return _cameraRepository.GetAll();
    }

    public Camera? GetById(Guid id)
    {
        return _cameraRepository.GetAll().FirstOrDefault(x => x.Id == id);
    }

    public CameraCredentials GetCredentials(Guid id)
    {
        return _cameraRepository.GetCredentials(id);
    }

    public void Add(Camera camera)
    {
        _cameraRepository.Add(camera);
    }

    public void Add(Camera camera, CameraCredentialMutation credentialMutation)
    {
        _cameraRepository.Add(camera, credentialMutation);
    }

    public void Update(Camera camera)
    {
        _cameraRepository.Update(camera);
    }

    public void Update(Camera camera, CameraCredentialMutation credentialMutation)
    {
        _cameraRepository.Update(camera, credentialMutation);
    }

    public void Delete(Guid id)
    {
        _cameraRepository.Delete(id);
    }
}
