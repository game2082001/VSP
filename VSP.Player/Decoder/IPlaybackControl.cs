namespace VSP.Player.Decoder;

/// <summary>
/// Internal seam exposing playback-specific capability (seek, pause/resume of the read loop,
/// total duration) that only a file-backed <see cref="Interfaces.IMediaSession"/> supports.
/// <see cref="Control.PlaybackController"/> reaches this via a runtime type check on the
/// <see cref="Interfaces.IMediaSession"/> it holds -- the same pattern <c>MediaController</c>
/// already uses (its decoder factory casts to a concrete/session-specific type) to reach
/// capability beyond the base contract. Not part of the public ADR-002 contracts: a Live source
/// never implements this.
/// </summary>
internal interface IPlaybackControl
{
    /// <summary>Total length of the recording, when known.</summary>
    TimeSpan? Duration { get; }

    /// <summary>
    /// Seeks to the nearest keyframe at or before <paramref name="target"/>. Returns the achieved
    /// position, or null if the session isn't open or the seek failed.
    /// </summary>
    TimeSpan? TrySeek(TimeSpan target);

    /// <summary>Blocks the read loop before its next packet read -- the file position stops advancing.</summary>
    void PauseReading();

    /// <summary>Releases a read loop blocked by <see cref="PauseReading"/>.</summary>
    void ResumeReading();
}
