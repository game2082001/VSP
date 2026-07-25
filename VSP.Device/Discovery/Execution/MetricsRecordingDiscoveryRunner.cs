using System.Diagnostics;
using VSP.Device.Discovery.Metrics;
using VSP.Device.Discovery.Orchestration;

namespace VSP.Device.Discovery.Execution;

public sealed class MetricsRecordingDiscoveryRunner : IDiscoveryRunner
{
    private readonly IDiscoveryRunner _inner;
    private readonly IDiscoveryMetricsSink _sink;

    public MetricsRecordingDiscoveryRunner(IDiscoveryRunner inner)
        : this(inner, NoOpDiscoveryMetricsSink.Instance)
    {
    }

    public MetricsRecordingDiscoveryRunner(IDiscoveryRunner inner, IDiscoveryMetricsSink sink)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    public async Task<DiscoveryOrchestrationResult> ExecuteAsync(
        DiscoveryOrchestrationRequest? request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var result = await _inner.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();

        _sink.Record(new DiscoveryMetricsSample(result.Status, stopwatch.Elapsed, request?.CorrelationId));

        return result;
    }
}
