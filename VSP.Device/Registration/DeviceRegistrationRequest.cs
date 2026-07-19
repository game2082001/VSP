using VSP.Domain.Entities;

namespace VSP.Device.Registration;

public sealed class DeviceRegistrationRequest
{
    public Camera? Camera { get; init; }

    public RegistrationSource Source { get; init; } = RegistrationSource.Manual;

    public DuplicatePolicy DuplicatePolicy { get; init; } = DuplicatePolicy.Reject;

    public string? CorrelationId { get; init; }
}
