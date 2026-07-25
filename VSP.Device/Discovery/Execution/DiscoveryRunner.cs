using VSP.Device.Discovery.Orchestration;

namespace VSP.Device.Discovery.Execution;

public sealed class DiscoveryRunner : IDiscoveryRunner
{
    private readonly DiscoveryOrchestrator _orchestrator;

    public DiscoveryRunner(DiscoveryOrchestrator orchestrator)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    public async Task<DiscoveryOrchestrationResult> ExecuteAsync(
        DiscoveryOrchestrationRequest? request,
        CancellationToken cancellationToken = default)
    {
        var executionContext = new DiscoveryExecutionContext(
            DiscoveryExecutionId.New(),
            request?.CorrelationId,
            cancellationToken);

        return await _orchestrator
            .ExecuteAsync(request, executionContext.CancellationToken)
            .ConfigureAwait(false);
    }
}
