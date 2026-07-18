using VSP.Device.Drivers.Capabilities;

namespace VSP.Device.Drivers.Selection;

public sealed class DriverEvidenceEvaluation
{
    public DriverEvidenceEvaluation(
        DriverCompatibilityEvidenceKind kind,
        DriverEvidenceEvaluationStatus status,
        DriverSelectionReason reason,
        string? qualifier = null)
    {
        ArgumentNullException.ThrowIfNull(reason);

        if (qualifier is not null && string.IsNullOrWhiteSpace(qualifier))
        {
            throw new ArgumentException("Qualifier must not be empty when provided.", nameof(qualifier));
        }

        Kind = kind;
        Qualifier = qualifier;
        Status = status;
        Reason = reason;
    }

    public DriverCompatibilityEvidenceKind Kind { get; }

    public string? Qualifier { get; }

    public DriverEvidenceEvaluationStatus Status { get; }

    public DriverSelectionReason Reason { get; }
}
