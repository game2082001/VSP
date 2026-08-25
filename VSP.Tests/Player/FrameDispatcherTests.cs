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

    [Fact]
    public async Task Dispatch_RapidSubscribeDispatch_DeliversWithoutPumpStartupRace()
    {
        for (var i = 0; i < 100; i++)
        {
            var dispatcher = new FrameDispatcher<int>();
            var consumer = new RecordingConsumer();
            using var subscription = dispatcher.Subscribe(consumer, BufferPolicy.DropOldestWhenFull);

            dispatcher.Dispatch(i);

            Assert.True(await consumer.WaitForCountAsync(1, TimeSpan.FromSeconds(2)));
            Assert.Equal([i], consumer.Received);
        }
    }

    [Fact]
    public async Task Dispatch_MultipleSubscribers_DeliversToEachConsumer()
    {
        var dispatcher = new FrameDispatcher<int>();
        var first = new RecordingConsumer();
        var second = new RecordingConsumer();
        using var firstSubscription = dispatcher.Subscribe(first, BufferPolicy.DropOldestWhenFull);
        using var secondSubscription = dispatcher.Subscribe(second, BufferPolicy.DropOldestWhenFull);

        dispatcher.Dispatch(7);

        Assert.True(await first.WaitForCountAsync(1, TimeSpan.FromSeconds(2)));
        Assert.True(await second.WaitForCountAsync(1, TimeSpan.FromSeconds(2)));
        Assert.Equal([7], first.Received);
        Assert.Equal([7], second.Received);
    }

    [Fact]
    public async Task Dispose_WithEmptyBuffer_StopsWaitingPump()
    {
        var dispatcher = new FrameDispatcher<int>();
        var consumer = new RecordingConsumer();
        var subscription = dispatcher.Subscribe(consumer, BufferPolicy.DropOldestWhenFull);

        await Task.Run(subscription.Dispose).WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(consumer.Received);
    }

    [Fact]
    public async Task Dispatch_AfterOneSubscriberUnsubscribes_DeliversOnlyToRemainingSubscriber()
    {
        var dispatcher = new FrameDispatcher<int>();
        var removed = new RecordingConsumer();
        var remaining = new RecordingConsumer();
        var removedSubscription = dispatcher.Subscribe(removed, BufferPolicy.DropOldestWhenFull);
        using var remainingSubscription = dispatcher.Subscribe(remaining, BufferPolicy.DropOldestWhenFull);

        dispatcher.Dispatch(1);
        Assert.True(await removed.WaitForCountAsync(1, TimeSpan.FromSeconds(2)));
        Assert.True(await remaining.WaitForCountAsync(1, TimeSpan.FromSeconds(2)));

        removedSubscription.Dispose();
        dispatcher.Dispatch(2);

        Assert.True(await remaining.WaitForCountAsync(2, TimeSpan.FromSeconds(2)));
        await Task.Delay(100);
        Assert.Equal([1], removed.Received);
        Assert.Equal([1, 2], remaining.Received);
    }

    [Fact]
    public async Task Dispose_WithBlockedProducer_CompletesOutstandingDispatch()
    {
        var dispatcher = new FrameDispatcher<int>();
        var consumer = new BlockingConsumer();
        var subscription = dispatcher.Subscribe(consumer, BufferPolicy.BlockProducerWhenFull);

        var producer = Task.Run(() =>
        {
            for (var i = 0; i < 32; i++)
            {
                dispatcher.Dispatch(i);
            }
        });

        Assert.True(consumer.WaitForEntered(TimeSpan.FromSeconds(2)));

        subscription.Dispose();

        try
        {
            await producer.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            consumer.Release();
        }
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

    private sealed class BlockingConsumer : IFrameConsumer<int>
    {
        private readonly ManualResetEventSlim _entered = new();
        private readonly ManualResetEventSlim _release = new();

        public void OnFrame(int frame)
        {
            _entered.Set();
            _release.Wait();
        }

        public bool WaitForEntered(TimeSpan timeout)
        {
            return _entered.Wait(timeout);
        }

        public void Release()
        {
            _release.Set();
        }
    }
}
