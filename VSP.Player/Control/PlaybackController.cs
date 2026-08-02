using System.Windows.Threading;
using VSP.Player.Decoder;
using VSP.Player.Entities;
using VSP.Player.Interfaces;
using VSP.Player.Pipeline;
using VSP.Player.Renderer;

namespace VSP.Player.Control;

/// <summary>
/// IMediaController for a recorded file (Epic-012, Playback Foundation). A second, small
/// implementation of the same unchanged interface MediaController implements for Live -- not a
/// modification of MediaController's reconnect-loop state machine, which is the wrong shape for
/// "open a finished file, seek around in it, stop" (no reconnect, no encoded-tier recording
/// dispatch, natural completion at end-of-file). Reuses the decoded-tier FrameDispatcher and
/// WpfFrameRenderer exactly as MediaController does -- both are already source-agnostic.
/// </summary>
public sealed class PlaybackController : IMediaController
{
    private readonly string _filePath;
    private readonly FrameDispatcher<DecodedFrame> _dispatcher = new();
    private readonly WpfFrameRenderer _renderer;
    private readonly PlaybackClock _clock;

    private readonly Func<string, IMediaSession> _sessionFactory;
    private readonly Func<IMediaSession, IVideoDecoder> _decoderFactory;

    private readonly object _gate = new();
    private MediaControllerState _state = MediaControllerState.Idle;
    private MediaError? _lastError;

    // volatile: written on the open/read thread, read from HandlePacketReceived (session's own
    // read-loop thread) and from PauseAsync/ResumeAsync/PerformSeek (caller's thread) -- same
    // reasoning as MediaController's _session/_decoder fields.
    private volatile IMediaSession? _session;
    private volatile IVideoDecoder? _decoder;

    private IDisposable? _rendererSubscription;
    private CancellationTokenSource? _controllerCts;
    private Task? _openTask;
    private bool _disposed;

    public PlaybackController(string filePath, Dispatcher uiDispatcher)
        : this(
            filePath,
            uiDispatcher,
            static path => new RecordedFileMediaSession(path),
            static session => new FfmpegVideoDecoder((IFfmpegDemuxSource)session))
    {
    }

    /// <summary>Test seam: mirrors MediaController's injectable-factory pattern.</summary>
    internal PlaybackController(
        string filePath,
        Dispatcher uiDispatcher,
        Func<string, IMediaSession> sessionFactory,
        Func<IMediaSession, IVideoDecoder> decoderFactory)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path must not be empty.", nameof(filePath));
        }

        _filePath = filePath;
        _sessionFactory = sessionFactory;
        _decoderFactory = decoderFactory;
        _renderer = new WpfFrameRenderer(uiDispatcher);
        _clock = new PlaybackClock(PerformSeek);
    }

    public MediaControllerState State
    {
        get { lock (_gate) { return _state; } }
    }

    /// <summary>
    /// Reconnect/connected-duration statistics aren't meaningful for a one-shot file session;
    /// deliberately not fabricated -- always <see cref="MediaSessionStatistics.Empty"/>.
    /// </summary>
    public MediaSessionStatistics Statistics => MediaSessionStatistics.Empty;

    public IFrameRenderer Renderer => _renderer;

    public IDispatcherMetrics DispatcherMetrics => _dispatcher.Metrics;

    public IMediaClock Clock => _clock;

    public TimeSpan? Duration { get; private set; }

    public bool IsRecording => false;

    public event EventHandler<MediaControllerStateChangedEventArgs>? StateChanged;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_gate)
        {
            if (_state is not (MediaControllerState.Idle or MediaControllerState.Disconnected or MediaControllerState.Error))
            {
                throw new InvalidOperationException($"Cannot start a controller in state {_state}.");
            }
        }

        _controllerCts?.Dispose();
        _controllerCts = new CancellationTokenSource();
        _rendererSubscription ??= _dispatcher.Subscribe(_renderer, BufferPolicy.DropOldestWhenFull);
        _renderer.Start();

        _openTask = Task.Run(() => OpenAndReadAsync(_controllerCts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _renderer.Stop();
        _controllerCts?.Cancel();

        if (_openTask is not null)
        {
            try
            {
                await _openTask.ConfigureAwait(false);
            }
            catch
            {
                // Failures during shutdown are already reflected via StateChanged/_lastError.
            }
        }

        SetState(MediaControllerState.Disconnected);
    }

    public Task PauseAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_gate)
        {
            if (_state != MediaControllerState.Connected)
            {
                throw new InvalidOperationException($"Cannot pause a controller in state {_state}.");
            }
        }

        (_session as IPlaybackControl)?.PauseReading();
        _renderer.Stop();
        SetState(MediaControllerState.Paused);
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_gate)
        {
            if (_state != MediaControllerState.Paused)
            {
                throw new InvalidOperationException($"Cannot resume a controller in state {_state}.");
            }
        }

        (_session as IPlaybackControl)?.ResumeReading();
        _renderer.Start();
        SetState(MediaControllerState.Connected);
        return Task.CompletedTask;
    }

    public Task StartRecordingAsync() => throw new NotSupportedException("Recording is not supported while playing back a recorded file.");

    public Task StopRecordingAsync() => throw new NotSupportedException("Recording is not supported while playing back a recorded file.");

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _controllerCts?.Cancel();

        try
        {
            _openTask?.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // best-effort during dispose
        }

        _rendererSubscription?.Dispose();
        _renderer.Dispose();
        _controllerCts?.Dispose();
    }

    private async Task OpenAndReadAsync(CancellationToken cancellationToken)
    {
        SetState(MediaControllerState.Connecting);

        var sessionEndedTcs = new TaskCompletionSource<MediaSessionState>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<MediaSessionStateChangedEventArgs> stateChangedHandler = (_, e) => HandleSessionStateChanged(e, sessionEndedTcs);

        var session = _sessionFactory(_filePath);
        session.PacketReceived += HandlePacketReceived;
        session.StateChanged += stateChangedHandler;
        _session = session;

        try
        {
            await session.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            CleanupSession(stateChangedHandler);
            return;
        }
        catch (Exception)
        {
            // The underlying MediaError was already captured via HandleSessionStateChanged.
            // Unlike MediaController, there is no reconnect loop here to eventually reach Error
            // after exhausting attempts -- a failed Open must transition to Error immediately.
            CleanupSession(stateChangedHandler);
            if (!cancellationToken.IsCancellationRequested)
            {
                SetState(MediaControllerState.Error);
            }

            return;
        }

        _decoder = _decoderFactory(session);
        Duration = (session as IPlaybackControl)?.Duration;

        SetState(MediaControllerState.Connected);

        MediaSessionState? finalSessionState = null;
        try
        {
            finalSessionState = await sessionEndedTcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Explicit Stop/Dispose -- fall through to cleanup below.
        }

        CleanupSession(stateChangedHandler);

        if (!cancellationToken.IsCancellationRequested)
        {
            SetState(finalSessionState == MediaSessionState.Faulted ? MediaControllerState.Error : MediaControllerState.Disconnected);
        }
    }

    private void CleanupSession(EventHandler<MediaSessionStateChangedEventArgs> stateChangedHandler)
    {
        var session = _session;
        if (session is not null)
        {
            session.PacketReceived -= HandlePacketReceived;
            session.StateChanged -= stateChangedHandler;
            try
            {
                session.CloseAsync().GetAwaiter().GetResult();
            }
            catch
            {
                // best-effort close during cleanup
            }

            session.Dispose();
            _session = null;
        }

        _decoder?.Dispose();
        _decoder = null;
    }

    private void HandlePacketReceived(object? sender, EncodedPacketReceivedEventArgs e)
    {
        try
        {
            var decoder = _decoder;
            var decoded = decoder?.Decode(e.Packet);
            if (decoded is not null)
            {
                _clock.Advance(decoded.Timestamp.PresentationTimestamp);
                _dispatcher.Dispatch(decoded);
            }
        }
        catch (Exception ex)
        {
            RecordError(new MediaError { Category = MediaErrorCategory.Decode, Message = ex.Message, Exception = ex });
        }
    }

    private void HandleSessionStateChanged(MediaSessionStateChangedEventArgs e, TaskCompletionSource<MediaSessionState> sessionEndedTcs)
    {
        if (e.State == MediaSessionState.Faulted && e.Error is not null)
        {
            RecordError(e.Error);
        }

        if (e.State is MediaSessionState.Faulted or MediaSessionState.Closed)
        {
            sessionEndedTcs.TrySetResult(e.State);
        }
    }

    /// <summary>
    /// The seek callback wired into <see cref="PlaybackClock"/>: seeks the open session (if it
    /// supports <see cref="IPlaybackControl"/>) and flushes the decoder's stale buffered state,
    /// mirroring the reset ADR-002 already anticipates for a seek.
    /// </summary>
    private TimeSpan? PerformSeek(TimeSpan target)
    {
        if (_session is not IPlaybackControl playbackControl)
        {
            return null;
        }

        var achieved = playbackControl.TrySeek(target);
        if (achieved.HasValue)
        {
            _decoder?.Reset();
        }

        return achieved;
    }

    private void RecordError(MediaError error)
    {
        lock (_gate)
        {
            _lastError = error;
        }
    }

    private void SetState(MediaControllerState state)
    {
        lock (_gate)
        {
            if (_state == state)
            {
                return;
            }

            _state = state;
        }

        MediaError? error;
        lock (_gate)
        {
            error = _lastError;
        }

        StateChanged?.Invoke(this, new MediaControllerStateChangedEventArgs { State = state, Error = state == MediaControllerState.Error ? error : null });
    }
}
