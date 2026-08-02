using VSP.Player.Interfaces;

namespace VSP.Player.Pipeline;

public sealed class MediaClock : IMediaClock
{
    private readonly object _gate = new();
    private TimeSpan _currentPresentationTime;

    public TimeSpan CurrentPresentationTime
    {
        get { lock (_gate) { return _currentPresentationTime; } }
    }

    public double PlaybackRate => 1.0;

    /// <summary>Live sources do not support seeking — unsupported per ADR-002.</summary>
    public TimeSpan? Seek(TimeSpan target) => null;

    public void Advance(TimeSpan presentationTimestamp)
    {
        lock (_gate)
        {
            _currentPresentationTime = presentationTimestamp;
        }
    }
}
