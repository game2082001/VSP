using VSP.Device.Discovery.Orchestration;

namespace VSP.Device.Discovery.Execution;

public sealed class TimeoutDiscoveryRunner : IDiscoveryRunner
{
    private readonly IDiscoveryRunner _inner;
    private readonly DiscoveryTimeoutPolicy _policy;

    public TimeoutDiscoveryRunner(IDiscoveryRunner inner)
        : this(inner, DiscoveryTimeoutPolicy.Default)
    {
    }

    public TimeoutDiscoveryRunner(IDiscoveryRunner inner, DiscoveryTimeoutPolicy policy)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public async Task<DiscoveryOrchestrationResult> ExecuteAsync(
        DiscoveryOrchestrationRequest? request,
        CancellationToken cancellationToken = default)
    {
        using var timeoutCts = new CancellationTokenSource(_policy.Timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            return await _inner.ExecuteAsync(request, linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            if (timeoutCts.IsCancellationRequested)
            {
                throw new DiscoveryTimeoutException(_policy.Timeout);
            }

            throw;
        }
    }
}
