using VSP.Device.Discovery.Orchestration;
using VSP.Device.Discovery.Progress;

namespace VSP.Device.Discovery.Execution;

public sealed class ProgressPublishingDiscoveryRunner : IDiscoveryRunner
{
    private readonly IDiscoveryRunner _inner;
    private readonly IDiscoveryProgressPublisher _publisher;

    public ProgressPublishingDiscoveryRunner(IDiscoveryRunner inner)
        : this(inner, NoOpDiscoveryProgressPublisher.Instance)
    {
    }

    public ProgressPublishingDiscoveryRunner(IDiscoveryRunner inner, IDiscoveryProgressPublisher publisher)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    public async Task<DiscoveryOrchestrationResult> ExecuteAsync(
        DiscoveryOrchestrationRequest? request,
        CancellationToken cancellationToken = default)
    {
        _publisher.Publish(new DiscoveryProgress(
            DiscoveryProgressStage.Discovering,
            completedSteps: 0,
            totalSteps: 2,
            new DiscoveryProgressSnapshot(
                DiscoveryProgressStage.Discovering,
                completedSteps: 0,
                totalSteps: 2,
                currentOperation: "Discovery execution started")));

        var result = await _inner.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

        var stage = MapStage(result.Status);
        var reasons = result.Reasons.Select(static reason => new ProgressReason(reason.Code, reason.Message));

        _publisher.Publish(new DiscoveryProgress(
            stage,
            completedSteps: 2,
            totalSteps: 2,
            new DiscoveryProgressSnapshot(
                stage,
                completedSteps: 2,
                totalSteps: 2,
                currentOperation: "Discovery execution finished",
                reasons: reasons)));

        return result;
    }

    private static DiscoveryProgressStage MapStage(DiscoveryOrchestrationStatus status)
    {
        return status switch
        {
            DiscoveryOrchestrationStatus.Completed => DiscoveryProgressStage.Completed,
            DiscoveryOrchestrationStatus.CompletedWithErrors => DiscoveryProgressStage.Completed,
            DiscoveryOrchestrationStatus.Cancelled => DiscoveryProgressStage.Cancelled,
            DiscoveryOrchestrationStatus.Failed => DiscoveryProgressStage.Failed,
            DiscoveryOrchestrationStatus.InvalidRequest => DiscoveryProgressStage.Failed,
            _ => DiscoveryProgressStage.Failed
        };
    }
}
