using VSP.Device.Cameras.Factory;
using VSP.Device.Drivers.Selection;

namespace VSP.Device.Discovery.Orchestration;

public sealed class DriverApprovalResult
{
    public DriverApprovalStatus Status { get; init; }

    public ApprovedDriverReference? ApprovedDriver { get; init; }

    public IReadOnlyList<DriverCompatibilityResult> CompatibleDrivers { get; init; } =
        Array.Empty<DriverCompatibilityResult>();

    public IReadOnlyList<DiscoveryOrchestrationReason> Reasons { get; init; } =
        Array.Empty<DiscoveryOrchestrationReason>();
}
