using VSP.Device.Interfaces;
using VSP.Domain.Entities;

namespace VSP.Device.Services;

public class CameraQueryService
{
    private readonly ICameraRepository _cameraRepository;

    public CameraQueryService(ICameraRepository cameraRepository)
    {
        _cameraRepository = cameraRepository;
    }

    public Task<IReadOnlyList<Camera>> GetAllAsync()
    {
        var cameras = _cameraRepository.GetAll().ToList();
        return Task.FromResult<IReadOnlyList<Camera>>(cameras);
    }

    public Task<Camera?> GetByIdAsync(Guid id)
    {
        return Task.FromResult(_cameraRepository.GetById(id));
    }
}
