using VSP.Device.Discovery.AutoDiscovery;
using VSP.Device.Registration;

namespace VSP.Device.Discovery.Orchestration;

public sealed class DiscoveryOrchestrationRequest
{
    public AutoDiscoveryRequest? DiscoveryRequest { get; init; }

    public IReadOnlyList<AutoDiscoveryCandidate>? Candidates { get; init; }

    public DuplicatePolicy DuplicatePolicy { get; init; } = DuplicatePolicy.Reject;

    public RegistrationSource RegistrationSource { get; init; } = RegistrationSource.Discovery;

    public string? CorrelationId { get; init; }

    public DateTime? Timestamp { get; init; }
}
