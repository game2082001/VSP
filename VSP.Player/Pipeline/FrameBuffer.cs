using VSP.Player.Entities;
using VSP.Player.Interfaces;

namespace VSP.Player.Pipeline;

public sealed class FrameBuffer<TFrame> : IFrameBuffer<TFrame>
{
    private readonly Queue<TFrame> _queue = new();
    private readonly object _gate = new();
    private readonly int _capacity;

    public FrameBuffer(BufferPolicy policy, int capacity = 8)
    {
        Policy = policy;
        _capacity = capacity;
    }

    public BufferPolicy Policy { get; }

    public event EventHandler<FrameDroppedEventArgs>? FrameDropped;

    public int Count
    {
        get { lock (_gate) { return _queue.Count; } }
    }

    public void Enqueue(TFrame frame)
    {
        lock (_gate)
        {
            if (Policy == BufferPolicy.BlockProducerWhenFull)
            {
                while (_queue.Count >= _capacity)
                {
                    Monitor.Wait(_gate);
                }
            }
            else if (_queue.Count >= _capacity)
            {
                if (Policy == BufferPolicy.DropOldestWhenFull)
                {
                    _queue.Dequeue();
                }

                FrameDropped?.Invoke(this, new FrameDroppedEventArgs { DroppedCount = 1 });

                if (Policy == BufferPolicy.DropNewestWhenFull)
                {
                    return;
                }
            }

            _queue.Enqueue(frame);
            Monitor.PulseAll(_gate);
        }
    }

    public bool TryDequeue(out TFrame? frame)
    {
        lock (_gate)
        {
            if (_queue.Count == 0)
            {
                frame = default;
                return false;
            }

            frame = _queue.Dequeue();
            Monitor.PulseAll(_gate);
            return true;
        }
    }
}
