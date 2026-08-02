using VSP.Core.Logging;
using VSP.Device.Discovery.Orchestration;

namespace VSP.Device.Discovery.Execution;

public sealed class RetryingDiscoveryRunner : IDiscoveryRunner
{
    private readonly IDiscoveryRunner _inner;
    private readonly DiscoveryRetryPolicy _policy;

    public RetryingDiscoveryRunner(IDiscoveryRunner inner)
        : this(inner, DiscoveryRetryPolicy.Default)
    {
    }

    public RetryingDiscoveryRunner(IDiscoveryRunner inner, DiscoveryRetryPolicy policy)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public async Task<DiscoveryOrchestrationResult> ExecuteAsync(
        DiscoveryOrchestrationRequest? request,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            var isLastAttempt = attempt >= _policy.MaxAttempts;

            DiscoveryOrchestrationResult result;
            try
            {
                result = await _inner.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (!isLastAttempt)
            {
                AppLog.Warning($"Discovery attempt {attempt}/{_policy.MaxAttempts} failed; retrying.", ex);
                await Task.Delay(_policy.Delay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (result.Status != DiscoveryOrchestrationStatus.Failed || isLastAttempt)
            {
                return result;
            }

            await Task.Delay(_policy.Delay, cancellationToken).ConfigureAwait(false);
        }
    }
}
