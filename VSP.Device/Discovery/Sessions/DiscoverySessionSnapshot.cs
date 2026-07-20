using System.Collections.ObjectModel;
using VSP.Device.Discovery.Orchestration;

namespace VSP.Device.Discovery.Sessions;

public sealed class DiscoverySessionSnapshot
{
    public DiscoverySessionSnapshot(
        DiscoveryOrchestrationSummary summary,
        IEnumerable<SessionReason>? reasons = null)
    {
        Summary = summary ?? throw new ArgumentNullException(nameof(summary));

        var copiedReasons = (reasons ?? Array.Empty<SessionReason>()).ToList();
        if (copiedReasons.Any(static reason => reason is null))
        {
            throw new InvalidOperationException("Session snapshot reasons must not contain null entries.");
        }

        Reasons = new ReadOnlyCollection<SessionReason>(copiedReasons);
    }

    public DiscoveryOrchestrationSummary Summary { get; }

    public IReadOnlyList<SessionReason> Reasons { get; }
}
