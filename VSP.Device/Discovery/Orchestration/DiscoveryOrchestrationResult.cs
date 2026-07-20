namespace VSP.Device.Discovery.Orchestration;

public sealed class DiscoveryOrchestrationResult
{
    private IReadOnlyList<CandidateOrchestrationResult> _candidateResults =
        Array.Empty<CandidateOrchestrationResult>();

    private DiscoveryOrchestrationSummary _summary = new();

    private IReadOnlyList<DiscoveryOrchestrationReason> _reasons =
        Array.Empty<DiscoveryOrchestrationReason>();

    public DiscoveryOrchestrationStatus Status { get; init; }

    public IReadOnlyList<CandidateOrchestrationResult> CandidateResults
    {
        get => _candidateResults;
        init => _candidateResults = value?.ToArray() ?? Array.Empty<CandidateOrchestrationResult>();
    }

    public DiscoveryOrchestrationSummary Summary
    {
        get => _summary;
        init => _summary = value ?? new DiscoveryOrchestrationSummary();
    }

    public IReadOnlyList<DiscoveryOrchestrationReason> Reasons
    {
        get => _reasons;
        init => _reasons = value?.ToArray() ?? Array.Empty<DiscoveryOrchestrationReason>();
    }
}
