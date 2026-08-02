namespace VSP.Player.Interfaces;

public interface IMediaClock
{
    TimeSpan CurrentPresentationTime { get; }

    double PlaybackRate { get; }

    /// <summary>Meaningful for RecordedFile sources. Live sources return null (unsupported).</summary>
    TimeSpan? Seek(TimeSpan target);

    void Advance(TimeSpan presentationTimestamp);
}
