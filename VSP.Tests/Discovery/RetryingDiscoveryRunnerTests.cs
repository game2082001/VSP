using VSP.Device.Discovery.Execution;
using VSP.Device.Discovery.Orchestration;
using Xunit;

namespace VSP.Tests.Discovery;

public class RetryingDiscoveryRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_WithSuccessOnFirstAttempt_DoesNotRetry()
    {
        var inner = new ScriptedDiscoveryRunner(
            new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Completed });
        var runner = new RetryingDiscoveryRunner(inner, new DiscoveryRetryPolicy(3, TimeSpan.Zero));

        var result = await runner.ExecuteAsync(new DiscoveryOrchestrationRequest());

        Assert.Equal(DiscoveryOrchestrationStatus.Completed, result.Status);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithFailedThenCompleted_RetriesUntilSuccess()
    {
        var inner = new ScriptedDiscoveryRunner(
            new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Failed },
            new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Completed });
        var runner = new RetryingDiscoveryRunner(inner, new DiscoveryRetryPolicy(3, TimeSpan.Zero));

        var result = await runner.ExecuteAsync(new DiscoveryOrchestrationRequest());

        Assert.Equal(DiscoveryOrchestrationStatus.Completed, result.Status);
        Assert.Equal(2, inner.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithAlwaysFailed_StopsAtMaxAttemptsAndReturnsFailed()
    {
        var inner = new ScriptedDiscoveryRunner(
            new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Failed },
            new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Failed },
            new DiscoveryOrchestrationResult { Status = DiscoveryOrchestrationStatus.Failed });
        var runner = new RetryingDiscoveryRunner(inner, new DiscoveryRetryPolicy(3, TimeSpan.Zero));

        var result = await runner.ExecuteAsync(new DiscoveryOrchestrationRequest());

        Assert.Equal(DiscoveryOrchestrationStatus.Failed, result.Status);
        Assert.Equal(3, inner.CallCount);
    }

    [Theory]
    [InlineData(DiscoveryOrchestrationStatus.Cancelled)]
    [InlineData(DiscoveryOrchestrationStatus.InvalidRequest)]
    [InlineData(DiscoveryOrchestrationStatus.CompletedWithErrors)]
    public async Task ExecuteAsync_WithNonRetryableStatus_DoesNotRetry(DiscoveryOrchestrationStatus status)
    {
        var inner = new ScriptedDiscoveryRunner(new DiscoveryOrchestrationResult { Status = status });
        var runner = new RetryingDiscoveryRunner(inner, new DiscoveryRetryPolicy(3, TimeSpan.Zero));

        var result = await runner.ExecuteAsync(new DiscoveryOrchestrationRequest());

        Assert.Equal(status, result.Status);
        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledDuringInnerCall_PropagatesImmediatelyWithoutRetry()
    {
        var inner = new ThrowingDiscoveryRunner(new OperationCanceledException());
        var runner = new RetryingDiscoveryRunner(inner, new DiscoveryRetryPolicy(3, TimeSpan.Zero));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => runner.ExecuteAsync(new DiscoveryOrchestrationRequest()));

        Assert.Equal(1, inner.CallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInnerThrows_RetriesThenRethrowsAfterMaxAttempts()
    {
        var inner = new ThrowingDiscoveryRunner(new InvalidOperationException("transient"));
        var runner = new RetryingDiscoveryRunner(inner, new DiscoveryRetryPolicy(3, TimeSpan.Zero));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => runner.ExecuteAsync(new DiscoveryOrchestrationRequest()));

        Assert.Equal(3, inner.CallCount);
    }

    [Fact]
    public void Constructor_WithNullInner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RetryingDiscoveryRunner(null!));
    }

    [Fact]
    public void Constructor_WithNullPolicy_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RetryingDiscoveryRunner(new ScriptedDiscoveryRunner(new DiscoveryOrchestrationResult()), null!));
    }

    [Fact]
    public void RetryPolicy_WithZeroMaxAttempts_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiscoveryRetryPolicy(0, TimeSpan.Zero));
    }

    [Fact]
    public void RetryPolicy_WithNegativeDelay_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new DiscoveryRetryPolicy(1, TimeSpan.FromSeconds(-1)));
    }

    private sealed class ScriptedDiscoveryRunner : IDiscoveryRunner
    {
        private readonly Queue<DiscoveryOrchestrationResult> _results;

        public ScriptedDiscoveryRunner(params DiscoveryOrchestrationResult[] results)
        {
            _results = new Queue<DiscoveryOrchestrationResult>(results);
        }

        public int CallCount { get; private set; }

        public Task<DiscoveryOrchestrationResult> ExecuteAsync(
            DiscoveryOrchestrationRequest? request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var result = _results.Count > 1 ? _results.Dequeue() : _results.Peek();
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingDiscoveryRunner : IDiscoveryRunner
    {
        private readonly Exception _exception;

        public ThrowingDiscoveryRunner(Exception exception)
        {
            _exception = exception;
        }

        public int CallCount { get; private set; }

        public Task<DiscoveryOrchestrationResult> ExecuteAsync(
            DiscoveryOrchestrationRequest? request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw _exception;
        }
    }
}
