using VSP.Device.Discovery.Diagnostics;
using VSP.Device.Discovery.Execution;
using VSP.Device.Discovery.Orchestration;
using Xunit;

namespace VSP.Tests.Discovery;

public class DiagnosticsRecordingDiscoveryRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_PublishesExactlyOneSnapshot()
    {
        var sink = new RecordingDiagnosticsSink();
        var runner = new DiagnosticsRecordingDiscoveryRunner(
            new StubDiscoveryRunner(new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Completed }),
            sink);

        await runner.ExecuteAsync(new DiscoveryOrchestrationRequest());

        Assert.Single(sink.Published);
    }

    [Fact]
    public async Task ExecuteAsync_PublishesCorrelationIdStatusAndReasons()
    {
        var sink = new RecordingDiagnosticsSink();
        var reasons = new[] { new DiscoveryOrchestrationReason("CODE-1", "message-1") };
        var runner = new DiagnosticsRecordingDiscoveryRunner(
            new StubDiscoveryRunner(new DiscoveryOrchestrationResult
            {
                Status = DiscoveryOrchestrationStatus.Failed,
                Reasons = reasons
            }),
            sink);

        await runner.ExecuteAsync(new DiscoveryOrchestrationRequest { CorrelationId = "corr-7" });

        var snapshot = sink.Published.Single();
        Assert.NotEqual(Guid.Empty, snapshot.DiagnosticId);
        Assert.Equal("corr-7", snapshot.CorrelationId);
        Assert.Equal(DiscoveryOrchestrationStatus.Failed, snapshot.Status);
        Assert.Single(snapshot.Reasons);
        Assert.Equal("CODE-1", snapshot.Reasons[0].Code);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratesDistinctDiagnosticIdsAcrossCalls()
    {
        var sink = new RecordingDiagnosticsSink();
        var runner = new DiagnosticsRecordingDiscoveryRunner(
            new StubDiscoveryRunner(new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Completed }),
            sink);

        await runner.ExecuteAsync(new DiscoveryOrchestrationRequest());
        await runner.ExecuteAsync(new DiscoveryOrchestrationRequest());

        Assert.NotEqual(sink.Published[0].DiagnosticId, sink.Published[1].DiagnosticId);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsInnerResultUnchanged()
    {
        var expected = new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Completed };
        var runner = new DiagnosticsRecordingDiscoveryRunner(
            new StubDiscoveryRunner(expected),
            new RecordingDiagnosticsSink());

        var result = await runner.ExecuteAsync(new DiscoveryOrchestrationRequest());

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInnerThrows_DoesNotPublishSnapshot()
    {
        var sink = new RecordingDiagnosticsSink();
        var runner = new DiagnosticsRecordingDiscoveryRunner(new ThrowingDiscoveryRunner(), sink);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.ExecuteAsync(new DiscoveryOrchestrationRequest()));

        Assert.Empty(sink.Published);
    }

    [Fact]
    public void Constructor_WithNullInner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DiagnosticsRecordingDiscoveryRunner(null!));
    }

    [Fact]
    public void Constructor_WithNullSink_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DiagnosticsRecordingDiscoveryRunner(new StubDiscoveryRunner(new DiscoveryOrchestrationResult()), null!));
    }

    [Fact]
    public void Snapshot_WithEmptyDiagnosticId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new DiscoveryDiagnosticsSnapshot(Guid.Empty, DateTimeOffset.UtcNow, null, DiscoveryOrchestrationStatus.Completed));
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

    private sealed class ThrowingDiscoveryRunner : IDiscoveryRunner
    {
        public Task<DiscoveryOrchestrationResult> ExecuteAsync(
            DiscoveryOrchestrationRequest? request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("boom");
        }
    }

    private sealed class RecordingDiagnosticsSink : IDiscoveryDiagnosticsSink
    {
        public List<DiscoveryDiagnosticsSnapshot> Published { get; } = [];

        public void Publish(DiscoveryDiagnosticsSnapshot snapshot)
        {
            Published.Add(snapshot);
        }
    }
}
