namespace VSP.Device.Discovery.Orchestration;

public sealed class DiscoveryOrchestrationResult
{
    public DiscoveryOrchestrationStatus Status { get; init; }

    public IReadOnlyList<CandidateOrchestrationResult> CandidateResults { get; init; } =
        Array.Empty<CandidateOrchestrationResult>();

    public DiscoveryOrchestrationSummary Summary { get; init; } = new();

    public IReadOnlyList<DiscoveryOrchestrationReason> Reasons { get; init; } =
        Array.Empty<DiscoveryOrchestrationReason>();
}
