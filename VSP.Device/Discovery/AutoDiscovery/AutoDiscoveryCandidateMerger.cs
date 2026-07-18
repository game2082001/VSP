using System.Collections.ObjectModel;

namespace VSP.Device.Discovery.AutoDiscovery;

public sealed class AutoDiscoveryCandidateMerger
{
    public IReadOnlyList<AutoDiscoveryCandidate> Merge(IEnumerable<AutoDiscoveryCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var mergedCandidates = new Dictionary<string, AutoDiscoveryCandidate>(StringComparer.Ordinal);

        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.CandidateKey))
            {
                continue;
            }

            if (!mergedCandidates.TryGetValue(candidate.CandidateKey, out var existingCandidate))
            {
                mergedCandidates[candidate.CandidateKey] = Normalize(candidate);
                continue;
            }

            mergedCandidates[candidate.CandidateKey] = Merge(existingCandidate, candidate);
        }

        return new ReadOnlyCollection<AutoDiscoveryCandidate>(
            mergedCandidates.Values
                .OrderBy(candidate => candidate.CandidateKey, StringComparer.Ordinal)
                .ToArray());
    }

    private static AutoDiscoveryCandidate Merge(
        AutoDiscoveryCandidate first,
        AutoDiscoveryCandidate second)
    {
        return new AutoDiscoveryCandidate
        {
            CandidateKey = first.CandidateKey,
            Sources = MergeSources(first.Sources, second.Sources),
            Host = FirstNonEmpty(first.Host, second.Host),
            Port = first.Port ?? second.Port,
            Endpoint = FirstNonEmpty(first.Endpoint, second.Endpoint),
            Name = FirstNonEmpty(first.Name, second.Name),
            Location = FirstNonEmpty(first.Location, second.Location),
            Model = FirstNonEmpty(first.Model, second.Model),
            Manufacturer = FirstNonEmpty(first.Manufacturer, second.Manufacturer),
            Reachability = FirstNonEmpty(first.Reachability, second.Reachability),
            ProbeClassification = FirstNonEmpty(first.ProbeClassification, second.ProbeClassification),
            Warnings = MergeWarnings(first.Warnings, second.Warnings)
        };
    }

    private static AutoDiscoveryCandidate Normalize(AutoDiscoveryCandidate candidate)
    {
        return new AutoDiscoveryCandidate
        {
            CandidateKey = candidate.CandidateKey,
            Sources = MergeSources(candidate.Sources),
            Host = candidate.Host,
            Port = candidate.Port,
            Endpoint = candidate.Endpoint,
            Name = candidate.Name,
            Location = candidate.Location,
            Model = candidate.Model,
            Manufacturer = candidate.Manufacturer,
            Reachability = candidate.Reachability,
            ProbeClassification = candidate.ProbeClassification,
            Warnings = MergeWarnings(candidate.Warnings)
        };
    }

    private static IReadOnlyCollection<AutoDiscoverySource> MergeSources(
        params IEnumerable<AutoDiscoverySource>[] sources)
    {
        return new ReadOnlyCollection<AutoDiscoverySource>(
            sources
                .SelectMany(source => source)
                .Distinct()
                .Order()
                .ToArray());
    }

    private static IReadOnlyCollection<string> MergeWarnings(params IEnumerable<string>[] warnings)
    {
        return new ReadOnlyCollection<string>(
            warnings
                .SelectMany(warning => warning)
                .Where(warning => !string.IsNullOrWhiteSpace(warning))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(warning => warning, StringComparer.Ordinal)
                .ToArray());
    }

    private static string? FirstNonEmpty(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first;
        }

        return string.IsNullOrWhiteSpace(second) ? null : second;
    }
}
