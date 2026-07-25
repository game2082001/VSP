using VSP.Device.Discovery.Execution;
using VSP.Device.Discovery.Metrics;
using VSP.Device.Discovery.Orchestration;
using Xunit;

namespace VSP.Tests.Discovery;

public class MetricsRecordingDiscoveryRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_RecordsExactlyOneSample()
    {
        var sink = new RecordingMetricsSink();
        var runner = new MetricsRecordingDiscoveryRunner(
            new StubDiscoveryRunner(new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Completed }),
            sink);

        await runner.ExecuteAsync(new DiscoveryOrchestrationRequest());

        Assert.Single(sink.Recorded);
    }

    [Fact]
    public async Task ExecuteAsync_RecordsResultStatusAndCorrelationId()
    {
        var sink = new RecordingMetricsSink();
        var runner = new MetricsRecordingDiscoveryRunner(
            new StubDiscoveryRunner(new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Failed }),
            sink);

        await runner.ExecuteAsync(new DiscoveryOrchestrationRequest { CorrelationId = "corr-9" });

        var sample = sink.Recorded.Single();
        Assert.Equal(DiscoveryOrchestrationStatus.Failed, sample.Status);
        Assert.Equal("corr-9", sample.CorrelationId);
        Assert.True(sample.Duration >= TimeSpan.Zero);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsInnerResultUnchanged()
    {
        var expected = new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Completed };
        var runner = new MetricsRecordingDiscoveryRunner(
            new StubDiscoveryRunner(expected),
            new RecordingMetricsSink());

        var result = await runner.ExecuteAsync(new DiscoveryOrchestrationRequest());

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInnerThrows_DoesNotRecordSample()
    {
        var sink = new RecordingMetricsSink();
        var runner = new MetricsRecordingDiscoveryRunner(new ThrowingDiscoveryRunner(), sink);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.ExecuteAsync(new DiscoveryOrchestrationRequest()));

        Assert.Empty(sink.Recorded);
    }

    [Fact]
    public void Constructor_WithNullInner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MetricsRecordingDiscoveryRunner(null!));
    }

    [Fact]
    public void Constructor_WithNullSink_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new MetricsRecordingDiscoveryRunner(new StubDiscoveryRunner(new DiscoveryOrchestrationResult()), null!));
    }

    [Fact]
    public void Sample_WithNegativeDuration_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new DiscoveryMetricsSample(DiscoveryOrchestrationStatus.Completed, TimeSpan.FromSeconds(-1), null));
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

    private sealed class RecordingMetricsSink : IDiscoveryMetricsSink
    {
        public List<DiscoveryMetricsSample> Recorded { get; } = [];

        public void Record(DiscoveryMetricsSample sample)
        {
            Recorded.Add(sample);
        }
    }
}
