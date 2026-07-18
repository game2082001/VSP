using System.Collections.ObjectModel;

namespace VSP.Device.Discovery.AutoDiscovery;

public sealed class AutoDiscoveryResult
{
    public IReadOnlyList<AutoDiscoveryCandidate> Candidates { get; init; } =
        new ReadOnlyCollection<AutoDiscoveryCandidate>(Array.Empty<AutoDiscoveryCandidate>());

    public IReadOnlyList<string> Messages { get; init; } =
        new ReadOnlyCollection<string>(Array.Empty<string>());
}
