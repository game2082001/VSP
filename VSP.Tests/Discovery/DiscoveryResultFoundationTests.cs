using VSP.Device.Discovery.AutoDiscovery;
using VSP.Device.Discovery.Orchestration;
using VSP.Device.Discovery.Progress;
using VSP.Device.Discovery.Sessions;
using Xunit;

namespace VSP.Tests.Discovery;

public class DiscoveryResultFoundationTests
{
    [Fact]
    public void Result_CopiesCandidateResults()
    {
        var candidateResults = new List<CandidateOrchestrationResult>
        {
            CreateCandidateResult("candidate-1")
        };

        var result = new DiscoveryOrchestrationResult
        {
            CandidateResults = candidateResults
        };

        candidateResults.Add(CreateCandidateResult("candidate-2"));

        Assert.Single(result.CandidateResults);
        Assert.Equal("candidate-1", result.CandidateResults[0].Candidate?.CandidateKey);
    }

    [Fact]
    public void Result_CopiesReasons()
    {
        var reasons = new List<DiscoveryOrchestrationReason>
        {
            new("OriginalReason", "Original result reason.")
        };

        var result = new DiscoveryOrchestrationResult
        {
            Reasons = reasons
        };

        reasons.Add(new DiscoveryOrchestrationReason("AddedReason", "Reason added after result creation."));

        Assert.Single(result.Reasons);
        Assert.Equal("OriginalReason", result.Reasons[0].Code);
    }

    [Theory]
    [MemberData(nameof(NegativeSummaryCounters))]
    public void Summary_RejectsNegativeCounters(Func<DiscoveryOrchestrationSummary> createSummary)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => createSummary());
    }

    [Fact]
    public void Summary_AllowsZero()
    {
        var summary = new DiscoveryOrchestrationSummary
        {
            TotalCandidates = 0,
            Registered = 0,
            Approved = 0,
            AwaitingApproval = 0,
            DuplicateSkipped = 0,
            Failed = 0,
            Rejected = 0
        };

        Assert.Equal(0, summary.TotalCandidates);
        Assert.Equal(0, summary.Registered);
        Assert.Equal(0, summary.Approved);
        Assert.Equal(0, summary.AwaitingApproval);
        Assert.Equal(0, summary.DuplicateSkipped);
        Assert.Equal(0, summary.Failed);
        Assert.Equal(0, summary.Rejected);
    }

    [Fact]
    public void Summary_StoresValidValues()
    {
        var summary = new DiscoveryOrchestrationSummary
        {
            TotalCandidates = 7,
            Registered = 2,
            Approved = 4,
            AwaitingApproval = 1,
            DuplicateSkipped = 1,
            Failed = 1,
            Rejected = 2
        };

        Assert.Equal(7, summary.TotalCandidates);
        Assert.Equal(2, summary.Registered);
        Assert.Equal(4, summary.Approved);
        Assert.Equal(1, summary.AwaitingApproval);
        Assert.Equal(1, summary.DuplicateSkipped);
        Assert.Equal(1, summary.Failed);
        Assert.Equal(2, summary.Rejected);
    }

    [Fact]
    public void Result_HasNoSessionDependency()
    {
        AssertNoDependencyOnNamespace(
            typeof(DiscoveryOrchestrationResult),
            typeof(DiscoverySession).Namespace!);
    }

    [Fact]
    public void Result_HasNoProgressDependency()
    {
        AssertNoDependencyOnNamespace(
            typeof(DiscoveryOrchestrationResult),
            typeof(DiscoveryProgress).Namespace!);
    }

    [Fact]
    public void SessionSnapshot_ReusesDiscoveryOrchestrationSummary()
    {
        var summary = new DiscoveryOrchestrationSummary
        {
            TotalCandidates = 1,
            Registered = 1
        };

        var snapshot = new DiscoverySessionSnapshot(summary);

        Assert.Same(summary, snapshot.Summary);
    }

    [Fact]
    public void NoDuplicateResultModelsExist()
    {
        var forbiddenTypeNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "DiscoveryResult",
            "DiscoveryResultSummary",
            "DiscoveryResultReason",
            "DiscoveryResultStatistics"
        };

        var matchingTypes = typeof(DiscoveryOrchestrationResult).Assembly.GetTypes()
            .Where(type => forbiddenTypeNames.Contains(type.Name))
            .Select(type => type.FullName)
            .ToArray();

        Assert.Empty(matchingTypes);
    }

    public static IEnumerable<object[]> NegativeSummaryCounters()
    {
        yield return [() => new DiscoveryOrchestrationSummary { TotalCandidates = -1 }];
        yield return [() => new DiscoveryOrchestrationSummary { Registered = -1 }];
        yield return [() => new DiscoveryOrchestrationSummary { Approved = -1 }];
        yield return [() => new DiscoveryOrchestrationSummary { AwaitingApproval = -1 }];
        yield return [() => new DiscoveryOrchestrationSummary { DuplicateSkipped = -1 }];
        yield return [() => new DiscoveryOrchestrationSummary { Failed = -1 }];
        yield return [() => new DiscoveryOrchestrationSummary { Rejected = -1 }];
    }

    private static CandidateOrchestrationResult CreateCandidateResult(string candidateKey)
    {
        return new CandidateOrchestrationResult
        {
            Candidate = new AutoDiscoveryCandidate
            {
                CandidateKey = candidateKey
            },
            Status = CandidateOrchestrationStatus.Registered
        };
    }

    private static void AssertNoDependencyOnNamespace(Type type, string forbiddenNamespace)
    {
        var dependencies = type.GetProperties()
            .Select(property => property.PropertyType)
            .Concat(type.GetFields(
                    System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Public)
                .Select(field => field.FieldType))
            .SelectMany(FlattenType)
            .Where(dependency => dependency.Namespace?.StartsWith(forbiddenNamespace, StringComparison.Ordinal) == true)
            .Select(dependency => dependency.FullName)
            .Distinct()
            .ToArray();

        Assert.Empty(dependencies);
    }

    private static IEnumerable<Type> FlattenType(Type type)
    {
        yield return type;

        if (!type.IsGenericType)
        {
            yield break;
        }

        foreach (var genericArgument in type.GetGenericArguments())
        {
            foreach (var nestedType in FlattenType(genericArgument))
            {
                yield return nestedType;
            }
        }
    }
}
