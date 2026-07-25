using VSP.Device.Discovery.Diagnostics;
using VSP.Device.Discovery.Orchestration;

namespace VSP.Device.Discovery.Execution;

public sealed class DiagnosticsRecordingDiscoveryRunner : IDiscoveryRunner
{
    private readonly IDiscoveryRunner _inner;
    private readonly IDiscoveryDiagnosticsSink _sink;

    public DiagnosticsRecordingDiscoveryRunner(IDiscoveryRunner inner)
        : this(inner, NoOpDiscoveryDiagnosticsSink.Instance)
    {
    }

    public DiagnosticsRecordingDiscoveryRunner(IDiscoveryRunner inner, IDiscoveryDiagnosticsSink sink)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
    }

    public async Task<DiscoveryOrchestrationResult> ExecuteAsync(
        DiscoveryOrchestrationRequest? request,
        CancellationToken cancellationToken = default)
    {
        var result = await _inner.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

        _sink.Publish(new DiscoveryDiagnosticsSnapshot(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            request?.CorrelationId,
            result.Status,
            result.Reasons));

        return result;
    }
}
