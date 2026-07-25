using VSP.Device.Discovery.Execution;
using VSP.Device.Discovery.Orchestration;
using VSP.Device.Discovery.Progress;
using Xunit;

namespace VSP.Tests.Discovery;

public class ProgressPublishingDiscoveryRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_PublishesStartedThenTerminalProgress()
    {
        var publisher = new RecordingProgressPublisher();
        var runner = new ProgressPublishingDiscoveryRunner(
            new StubDiscoveryRunner(new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Completed }),
            publisher);

        await runner.ExecuteAsync(new DiscoveryOrchestrationRequest());

        Assert.Equal(2, publisher.Published.Count);
        Assert.Equal(DiscoveryProgressStage.Discovering, publisher.Published[0].Stage);
        Assert.Equal(0, publisher.Published[0].CompletedSteps);
        Assert.Equal(DiscoveryProgressStage.Completed, publisher.Published[1].Stage);
        Assert.Equal(2, publisher.Published[1].CompletedSteps);
    }

    [Theory]
    [InlineData(DiscoveryOrchestrationStatus.Completed, DiscoveryProgressStage.Completed)]
    [InlineData(DiscoveryOrchestrationStatus.CompletedWithErrors, DiscoveryProgressStage.Completed)]
    [InlineData(DiscoveryOrchestrationStatus.Cancelled, DiscoveryProgressStage.Cancelled)]
    [InlineData(DiscoveryOrchestrationStatus.Failed, DiscoveryProgressStage.Failed)]
    [InlineData(DiscoveryOrchestrationStatus.InvalidRequest, DiscoveryProgressStage.Failed)]
    public async Task ExecuteAsync_MapsOrchestrationStatusToTerminalProgressStage(
        DiscoveryOrchestrationStatus status,
        DiscoveryProgressStage expectedStage)
    {
        var publisher = new RecordingProgressPublisher();
        var runner = new ProgressPublishingDiscoveryRunner(
            new StubDiscoveryRunner(new DiscoveryOrchestrationResult { Status = status }),
            publisher);

        await runner.ExecuteAsync(new DiscoveryOrchestrationRequest());

        Assert.Equal(expectedStage, publisher.Published[1].Stage);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsInnerResultUnchanged()
    {
        var expected = new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Completed };
        var runner = new ProgressPublishingDiscoveryRunner(
            new StubDiscoveryRunner(expected),
            new RecordingProgressPublisher());

        var result = await runner.ExecuteAsync(new DiscoveryOrchestrationRequest());

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task ExecuteAsync_PassesRequestAndCancellationTokenToInnerRunner()
    {
        var inner = new StubDiscoveryRunner(new DiscoveryOrchestrationResult());
        var runner = new ProgressPublishingDiscoveryRunner(inner, new RecordingProgressPublisher());
        using var cts = new CancellationTokenSource();
        var request = new DiscoveryOrchestrationRequest { CorrelationId = "abc" };

        await runner.ExecuteAsync(request, cts.Token);

        Assert.Same(request, inner.ReceivedRequest);
        Assert.Equal(cts.Token, inner.ReceivedCancellationToken);
    }

    [Fact]
    public void Constructor_WithNullInner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ProgressPublishingDiscoveryRunner(null!));
    }

    [Fact]
    public void Constructor_WithNullPublisher_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ProgressPublishingDiscoveryRunner(new StubDiscoveryRunner(new DiscoveryOrchestrationResult()), null!));
    }

    private sealed class StubDiscoveryRunner : IDiscoveryRunner
    {
        private readonly DiscoveryOrchestrationResult _result;

        public StubDiscoveryRunner(DiscoveryOrchestrationResult result)
        {
            _result = result;
        }

        public DiscoveryOrchestrationRequest? ReceivedRequest { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<DiscoveryOrchestrationResult> ExecuteAsync(
            DiscoveryOrchestrationRequest? request,
            CancellationToken cancellationToken = default)
        {
            ReceivedRequest = request;
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(_result);
        }
    }

    private sealed class RecordingProgressPublisher : IDiscoveryProgressPublisher
    {
        public List<DiscoveryProgress> Published { get; } = [];

        public void Publish(DiscoveryProgress progress)
        {
            Published.Add(progress);
        }
    }
}
