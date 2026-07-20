using VSP.Device.Discovery.Orchestration;

namespace VSP.Device.Discovery.Sessions;

public sealed class DiscoverySessionFactoryRequest
{
    public DiscoveryOrchestrationResult? DiscoveryOrchestrationResult { get; init; }

    public DiscoverySessionId? SessionId { get; init; }

    public string? CorrelationId { get; init; }

    public DateTimeOffset? StartTime { get; init; }

    public DateTimeOffset? EndTime { get; init; }
}
