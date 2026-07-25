using VSP.Device.Discovery.Orchestration;

namespace VSP.Device.Discovery.Metrics;

public sealed class DiscoveryMetricsSample
{
    public DiscoveryMetricsSample(
        DiscoveryOrchestrationStatus status,
        TimeSpan duration,
        string? correlationId)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Duration must not be negative.");
        }

        Status = status;
        Duration = duration;
        CorrelationId = correlationId;
    }

    public DiscoveryOrchestrationStatus Status { get; }

    public TimeSpan Duration { get; }

    public string? CorrelationId { get; }
}
