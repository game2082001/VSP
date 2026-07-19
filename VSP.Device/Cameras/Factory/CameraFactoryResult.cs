using VSP.Domain.Entities;

namespace VSP.Device.Cameras.Factory;

public sealed class CameraFactoryResult
{
    public CameraFactoryStatus Status { get; init; }

    public Camera? Camera { get; init; }

    public IReadOnlyList<CameraFactoryReason> Reasons { get; init; } = Array.Empty<CameraFactoryReason>();
}
