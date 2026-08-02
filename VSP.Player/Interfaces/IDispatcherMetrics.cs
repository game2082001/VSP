namespace VSP.Player.Interfaces;

public interface IDispatcherMetrics
{
    double FramesPerSecond { get; }

    TimeSpan AverageLatency { get; }

    int QueueLength { get; }

    long DroppedFrameCount { get; }

    event EventHandler? MetricsUpdated;
}
