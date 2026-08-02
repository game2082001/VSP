using VSP.Player.Interfaces;

namespace VSP.Player.Pipeline;

public sealed class DispatcherMetrics : IDispatcherMetrics
{
    private readonly object _gate = new();
    private readonly Queue<DateTime> _recentDeliveryTimestamps = new();
    private double _framesPerSecond;
    private TimeSpan _averageLatency;
    private int _queueLength;
    private long _droppedFrameCount;

    public double FramesPerSecond
    {
        get { lock (_gate) { return _framesPerSecond; } }
    }

    public TimeSpan AverageLatency
    {
        get { lock (_gate) { return _averageLatency; } }
    }

    public int QueueLength
    {
        get { lock (_gate) { return _queueLength; } }
    }

    public long DroppedFrameCount => Interlocked.Read(ref _droppedFrameCount);

    public event EventHandler? MetricsUpdated;

    public void RecordDelivery(TimeSpan latency, int queueLengthAtDelivery)
    {
        lock (_gate)
        {
            var now = DateTime.UtcNow;
            _recentDeliveryTimestamps.Enqueue(now);
            while (_recentDeliveryTimestamps.Count > 0 && now - _recentDeliveryTimestamps.Peek() > TimeSpan.FromSeconds(1))
            {
                _recentDeliveryTimestamps.Dequeue();
            }

            _framesPerSecond = _recentDeliveryTimestamps.Count;
            _averageLatency = _averageLatency == TimeSpan.Zero
                ? latency
                : TimeSpan.FromTicks((long)(_averageLatency.Ticks * 0.9 + latency.Ticks * 0.1));
            _queueLength = queueLengthAtDelivery;
        }

        MetricsUpdated?.Invoke(this, EventArgs.Empty);
    }

    public void RecordDropped(int count)
    {
        Interlocked.Add(ref _droppedFrameCount, count);
        MetricsUpdated?.Invoke(this, EventArgs.Empty);
    }
}
