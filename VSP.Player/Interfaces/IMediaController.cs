using VSP.Player.Entities;

namespace VSP.Player.Interfaces;

/// <summary>
/// The one surface a ViewModel touches. Every other pipeline contract stays internal to VSP.Player.
/// </summary>
public interface IMediaController : IDisposable
{
    MediaControllerState State { get; }

    MediaSessionStatistics Statistics { get; }

    IFrameRenderer Renderer { get; }

    IDispatcherMetrics DispatcherMetrics { get; }

    /// <summary>
    /// Timestamp/seek surface (per ADR-002). Live sources never make <see cref="IMediaClock.Seek"/>
    /// meaningful; a RecordedFile-backed implementation does.
    /// </summary>
    IMediaClock Clock { get; }

    /// <summary>Total length of the source, when known. Null for Live (no fixed duration).</summary>
    TimeSpan? Duration { get; }

    bool IsRecording { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync();

    Task PauseAsync();

    Task ResumeAsync();

    /// <summary>
    /// Starts continuous, encoded-tier, stream-copy recording (per ADR-002/Epic-011). Requires
    /// the controller to be Connected; independent of Pause/Resume, which only affect the
    /// renderer.
    /// </summary>
    Task StartRecordingAsync();

    Task StopRecordingAsync();

    event EventHandler<MediaControllerStateChangedEventArgs>? StateChanged;
}
