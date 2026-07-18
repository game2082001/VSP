using VSP.Device.Discovery.AutoDiscovery;
using VSP.Device.Drivers.Capabilities;

namespace VSP.Device.Drivers.Selection;

public sealed class AutoDiscoveryCandidateEvidenceMapper
{
    public DriverEvidenceCollection Map(AutoDiscoveryCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        var evidence = new List<DriverEvidence>();

        AddIfPresent(evidence, DriverCompatibilityEvidenceKind.Host, candidate.Host);
        AddIfPresent(evidence, DriverCompatibilityEvidenceKind.Endpoint, candidate.Endpoint);
        AddIfPresent(evidence, DriverCompatibilityEvidenceKind.ManufacturerHint, candidate.Manufacturer);
        AddIfPresent(evidence, DriverCompatibilityEvidenceKind.ModelHint, candidate.Model);

        if (candidate.Port.HasValue)
        {
            evidence.Add(new DriverEvidence(
                DriverCompatibilityEvidenceKind.Port,
                displayValue: candidate.Port.Value.ToString()));
        }

        if (candidate.Sources.Contains(AutoDiscoverySource.ONVIF)
            && HasOnvifSummary(candidate))
        {
            evidence.Add(new DriverEvidence(DriverCompatibilityEvidenceKind.OnvifDiscovery));
        }

        if (candidate.Sources.Contains(AutoDiscoverySource.RTSP)
            && !string.IsNullOrWhiteSpace(candidate.ProbeClassification))
        {
            evidence.Add(new DriverEvidence(DriverCompatibilityEvidenceKind.RtspEndpointProbe));
        }

        return new DriverEvidenceCollection(evidence);
    }

    private static void AddIfPresent(
        ICollection<DriverEvidence> evidence,
        DriverCompatibilityEvidenceKind kind,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            evidence.Add(new DriverEvidence(kind, displayValue: value));
        }
    }

    private static bool HasOnvifSummary(AutoDiscoveryCandidate candidate)
    {
        return !string.IsNullOrWhiteSpace(candidate.Endpoint)
            || !string.IsNullOrWhiteSpace(candidate.Manufacturer)
            || !string.IsNullOrWhiteSpace(candidate.Model)
            || !string.IsNullOrWhiteSpace(candidate.Name);
    }
}
