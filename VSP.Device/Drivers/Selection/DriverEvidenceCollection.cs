using VSP.Device.Drivers.Capabilities;

namespace VSP.Device.Drivers.Selection;

public sealed class DriverEvidenceCollection
{
    private readonly IReadOnlyList<DriverEvidence> _evidence;

    public DriverEvidenceCollection(IEnumerable<DriverEvidence>? evidence = null)
    {
        var copiedEvidence = (evidence ?? Array.Empty<DriverEvidence>()).ToList();
        if (copiedEvidence.Any(static item => item is null))
        {
            throw new InvalidOperationException("Driver evidence collection must not contain null entries.");
        }

        _evidence = copiedEvidence.AsReadOnly();
    }

    public IReadOnlyList<DriverEvidence> Evidence => _evidence;

    public bool Contains(DriverCompatibilityEvidenceRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        return _evidence.Any(evidence =>
            evidence.Kind == requirement.Kind
            && QualifierMatches(requirement.Qualifier, evidence.Qualifier));
    }

    public bool ContainsKind(DriverCompatibilityEvidenceKind kind)
    {
        return _evidence.Any(evidence => evidence.Kind == kind);
    }

    private static bool QualifierMatches(string? requirementQualifier, string? evidenceQualifier)
    {
        return requirementQualifier is null
            || string.Equals(requirementQualifier, evidenceQualifier, StringComparison.OrdinalIgnoreCase);
    }
}
