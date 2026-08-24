using VSP.Domain.Entities;

namespace VSP.Device.Interfaces;

public interface ICameraRepository
{
    IEnumerable<Camera> GetAll();

    Camera? GetById(Guid id);

    CameraCredentials GetCredentials(Guid id) => new("", "");

    void Add(Camera camera);

    void Add(Camera camera, CameraCredentialMutation credentialMutation) => Add(camera);

    void Update(Camera camera);

    void Update(Camera camera, CameraCredentialMutation credentialMutation) => Update(camera);

    void Delete(Guid id);
}
