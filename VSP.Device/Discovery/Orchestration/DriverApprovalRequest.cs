using VSP.Device.Discovery.AutoDiscovery;
using VSP.Device.Drivers.Selection;

namespace VSP.Device.Discovery.Orchestration;

public sealed class DriverApprovalRequest
{
    public AutoDiscoveryCandidate? Candidate { get; init; }

    public DriverSelectionResult? DriverSelectionResult { get; init; }

    public IReadOnlyList<DriverCompatibilityResult> CompatibleDrivers { get; init; } =
        Array.Empty<DriverCompatibilityResult>();

    public string? CorrelationId { get; init; }
}
