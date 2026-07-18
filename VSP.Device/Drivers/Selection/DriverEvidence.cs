using VSP.Device.Drivers.Capabilities;

namespace VSP.Device.Drivers.Selection;

public sealed class DriverEvidence
{
    public DriverEvidence(
        DriverCompatibilityEvidenceKind kind,
        string? qualifier = null,
        string? displayValue = null)
    {
        if (qualifier is not null && string.IsNullOrWhiteSpace(qualifier))
        {
            throw new ArgumentException("Qualifier must not be empty when provided.", nameof(qualifier));
        }

        Kind = kind;
        Qualifier = qualifier;
        DisplayValue = displayValue;
    }

    public DriverCompatibilityEvidenceKind Kind { get; }

    public string? Qualifier { get; }

    public string? DisplayValue { get; }
}
