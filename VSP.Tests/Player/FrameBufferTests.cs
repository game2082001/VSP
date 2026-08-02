using VSP.Player.Entities;
using VSP.Player.Pipeline;
using Xunit;

namespace VSP.Tests.Player;

public class FrameBufferTests
{
    [Fact]
    public void Enqueue_DropOldestWhenFull_DropsOldestAndRaisesFrameDropped()
    {
        var buffer = new FrameBuffer<int>(BufferPolicy.DropOldestWhenFull, capacity: 2);
        var droppedCount = 0;
        buffer.FrameDropped += (_, e) => droppedCount += e.DroppedCount;

        buffer.Enqueue(1);
        buffer.Enqueue(2);
        buffer.Enqueue(3);

        Assert.Equal(2, buffer.Count);
        Assert.Equal(1, droppedCount);

        buffer.TryDequeue(out var first);
        Assert.Equal(2, first);
    }

    [Fact]
    public void Enqueue_DropNewestWhenFull_KeepsExistingItemsAndDropsIncoming()
    {
        var buffer = new FrameBuffer<int>(BufferPolicy.DropNewestWhenFull, capacity: 2);
        var droppedCount = 0;
        buffer.FrameDropped += (_, e) => droppedCount += e.DroppedCount;

        buffer.Enqueue(1);
        buffer.Enqueue(2);
        buffer.Enqueue(3);

        Assert.Equal(2, buffer.Count);
        Assert.Equal(1, droppedCount);

        buffer.TryDequeue(out var first);
        Assert.Equal(1, first);
    }

    [Fact]
    public void Enqueue_BlockProducerWhenFull_UnblocksAfterDequeue()
    {
        var buffer = new FrameBuffer<int>(BufferPolicy.BlockProducerWhenFull, capacity: 1);
        buffer.Enqueue(1);

        var producer = Task.Run(() => buffer.Enqueue(2));

        Assert.False(producer.Wait(TimeSpan.FromMilliseconds(200)));

        buffer.TryDequeue(out _);

        Assert.True(producer.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(1, buffer.Count);
    }

    [Fact]
    public void TryDequeue_WhenEmpty_ReturnsFalse()
    {
        var buffer = new FrameBuffer<int>(BufferPolicy.DropOldestWhenFull);

        var result = buffer.TryDequeue(out var frame);

        Assert.False(result);
        Assert.Equal(0, frame);
    }
}
