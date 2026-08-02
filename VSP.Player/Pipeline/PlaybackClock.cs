using VSP.Player.Interfaces;

namespace VSP.Player.Pipeline;

/// <summary>
/// IMediaClock for RecordedFile sources (per ADR-002, "Seek ... meaningful for RecordedFile
/// sources"). Unlike <see cref="MediaClock"/> (Live, Seek always unsupported), this makes Seek
/// real -- delegated to an injected callback so this class carries no FFmpeg/session dependency
/// of its own; PlaybackController wires the callback to the open session's seek + a decoder
/// flush.
/// </summary>
public sealed class PlaybackClock : IMediaClock
{
    private readonly Func<TimeSpan, TimeSpan?> _seekHandler;
    private readonly object _gate = new();
    private TimeSpan _currentPresentationTime;

    public PlaybackClock(Func<TimeSpan, TimeSpan?> seekHandler)
    {
        _seekHandler = seekHandler ?? throw new ArgumentNullException(nameof(seekHandler));
    }

    public TimeSpan CurrentPresentationTime
    {
        get { lock (_gate) { return _currentPresentationTime; } }
    }

    public double PlaybackRate => 1.0;

    public TimeSpan? Seek(TimeSpan target)
    {
        var achieved = _seekHandler(target);
        if (achieved.HasValue)
        {
            Advance(achieved.Value);
        }

        return achieved;
    }

    public void Advance(TimeSpan presentationTimestamp)
    {
        lock (_gate)
        {
            _currentPresentationTime = presentationTimestamp;
        }
    }
}
