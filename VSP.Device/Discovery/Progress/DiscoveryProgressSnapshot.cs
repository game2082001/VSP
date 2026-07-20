using System.Collections.ObjectModel;

namespace VSP.Device.Discovery.Progress;

public sealed class DiscoveryProgressSnapshot
{
    public DiscoveryProgressSnapshot(
        DiscoveryProgressStage stage,
        int completedSteps,
        int? totalSteps,
        string? currentOperation = null,
        IEnumerable<ProgressReason>? reasons = null)
    {
        ValidateStage(stage);
        ValidateSteps(completedSteps, totalSteps);

        var copiedReasons = (reasons ?? Array.Empty<ProgressReason>()).ToList();
        if (copiedReasons.Any(static reason => reason is null))
        {
            throw new InvalidOperationException("Progress snapshot reasons must not contain null entries.");
        }

        Stage = stage;
        CurrentOperation = string.IsNullOrWhiteSpace(currentOperation) ? null : currentOperation.Trim();
        CompletedSteps = completedSteps;
        TotalSteps = totalSteps;
        Reasons = new ReadOnlyCollection<ProgressReason>(copiedReasons);
    }

    public DiscoveryProgressStage Stage { get; }

    public string? CurrentOperation { get; }

    public int CompletedSteps { get; }

    public int? TotalSteps { get; }

    public IReadOnlyList<ProgressReason> Reasons { get; }

    internal static void ValidateStage(DiscoveryProgressStage stage)
    {
        if (!Enum.IsDefined(stage))
        {
            throw new ArgumentOutOfRangeException(nameof(stage), "Progress stage must be a defined value.");
        }
    }

    internal static void ValidateSteps(int completedSteps, int? totalSteps)
    {
        if (completedSteps < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(completedSteps), "Completed steps must be greater than or equal to zero.");
        }

        if (totalSteps < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalSteps), "Total steps must be greater than or equal to zero.");
        }

        if (totalSteps.HasValue && completedSteps > totalSteps.Value)
        {
            throw new ArgumentOutOfRangeException(nameof(completedSteps), "Completed steps must not exceed total steps when total steps is known.");
        }
    }
}
