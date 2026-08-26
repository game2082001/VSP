using VSP.Player.Entities;
using VSP.Player.Interfaces;

namespace VSP.Player.Pipeline;

public sealed class FrameDispatcher<TFrame> : IFrameDispatcher<TFrame>
{
    private readonly List<Subscription> _subscriptions = new();
    private readonly object _gate = new();
    private readonly DispatcherMetrics _metrics = new();

    public IDispatcherMetrics Metrics => _metrics;

    public IDisposable Subscribe(IFrameConsumer<TFrame> consumer, BufferPolicy policy)
    {
        var subscription = new Subscription(this, consumer, policy);
        lock (_gate)
        {
            _subscriptions.Add(subscription);
        }

        subscription.Start();
        return subscription;
    }

    public void Dispatch(TFrame frame)
    {
        Subscription[] snapshot;
        lock (_gate)
        {
            snapshot = _subscriptions.ToArray();
        }

        var enqueuedAt = DateTime.UtcNow;
        foreach (var subscription in snapshot)
        {
            subscription.Buffer.Enqueue((frame, enqueuedAt));
        }
    }

    private void Remove(Subscription subscription)
    {
        lock (_gate)
        {
            _subscriptions.Remove(subscription);
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly FrameDispatcher<TFrame> _owner;
        private readonly IFrameConsumer<TFrame> _consumer;
        private readonly CancellationTokenSource _cts = new();
        private readonly ManualResetEventSlim _ready = new();
        private Task? _pumpTask;
        private bool _disposed;

        public Subscription(FrameDispatcher<TFrame> owner, IFrameConsumer<TFrame> consumer, BufferPolicy policy)
        {
            _owner = owner;
            _consumer = consumer;
            Buffer = new FrameBuffer<(TFrame Frame, DateTime EnqueuedAt)>(policy);
            Buffer.FrameDropped += (_, e) => _owner._metrics.RecordDropped(e.DroppedCount);
        }

        public FrameBuffer<(TFrame Frame, DateTime EnqueuedAt)> Buffer { get; }

        public void Start()
        {
            _pumpTask = Task.Factory.StartNew(
                Pump,
                _cts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            _ready.Wait(_cts.Token);
        }

        private void Pump()
        {
            var token = _cts.Token;
            _ready.Set();
            while (!token.IsCancellationRequested)
            {
                if (Buffer.WaitToDequeue(out var item, token))
                {
                    var latency = DateTime.UtcNow - item.EnqueuedAt;
                    _owner._metrics.RecordDelivery(latency, Buffer.Count);
                    _consumer.OnFrame(item.Frame);
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cts.Cancel();
            Buffer.Complete();
            try
            {
                _pumpTask?.Wait(TimeSpan.FromSeconds(1));
            }
            catch
            {
                // best-effort during dispose
            }

            _ready.Dispose();
            _cts.Dispose();
            _owner.Remove(this);
        }
    }
}
