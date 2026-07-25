using VSP.Device.Discovery.Execution;
using VSP.Device.Discovery.Orchestration;
using Xunit;

namespace VSP.Tests.Discovery;

public class TimeoutDiscoveryRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_WithFastInner_ReturnsResultUnchanged()
    {
        var expected = new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Completed };
        var runner = new TimeoutDiscoveryRunner(
            new ImmediateDiscoveryRunner(expected),
            new DiscoveryTimeoutPolicy(TimeSpan.FromSeconds(5)));

        var result = await runner.ExecuteAsync(new DiscoveryOrchestrationRequest());

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInnerExceedsTimeout_ThrowsDiscoveryTimeoutException()
    {
        var runner = new TimeoutDiscoveryRunner(
            new DelayingDiscoveryRunner(TimeSpan.FromSeconds(10)),
            new DiscoveryTimeoutPolicy(TimeSpan.FromMilliseconds(20)));

        var exception = await Assert.ThrowsAsync<DiscoveryTimeoutException>(
            () => runner.ExecuteAsync(new DiscoveryOrchestrationRequest()));

        Assert.Equal(TimeSpan.FromMilliseconds(20), exception.Timeout);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCallerCancelsBeforeTimeout_PropagatesOperationCanceledExceptionNotTimeout()
    {
        using var callerCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));
        var runner = new TimeoutDiscoveryRunner(
            new DelayingDiscoveryRunner(TimeSpan.FromSeconds(10)),
            new DiscoveryTimeoutPolicy(TimeSpan.FromSeconds(5)));

        var exception = await Record.ExceptionAsync(
            () => runner.ExecuteAsync(new DiscoveryOrchestrationRequest(), callerCts.Token));

        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.IsNotType<DiscoveryTimeoutException>(exception);
    }

    [Fact]
    public void Constructor_WithNullInner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TimeoutDiscoveryRunner(null!));
    }

    [Fact]
    public void Constructor_WithNullPolicy_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new TimeoutDiscoveryRunner(new ImmediateDiscoveryRunner(new DiscoveryOrchestrationResult()), null!));
    }

    [Fact]
    public void TimeoutPolicy_WithZeroTimeout_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiscoveryTimeoutPolicy(TimeSpan.Zero));
    }

    [Fact]
    public void TimeoutPolicy_WithNegativeTimeout_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiscoveryTimeoutPolicy(TimeSpan.FromSeconds(-1)));
    }

    private sealed class ImmediateDiscoveryRunner : IDiscoveryRunner
    {
        private readonly DiscoveryOrchestrationResult _result;

        public ImmediateDiscoveryRunner(DiscoveryOrchestrationResult result)
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

    private sealed class DelayingDiscoveryRunner : IDiscoveryRunner
    {
        private readonly TimeSpan _delay;

        public DelayingDiscoveryRunner(TimeSpan delay)
        {
            _delay = delay;
        }

        public async Task<DiscoveryOrchestrationResult> ExecuteAsync(
            DiscoveryOrchestrationRequest? request,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(_delay, cancellationToken).ConfigureAwait(false);
            return new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Completed };
        }
    }
}
