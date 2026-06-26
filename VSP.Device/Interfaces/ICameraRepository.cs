using VSP.Domain.Entities;

namespace VSP.Device.Interfaces;

public interface ICameraRepository
{
    IEnumerable<Camera> GetAll();
    Camera? GetById(Guid id);
    void Add(Camera camera);
    void Update(Camera camera);
    void Delete(Guid id);
}