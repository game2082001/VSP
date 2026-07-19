namespace VSP.Device.Discovery.Orchestration;

public sealed class DiscoveryOrchestrationSummary
{
    public int TotalCandidates { get; init; }

    public int Registered { get; init; }

    public int Approved { get; init; }

    public int AwaitingApproval { get; init; }

    public int DuplicateSkipped { get; init; }

    public int Failed { get; init; }

    public int Rejected { get; init; }
}
