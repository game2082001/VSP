using VSP.Device.Drivers.Settings;

namespace VSP.Device.Drivers.Capabilities;

public sealed class DriverCompatibilityCapability
{
    public static readonly DriverCompatibilityCapability Empty = new();

    public DriverCompatibilityCapability(
        IEnumerable<DriverCompatibilityEvidenceRequirement>? requiredEvidence = null,
        IEnumerable<DriverCompatibilityEvidenceRequirement>? optionalEvidence = null,
        IEnumerable<DriverCompatibilityEvidenceRequirement>? unsupportedEvidence = null,
        IEnumerable<DriverSettingKey>? requiredSettings = null)
    {
        RequiredEvidence = CopyRequirements(requiredEvidence, nameof(requiredEvidence));
        OptionalEvidence = CopyRequirements(optionalEvidence, nameof(optionalEvidence));
        UnsupportedEvidence = CopyRequirements(unsupportedEvidence, nameof(unsupportedEvidence));
        RequiredSettings = (requiredSettings ?? Array.Empty<DriverSettingKey>()).ToArray().AsReadOnly();
    }

    public IReadOnlyList<DriverCompatibilityEvidenceRequirement> RequiredEvidence { get; }

    public IReadOnlyList<DriverCompatibilityEvidenceRequirement> OptionalEvidence { get; }

    public IReadOnlyList<DriverCompatibilityEvidenceRequirement> UnsupportedEvidence { get; }

    public IReadOnlyList<DriverSettingKey> RequiredSettings { get; }

    private static IReadOnlyList<DriverCompatibilityEvidenceRequirement> CopyRequirements(
        IEnumerable<DriverCompatibilityEvidenceRequirement>? requirements,
        string parameterName)
    {
        var copiedRequirements = (requirements ?? Array.Empty<DriverCompatibilityEvidenceRequirement>()).ToList();
        if (copiedRequirements.Any(static requirement => requirement is null))
        {
            throw new InvalidOperationException($"{parameterName} must not contain null entries.");
        }

        return copiedRequirements.AsReadOnly();
    }
}
