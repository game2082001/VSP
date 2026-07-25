using VSP.Device.Discovery.Orchestration;

namespace VSP.Device.Discovery.Execution;

public interface IDiscoveryRunner
{
    Task<DiscoveryOrchestrationResult> ExecuteAsync(
        DiscoveryOrchestrationRequest? request,
        CancellationToken cancellationToken = default);
}
