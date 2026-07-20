using VSP.Device.Discovery.Orchestration;
using VSP.Device.Discovery.Sessions;
using Xunit;

namespace VSP.Tests.Discovery;

public class DiscoverySessionFactoryTests
{
    [Fact]
    public void Create_WithCompletedResult_CreatesSession()
    {
        var factory = new DiscoverySessionFactory();
        var result = CreateResult(DiscoveryOrchestrationStatus.Completed);

        var session = factory.Create(new DiscoverySessionFactoryRequest
        {
            DiscoveryOrchestrationResult = result,
            StartTime = StartTime,
            EndTime = EndTime
        });

        Assert.NotEqual(Guid.Empty, session.SessionId.Value);
        Assert.Equal(DiscoverySessionStatus.Completed, session.Status);
        Assert.Same(result.Summary, session.Snapshot.Summary);
    }

    [Fact]
    public void Create_MapsCompletedStatus()
    {
        var session = CreateSession(DiscoveryOrchestrationStatus.Completed);

        Assert.Equal(DiscoverySessionStatus.Completed, session.Status);
    }

    [Fact]
    public void Create_MapsCompletedWithErrorsStatus()
    {
        var session = CreateSession(DiscoveryOrchestrationStatus.CompletedWithErrors);

        Assert.Equal(DiscoverySessionStatus.CompletedWithErrors, session.Status);
    }

    [Fact]
    public void Create_MapsCancelledStatus()
    {
        var session = CreateSession(DiscoveryOrchestrationStatus.Cancelled);

        Assert.Equal(DiscoverySessionStatus.Cancelled, session.Status);
    }

    [Fact]
    public void Create_MapsFailedStatus()
    {
        var session = CreateSession(DiscoveryOrchestrationStatus.Failed);

        Assert.Equal(DiscoverySessionStatus.Failed, session.Status);
    }

    [Fact]
    public void Create_MapsInvalidRequestToFailedStatus()
    {
        var session = CreateSession(DiscoveryOrchestrationStatus.InvalidRequest);

        Assert.Equal(DiscoverySessionStatus.Failed, session.Status);
        Assert.Contains(session.Snapshot.Reasons, reason => reason.Code == "InvalidRequestMappedToFailed");
    }

    [Fact]
    public void Create_GeneratesSessionIdWhenMissing()
    {
        var session = CreateSession(DiscoveryOrchestrationStatus.Completed);

        Assert.NotEqual(Guid.Empty, session.SessionId.Value);
    }

    [Fact]
    public void Create_PreservesProvidedSessionId()
    {
        var sessionId = DiscoverySessionId.FromGuid(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var factory = new DiscoverySessionFactory();

        var session = factory.Create(new DiscoverySessionFactoryRequest
        {
            DiscoveryOrchestrationResult = CreateResult(DiscoveryOrchestrationStatus.Completed),
            SessionId = sessionId,
            StartTime = StartTime,
            EndTime = EndTime
        });

        Assert.Equal(sessionId, session.SessionId);
    }

    [Fact]
    public void Create_PreservesCorrelationIdSeparatelyFromSessionId()
    {
        var sessionId = DiscoverySessionId.FromGuid(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var correlationId = "correlation-abc";
        var factory = new DiscoverySessionFactory();

        var session = factory.Create(new DiscoverySessionFactoryRequest
        {
            DiscoveryOrchestrationResult = CreateResult(DiscoveryOrchestrationStatus.Completed),
            SessionId = sessionId,
            CorrelationId = correlationId,
            StartTime = StartTime,
            EndTime = EndTime
        });

        Assert.Equal(sessionId, session.SessionId);
        Assert.Equal(correlationId, session.CorrelationId);
        Assert.NotEqual(session.SessionId.ToString(), session.CorrelationId);
    }

    [Fact]
    public void Snapshot_IsImmutable()
    {
        var reasons = new List<SessionReason>
        {
            new("Original", "Original reason.")
        };
        var snapshot = new DiscoverySessionSnapshot(new DiscoveryOrchestrationSummary(), reasons);

        reasons.Add(new SessionReason("Added", "Added after snapshot creation."));

        Assert.Single(snapshot.Reasons);
        Assert.IsAssignableFrom<IReadOnlyList<SessionReason>>(snapshot.Reasons);
        Assert.DoesNotContain(snapshot.Reasons, reason => reason.Code == "Added");
    }

    [Fact]
    public void Create_ReusesDiscoveryOrchestrationSummary()
    {
        var factory = new DiscoverySessionFactory();
        var summary = new DiscoveryOrchestrationSummary
        {
            TotalCandidates = 3,
            Registered = 2
        };
        var result = new DiscoveryOrchestrationResult
        {
            Status = DiscoveryOrchestrationStatus.Completed,
            Summary = summary
        };

        var session = factory.Create(new DiscoverySessionFactoryRequest
        {
            DiscoveryOrchestrationResult = result,
            StartTime = StartTime,
            EndTime = EndTime
        });

        Assert.Same(summary, session.Snapshot.Summary);
    }

    [Fact]
    public void Create_WhenEndTimeEarlierThanStartTime_ReturnsFailedSessionWithReason()
    {
        var factory = new DiscoverySessionFactory();

        var session = factory.Create(new DiscoverySessionFactoryRequest
        {
            DiscoveryOrchestrationResult = CreateResult(DiscoveryOrchestrationStatus.Completed),
            StartTime = EndTime,
            EndTime = StartTime
        });

        Assert.Equal(DiscoverySessionStatus.Failed, session.Status);
        Assert.Equal(TimeSpan.Zero, session.Duration);
        Assert.Contains(session.Snapshot.Reasons, reason => reason.Code == "InvalidTimeRange");
    }

    [Fact]
    public void FromGuid_RejectsEmptyGuid()
    {
        Assert.Throws<ArgumentException>(() => DiscoverySessionId.FromGuid(Guid.Empty));
    }

    private static DiscoverySession CreateSession(DiscoveryOrchestrationStatus status)
    {
        return new DiscoverySessionFactory().Create(new DiscoverySessionFactoryRequest
        {
            DiscoveryOrchestrationResult = CreateResult(status),
            StartTime = StartTime,
            EndTime = EndTime
        });
    }

    private static DiscoveryOrchestrationResult CreateResult(DiscoveryOrchestrationStatus status)
    {
        return new DiscoveryOrchestrationResult
        {
            Status = status,
            Summary = new DiscoveryOrchestrationSummary
            {
                TotalCandidates = 1,
                Registered = status == DiscoveryOrchestrationStatus.Completed ? 1 : 0,
                Failed = status == DiscoveryOrchestrationStatus.Failed ? 1 : 0
            },
            Reasons =
            [
                new DiscoveryOrchestrationReason("ResultReason", "Result reason.")
            ]
        };
    }

    private static readonly DateTimeOffset StartTime = new(2026, 7, 20, 10, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset EndTime = new(2026, 7, 20, 10, 0, 5, TimeSpan.Zero);
}
