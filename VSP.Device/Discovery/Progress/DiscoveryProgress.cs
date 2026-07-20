namespace VSP.Device.Discovery.Progress;

public sealed class DiscoveryProgress
{
    public DiscoveryProgress(
        DiscoveryProgressStage stage,
        int completedSteps,
        int? totalSteps,
        DiscoveryProgressSnapshot snapshot)
    {
        DiscoveryProgressSnapshot.ValidateStage(stage);
        DiscoveryProgressSnapshot.ValidateSteps(completedSteps, totalSteps);

        Stage = stage;
        CompletedSteps = completedSteps;
        TotalSteps = totalSteps;
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public DiscoveryProgressStage Stage { get; }

    public int CompletedSteps { get; }

    public int? TotalSteps { get; }

    public DiscoveryProgressSnapshot Snapshot { get; }

    public double? Percentage
    {
        get
        {
            if (TotalSteps is null or 0)
            {
                return null;
            }

            return (double)CompletedSteps / TotalSteps.Value * 100;
        }
    }
}
