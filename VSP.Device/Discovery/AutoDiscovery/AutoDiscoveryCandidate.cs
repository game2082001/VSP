using System.Collections.ObjectModel;

namespace VSP.Device.Discovery.AutoDiscovery;

public sealed class AutoDiscoveryCandidate
{
    public string CandidateKey { get; init; } = string.Empty;

    public IReadOnlyCollection<AutoDiscoverySource> Sources { get; init; } =
        new ReadOnlyCollection<AutoDiscoverySource>(Array.Empty<AutoDiscoverySource>());

    public string? Host { get; init; }

    public int? Port { get; init; }

    public string? Endpoint { get; init; }

    public string? Name { get; init; }

    public string? Location { get; init; }

    public string? Model { get; init; }

    public string? Manufacturer { get; init; }

    public string? Reachability { get; init; }

    public string? ProbeClassification { get; init; }

    public IReadOnlyCollection<string> Warnings { get; init; } =
        new ReadOnlyCollection<string>(Array.Empty<string>());
}
