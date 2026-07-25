using VSP.Device.Discovery.Execution;
using VSP.Device.Discovery.Orchestration;
using VSP.Device.Discovery.Sessions;
using Xunit;

namespace VSP.Tests.Discovery;

public class SessionRecordingDiscoveryRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_PublishesSessionExactlyOnce()
    {
        var sink = new RecordingSessionSink();
        var runner = new SessionRecordingDiscoveryRunner(
            new StubDiscoveryRunner(new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Completed }),
            new DiscoverySessionFactory(),
            sink);

        await runner.ExecuteAsync(new DiscoveryOrchestrationRequest());

        Assert.Single(sink.Published);
    }

    [Fact]
    public async Task ExecuteAsync_WithCompletedResult_PublishesCompletedSession()
    {
        var sink = new RecordingSessionSink();
        var runner = new SessionRecordingDiscoveryRunner(
            new StubDiscoveryRunner(new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Completed }),
            new DiscoverySessionFactory(),
            sink);

        await runner.ExecuteAsync(new DiscoveryOrchestrationRequest());

        Assert.Equal(DiscoverySessionStatus.Completed, sink.Published.Single().Status);
    }

    [Fact]
    public async Task ExecuteAsync_PassesCorrelationIdFromRequestToSession()
    {
        var sink = new RecordingSessionSink();
        var runner = new SessionRecordingDiscoveryRunner(
            new StubDiscoveryRunner(new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Completed }),
            new DiscoverySessionFactory(),
            sink);

        await runner.ExecuteAsync(new DiscoveryOrchestrationRequest { CorrelationId = "corr-1" });

        Assert.Equal("corr-1", sink.Published.Single().CorrelationId);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsInnerResultUnchanged()
    {
        var expected = new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Completed };
        var runner = new SessionRecordingDiscoveryRunner(
            new StubDiscoveryRunner(expected),
            new DiscoverySessionFactory(),
            new RecordingSessionSink());

        var result = await runner.ExecuteAsync(new DiscoveryOrchestrationRequest());

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task ExecuteAsync_WithBackwardCompatibleConstructor_DoesNotThrow()
    {
        var runner = new SessionRecordingDiscoveryRunner(
            new StubDiscoveryRunner(new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Completed }));

        var result = await runner.ExecuteAsync(new DiscoveryOrchestrationRequest());

        Assert.Equal(DiscoveryOrchestrationStatus.Completed, result.Status);
    }

    [Fact]
    public void Constructor_WithNullInner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SessionRecordingDiscoveryRunner(null!));
    }

    private sealed class StubDiscoveryRunner : IDiscoveryRunner
    {
        private readonly DiscoveryOrchestrationResult _result;

        public StubDiscoveryRunner(DiscoveryOrchestrationResult result)
        {
            _result = result;
        }

        public Task<DiscoveryOrchestrationResult> ExecuteAsync(
            DiscoveryOrchestrationRequest? request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_result);
        }
    }

    private sealed class RecordingSessionSink : IDiscoverySessionSink
    {
        public List<DiscoverySession> Published { get; } = [];

        public void Publish(DiscoverySession session)
        {
            Published.Add(session);
        }
    }
}
