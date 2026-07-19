using VSP.Device.Cameras.Factory;
using VSP.Device.Discovery.AutoDiscovery;
using VSP.Device.Drivers.Selection;
using VSP.Device.Registration;

namespace VSP.Device.Discovery.Orchestration;

public sealed class CandidateOrchestrationResult
{
    public AutoDiscoveryCandidate? Candidate { get; init; }

    public CandidateOrchestrationStatus Status { get; init; }

    public DriverSelectionResult? DriverSelectionResult { get; init; }

    public DriverApprovalResult? DriverApprovalResult { get; init; }

    public CameraFactoryResult? CameraFactoryResult { get; init; }

    public DeviceRegistrationResult? RegistrationResult { get; init; }

    public IReadOnlyList<DiscoveryOrchestrationReason> Reasons { get; init; } =
        Array.Empty<DiscoveryOrchestrationReason>();
}
