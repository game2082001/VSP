using VSP.Device.Discovery.Progress;
using Xunit;

namespace VSP.Tests.Discovery;

public class DiscoveryProgressTests
{
    [Fact]
    public void DiscoveryProgress_CreatesProgressWithStageAndSnapshot()
    {
        var snapshot = new DiscoveryProgressSnapshot(
            DiscoveryProgressStage.Mapping,
            completedSteps: 1,
            totalSteps: 4,
            currentOperation: "Mapping candidate evidence.");

        var progress = new DiscoveryProgress(
            DiscoveryProgressStage.Mapping,
            completedSteps: 1,
            totalSteps: 4,
            snapshot);

        Assert.Equal(DiscoveryProgressStage.Mapping, progress.Stage);
        Assert.Equal(1, progress.CompletedSteps);
        Assert.Equal(4, progress.TotalSteps);
        Assert.Same(snapshot, progress.Snapshot);
    }

    [Fact]
    public void DiscoveryProgress_RejectsUndefinedStage()
    {
        var snapshot = new DiscoveryProgressSnapshot(DiscoveryProgressStage.NotStarted, 0, null);

        Assert.Throws<ArgumentOutOfRangeException>(() => new DiscoveryProgress(
            (DiscoveryProgressStage)999,
            completedSteps: 0,
            totalSteps: null,
            snapshot));
    }

    [Fact]
    public void DiscoveryProgress_RejectsNegativeCompletedSteps()
    {
        var snapshot = new DiscoveryProgressSnapshot(DiscoveryProgressStage.NotStarted, 0, null);

        Assert.Throws<ArgumentOutOfRangeException>(() => new DiscoveryProgress(
            DiscoveryProgressStage.NotStarted,
            completedSteps: -1,
            totalSteps: null,
            snapshot));
    }

    [Fact]
    public void DiscoveryProgress_RejectsNegativeTotalSteps()
    {
        var snapshot = new DiscoveryProgressSnapshot(DiscoveryProgressStage.NotStarted, 0, null);

        Assert.Throws<ArgumentOutOfRangeException>(() => new DiscoveryProgress(
            DiscoveryProgressStage.NotStarted,
            completedSteps: 0,
            totalSteps: -1,
            snapshot));
    }

    [Fact]
    public void DiscoveryProgress_RejectsCompletedStepsGreaterThanTotalSteps()
    {
        var snapshot = new DiscoveryProgressSnapshot(DiscoveryProgressStage.NotStarted, 0, null);

        Assert.Throws<ArgumentOutOfRangeException>(() => new DiscoveryProgress(
            DiscoveryProgressStage.NotStarted,
            completedSteps: 2,
            totalSteps: 1,
            snapshot));
    }

    [Fact]
    public void DiscoveryProgress_AllowsUnknownTotalSteps()
    {
        var progress = new DiscoveryProgress(
            DiscoveryProgressStage.Discovering,
            completedSteps: 3,
            totalSteps: null,
            new DiscoveryProgressSnapshot(DiscoveryProgressStage.Discovering, 3, null));

        Assert.Null(progress.TotalSteps);
        Assert.Null(progress.Percentage);
    }

    [Fact]
    public void DiscoveryProgress_ReturnsZeroPercentage()
    {
        var progress = new DiscoveryProgress(
            DiscoveryProgressStage.NotStarted,
            completedSteps: 0,
            totalSteps: 4,
            new DiscoveryProgressSnapshot(DiscoveryProgressStage.NotStarted, 0, 4));

        Assert.Equal(0, progress.Percentage);
    }

    [Fact]
    public void DiscoveryProgress_ReturnsSeventyFivePercentage()
    {
        var progress = new DiscoveryProgress(
            DiscoveryProgressStage.Registering,
            completedSteps: 3,
            totalSteps: 4,
            new DiscoveryProgressSnapshot(DiscoveryProgressStage.Registering, 3, 4));

        Assert.Equal(75, progress.Percentage);
    }

    [Fact]
    public void DiscoveryProgress_ReturnsOneHundredPercentage()
    {
        var progress = new DiscoveryProgress(
            DiscoveryProgressStage.Completed,
            completedSteps: 4,
            totalSteps: 4,
            new DiscoveryProgressSnapshot(DiscoveryProgressStage.Completed, 4, 4));

        Assert.Equal(100, progress.Percentage);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void DiscoveryProgress_ReturnsNullPercentageWhenTotalStepsIsUnavailable(int? totalSteps)
    {
        var progress = new DiscoveryProgress(
            DiscoveryProgressStage.Discovering,
            completedSteps: 0,
            totalSteps: totalSteps,
            new DiscoveryProgressSnapshot(DiscoveryProgressStage.Discovering, 0, totalSteps));

        Assert.Null(progress.Percentage);
    }

    [Fact]
    public void DiscoveryProgressSnapshot_IsImmutable()
    {
        var reasons = new List<ProgressReason>
        {
            new("Started", "Discovery progress started.")
        };
        var snapshot = new DiscoveryProgressSnapshot(
            DiscoveryProgressStage.Discovering,
            completedSteps: 1,
            totalSteps: 3,
            reasons: reasons);

        reasons.Add(new ProgressReason("Added", "Added after snapshot creation."));

        Assert.Single(snapshot.Reasons);
        Assert.DoesNotContain(snapshot.Reasons, reason => reason.Code == "Added");
    }

    [Fact]
    public void DiscoveryProgressSnapshot_CopiesReasonCollection()
    {
        var reasons = new[]
        {
            new ProgressReason("Mapping", "Mapping is active.")
        };

        var snapshot = new DiscoveryProgressSnapshot(
            DiscoveryProgressStage.Mapping,
            completedSteps: 1,
            totalSteps: 2,
            reasons: reasons);

        Assert.NotSame(reasons, snapshot.Reasons);
        Assert.Equal("Mapping", snapshot.Reasons.Single().Code);
    }

    [Fact]
    public void DiscoveryProgressSnapshot_RejectsNullReason()
    {
        ProgressReason?[] reasons = [null];

        Assert.Throws<InvalidOperationException>(() => new DiscoveryProgressSnapshot(
            DiscoveryProgressStage.Failed,
            completedSteps: 0,
            totalSteps: null,
            reasons: reasons!));
    }

    [Theory]
    [InlineData(null, "message")]
    [InlineData("", "message")]
    [InlineData(" ", "message")]
    [InlineData("Code", null)]
    [InlineData("Code", "")]
    [InlineData("Code", " ")]
    public void ProgressReason_RejectsInvalidCodeOrMessage(string? code, string? message)
    {
        Assert.Throws<ArgumentException>(() => new ProgressReason(code!, message!));
    }

    [Fact]
    public void DiscoveryProgressStage_ContainsRequiredStages()
    {
        Assert.Contains(DiscoveryProgressStage.NotStarted, Enum.GetValues<DiscoveryProgressStage>());
        Assert.Contains(DiscoveryProgressStage.Discovering, Enum.GetValues<DiscoveryProgressStage>());
        Assert.Contains(DiscoveryProgressStage.Mapping, Enum.GetValues<DiscoveryProgressStage>());
        Assert.Contains(DiscoveryProgressStage.SelectingDriver, Enum.GetValues<DiscoveryProgressStage>());
        Assert.Contains(DiscoveryProgressStage.AwaitingApproval, Enum.GetValues<DiscoveryProgressStage>());
        Assert.Contains(DiscoveryProgressStage.CreatingCamera, Enum.GetValues<DiscoveryProgressStage>());
        Assert.Contains(DiscoveryProgressStage.Registering, Enum.GetValues<DiscoveryProgressStage>());
        Assert.Contains(DiscoveryProgressStage.Completed, Enum.GetValues<DiscoveryProgressStage>());
        Assert.Contains(DiscoveryProgressStage.Cancelled, Enum.GetValues<DiscoveryProgressStage>());
        Assert.Contains(DiscoveryProgressStage.Failed, Enum.GetValues<DiscoveryProgressStage>());
    }

    [Fact]
    public void ProgressNamespace_HasNoUiTransportSessionOrOrchestrationDependency()
    {
        var progressTypes = typeof(DiscoveryProgress).Assembly.GetTypes()
            .Where(type => type.Namespace == "VSP.Device.Discovery.Progress")
            .ToArray();

        var referencedTypeNames = progressTypes
            .SelectMany(type => type.GetMembers())
            .SelectMany(member => member switch
            {
                System.Reflection.PropertyInfo property => [property.PropertyType.FullName],
                System.Reflection.ConstructorInfo constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType.FullName),
                System.Reflection.MethodInfo method => method.GetParameters().Select(parameter => parameter.ParameterType.FullName).Append(method.ReturnType.FullName),
                _ => []
            })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        Assert.DoesNotContain(referencedTypeNames, name => name!.Contains("Presentation", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedTypeNames, name => name!.Contains("Windows", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedTypeNames, name => name!.Contains("SignalR", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedTypeNames, name => name!.Contains("Logging", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedTypeNames, name => name!.Contains("Repository", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedTypeNames, name => name!.Contains("Sessions", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedTypeNames, name => name!.Contains("Orchestration", StringComparison.OrdinalIgnoreCase));
    }
}
