using VSP.Device.Discovery.Orchestration;

namespace VSP.Device.Discovery.Diagnostics;

public sealed class DiscoveryDiagnosticsSnapshot
{
    public DiscoveryDiagnosticsSnapshot(
        Guid diagnosticId,
        DateTimeOffset timestamp,
        string? correlationId,
        DiscoveryOrchestrationStatus status,
        IEnumerable<DiscoveryOrchestrationReason>? reasons = null)
    {
        if (diagnosticId == Guid.Empty)
        {
            throw new ArgumentException("Diagnostic id must not be empty.", nameof(diagnosticId));
        }

        DiagnosticId = diagnosticId;
        Timestamp = timestamp;
        CorrelationId = correlationId;
        Status = status;
        Reasons = (reasons ?? Array.Empty<DiscoveryOrchestrationReason>()).ToArray();
    }

    public Guid DiagnosticId { get; }

    public DateTimeOffset Timestamp { get; }

    public string? CorrelationId { get; }

    public DiscoveryOrchestrationStatus Status { get; }

    public IReadOnlyList<DiscoveryOrchestrationReason> Reasons { get; }
}
