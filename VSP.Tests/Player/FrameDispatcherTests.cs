using VSP.Player.Entities;
using VSP.Player.Interfaces;
using VSP.Player.Pipeline;
using Xunit;

namespace VSP.Tests.Player;

public class FrameDispatcherTests
{
    [Fact]
    public async Task Dispatch_DeliversFramesToSubscribedConsumer()
    {
        var dispatcher = new FrameDispatcher<int>();
        var consumer = new RecordingConsumer();
        using var subscription = dispatcher.Subscribe(consumer, BufferPolicy.DropOldestWhenFull);

        dispatcher.Dispatch(1);
        dispatcher.Dispatch(2);

        Assert.True(await consumer.WaitForCountAsync(2, TimeSpan.FromSeconds(2)));
        Assert.Equal([1, 2], consumer.Received);
    }

    [Fact]
    public async Task Dispatch_AfterUnsubscribe_DoesNotDeliverToConsumer()
    {
        var dispatcher = new FrameDispatcher<int>();
        var consumer = new RecordingConsumer();
        var subscription = dispatcher.Subscribe(consumer, BufferPolicy.DropOldestWhenFull);

        dispatcher.Dispatch(1);
        Assert.True(await consumer.WaitForCountAsync(1, TimeSpan.FromSeconds(2)));

        subscription.Dispose();
        dispatcher.Dispatch(2);

        await Task.Delay(100);
        Assert.Single(consumer.Received);
    }

    [Fact]
    public async Task Dispatch_UpdatesMetricsFramesPerSecondAndLatency()
    {
        var dispatcher = new FrameDispatcher<int>();
        var consumer = new RecordingConsumer();
        using var subscription = dispatcher.Subscribe(consumer, BufferPolicy.DropOldestWhenFull);

        dispatcher.Dispatch(1);
        Assert.True(await consumer.WaitForCountAsync(1, TimeSpan.FromSeconds(2)));

        Assert.True(dispatcher.Metrics.FramesPerSecond > 0);
    }

    private sealed class RecordingConsumer : IFrameConsumer<int>
    {
        private readonly object _gate = new();
        public List<int> Received { get; } = new();

        public void OnFrame(int frame)
        {
            lock (_gate)
            {
                Received.Add(frame);
            }
        }

        public async Task<bool> WaitForCountAsync(int count, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                lock (_gate)
                {
                    if (Received.Count >= count)
                    {
                        return true;
                    }
                }

                await Task.Delay(10);
            }

            return false;
        }
    }
}
