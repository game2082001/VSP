namespace VSP.Device.Registration;

public sealed class DeviceRegistrationResult
{
    public DeviceRegistrationStatus Status { get; init; }

    public Guid? RegisteredCameraId { get; init; }

    public IReadOnlyList<DeviceDuplicateCheckResult> DuplicateResults { get; init; } =
        Array.Empty<DeviceDuplicateCheckResult>();

    public IReadOnlyList<DeviceRegistrationReason> Reasons { get; init; } =
        Array.Empty<DeviceRegistrationReason>();
}
