using System.Collections.ObjectModel;
using System.Threading.Tasks;
using VSP.Device.Discovery.AutoDiscovery;

namespace VSP.Device.Discovery.NetworkScan;

public sealed class NetworkScanService : INetworkScanService
{
    private readonly INetworkReachabilityProbe _probe;

    public NetworkScanService(INetworkReachabilityProbe probe)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
    }

    public async Task<IReadOnlyList<NetworkScanResult>> ScanAsync(
        NetworkScanRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveRequest = request ?? new NetworkScanRequest();
        effectiveRequest.Validate();

        if (effectiveRequest.Targets.Count == 0)
        {
            return Array.Empty<NetworkScanResult>();
        }

        var results = new NetworkScanResult?[effectiveRequest.Targets.Count];

        await Parallel.ForEachAsync(
            effectiveRequest.Targets.Select((target, index) => new IndexedTarget(target, index)),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = effectiveRequest.MaxConcurrency,
                CancellationToken = cancellationToken
            },
            async (indexedTarget, batchCancellationToken) =>
            {
                results[indexedTarget.Index] = await ScanTargetAsync(
                    indexedTarget.Target,
                    effectiveRequest.Timeout,
                    batchCancellationToken);
            });

        return new ReadOnlyCollection<NetworkScanResult>(results.Select(static result => result!).ToList());
    }

    private async Task<NetworkScanResult> ScanTargetAsync(
        NetworkScanTarget target,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            var reachability = await _probe.ProbeAsync(target, linkedCts.Token);
            var endedAt = DateTimeOffset.UtcNow;

            return new NetworkScanResult
            {
                Target = target,
                ScanStartedAt = startedAt,
                ScanEndedAt = endedAt,
                ResponseTime = endedAt - startedAt,
                Reachability = reachability
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            var endedAt = DateTimeOffset.UtcNow;

            return new NetworkScanResult
            {
                Target = target,
                ScanStartedAt = startedAt,
                ScanEndedAt = endedAt,
                ResponseTime = endedAt - startedAt,
                Reachability = NetworkReachabilityClassification.TimedOut,
                TimeoutOccurred = true,
                ErrorCategory = nameof(OperationCanceledException),
                ErrorMessage = "The network scan target timed out."
            };
        }
        catch (Exception exception)
        {
            var endedAt = DateTimeOffset.UtcNow;

            return new NetworkScanResult
            {
                Target = target,
                ScanStartedAt = startedAt,
                ScanEndedAt = endedAt,
                ResponseTime = endedAt - startedAt,
                Reachability = NetworkReachabilityClassification.UnknownFailure,
                ErrorCategory = exception.GetType().Name,
                ErrorMessage = exception.Message
            };
        }
    }

    private sealed record IndexedTarget(NetworkScanTarget Target, int Index);
}
