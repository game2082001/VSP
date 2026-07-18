namespace VSP.Device.Drivers.Capabilities;

public sealed class DriverCompatibilityEvidenceRequirement
{
    public DriverCompatibilityEvidenceRequirement(
        DriverCompatibilityEvidenceKind kind,
        string? qualifier = null)
    {
        if (qualifier is not null && string.IsNullOrWhiteSpace(qualifier))
        {
            throw new ArgumentException("Qualifier must not be empty when provided.", nameof(qualifier));
        }

        Kind = kind;
        Qualifier = qualifier;
    }

    public DriverCompatibilityEvidenceKind Kind { get; }

    public string? Qualifier { get; }
}
