using System.IO;
using System.Windows.Threading;
using VSP.Core.Logging;
using VSP.Player.Decoder;
using VSP.Player.Entities;
using VSP.Player.Interfaces;
using VSP.Player.Pipeline;
using VSP.Player.Recording;
using VSP.Player.Renderer;

namespace VSP.Player.Control;

/// <summary>
/// The one surface a ViewModel touches (per ADR-002). Composes an FFmpeg-backed
/// <see cref="RtspMediaSession"/>, <see cref="FfmpegVideoDecoder"/>, the decoded-tier
/// <see cref="FrameDispatcher{TFrame}"/>, and a <see cref="WpfFrameRenderer"/>, and owns
/// bounded reconnect. No FFmpeg type is exposed through <see cref="IMediaController"/>.
/// </summary>
public sealed class MediaController : IMediaController
{
    private readonly string _rtspUrl;
    private readonly int _maxReconnectAttempts;
    private readonly TimeSpan _reconnectDelay;
    private readonly FrameDispatcher<DecodedFrame> _dispatcher = new();
    private readonly FrameDispatcher<EncodedFrame> _encodedDispatcher = new();
    private readonly WpfFrameRenderer _renderer;
    private readonly MediaClock _clock = new();
    private readonly Guid? _cameraId;

    private readonly object _gate = new();
    private MediaControllerState _state = MediaControllerState.Idle;
    private int _reconnectAttempts;
    private DateTimeOffset? _connectedSince;
    private TimeSpan _totalConnectedDuration;
    private MediaError? _lastError;
    private DateTimeOffset? _lastErrorAt;
    private bool _isRecording;
    private IRecordingSession? _recordingSession;
    private IDisposable? _recordingSubscription;
    private IDisposable? _recordingFileUsage;

    private readonly Func<string, IMediaSession> _sessionFactory;
    private readonly Func<IMediaSession, IVideoDecoder> _decoderFactory;
    private readonly Func<IMediaSession, IRecordingSession> _recordingSessionFactory;

    // volatile: written on the connection-loop thread, read from HandlePacketReceived/
    // HandleSessionStateChanged which run on the session's own read-loop/open threads.
    private volatile IMediaSession? _session;
    private volatile IVideoDecoder? _decoder;

    private IDisposable? _rendererSubscription;
    private CancellationTokenSource? _controllerCts;
    private Task? _connectionLoopTask;
    private volatile bool _explicitStopRequested;
    private bool _disposed;

    public MediaController(
        string rtspUrl,
        Dispatcher uiDispatcher,
        int maxReconnectAttempts = 5,
        TimeSpan? reconnectDelay = null,
        Guid? cameraId = null)
        : this(
            rtspUrl,
            uiDispatcher,
            static url => new RtspMediaSession(url),
            static session => new FfmpegVideoDecoder((RtspMediaSession)session),
            maxReconnectAttempts,
            reconnectDelay,
            recordingSessionFactory: null,
            cameraId: cameraId)
    {
    }

    /// <summary>
    /// Test seam: injects the session/decoder/recording factories so the reconnect state
    /// machine and recording lifecycle can be exercised against a fake <see cref="IMediaSession"/>
    /// without a real RTSP source. The public constructor above wires the real FFmpeg pairing.
    /// </summary>
    internal MediaController(
        string rtspUrl,
        Dispatcher uiDispatcher,
        Func<string, IMediaSession> sessionFactory,
        Func<IMediaSession, IVideoDecoder> decoderFactory,
        int maxReconnectAttempts = 5,
        TimeSpan? reconnectDelay = null,
        Func<IMediaSession, IRecordingSession>? recordingSessionFactory = null,
        Guid? cameraId = null)
    {
        if (string.IsNullOrWhiteSpace(rtspUrl))
        {
            throw new ArgumentException("RTSP URL must not be empty.", nameof(rtspUrl));
        }

        _rtspUrl = rtspUrl;
        _sessionFactory = sessionFactory;
        _decoderFactory = decoderFactory;
        _recordingSessionFactory = recordingSessionFactory ?? (static session => new FfmpegRecordingSession((RtspMediaSession)session));
        _maxReconnectAttempts = maxReconnectAttempts;
        _reconnectDelay = reconnectDelay ?? TimeSpan.FromSeconds(3);
        _renderer = new WpfFrameRenderer(uiDispatcher);
        _cameraId = cameraId;
    }

    public MediaControllerState State
    {
        get { lock (_gate) { return _state; } }
    }

    public MediaSessionStatistics Statistics
    {
        get
        {
            lock (_gate)
            {
                var connectedDuration = _totalConnectedDuration;
                if (_connectedSince.HasValue)
                {
                    connectedDuration += DateTimeOffset.UtcNow - _connectedSince.Value;
                }

                return new MediaSessionStatistics
                {
                    ConnectedSince = _connectedSince,
                    ReconnectAttempts = _reconnectAttempts,
                    LastErrorAt = _lastErrorAt,
                    LastError = _lastError,
                    TotalConnectedDuration = connectedDuration
                };
            }
        }
    }

    public IFrameRenderer Renderer => _renderer;

    public IDispatcherMetrics DispatcherMetrics => _dispatcher.Metrics;

    /// <summary>
    /// Present to satisfy <see cref="IMediaController"/> uniformly across implementations; never
    /// advanced for a Live source (no fixed timeline), so <see cref="IMediaClock.Seek"/> keeps
    /// returning null as already documented on <see cref="MediaClock"/>.
    /// </summary>
    public IMediaClock Clock => _clock;

    /// <summary>Live has no fixed duration.</summary>
    public TimeSpan? Duration => null;

    public bool IsRecording
    {
        get { lock (_gate) { return _isRecording; } }
    }

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

        _explicitStopRequested = false;
        _reconnectAttempts = 0;
        _controllerCts?.Dispose();
        _controllerCts = new CancellationTokenSource();
        _rendererSubscription ??= _dispatcher.Subscribe(_renderer, BufferPolicy.DropOldestWhenFull);
        _renderer.Start();

        _connectionLoopTask = Task.Run(() => ConnectionLoopAsync(_controllerCts.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _explicitStopRequested = true;
        _renderer.Stop();
        _controllerCts?.Cancel();

        if (_connectionLoopTask is not null)
        {
            try
            {
                await _connectionLoopTask.ConfigureAwait(false);
            }
            catch
            {
                // Failures during shutdown are already reflected in Statistics.LastError.
            }
        }

        await StopRecordingAsync().ConfigureAwait(false);

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

        _renderer.Start();
        SetState(MediaControllerState.Connected);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Starts continuous, encoded-tier, stream-copy recording of the active session. Subscribes
    /// at the encoded-tier Dispatcher independently of the renderer subscription, per ADR-002 --
    /// unaffected by Pause/Resume, which only start/stop the renderer.
    /// </summary>
    public async Task StartRecordingAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        IMediaSession? session;
        lock (_gate)
        {
            if (_state != MediaControllerState.Connected)
            {
                throw new InvalidOperationException($"Cannot start recording in state {_state}.");
            }

            if (_isRecording)
            {
                throw new InvalidOperationException("Recording has already been started.");
            }

            session = _session;
        }

        if (session is null)
        {
            throw new InvalidOperationException("No active media session to record.");
        }

        var recordingSession = _recordingSessionFactory(session);
        var filePath = BuildRecordingFilePath();
        var recordingFileUsage = RecordingFileUsageTracker.Shared.Register(filePath);

        try
        {
            await recordingSession.StartAsync(filePath, CancellationToken.None).ConfigureAwait(false);
            recordingSession.Start();
        }
        catch
        {
            recordingFileUsage.Dispose();
            recordingSession.Dispose();
            throw;
        }

        IDisposable subscription;
        try
        {
            subscription = _encodedDispatcher.Subscribe(recordingSession, BufferPolicy.BlockProducerWhenFull);
        }
        catch
        {
            recordingFileUsage.Dispose();
            recordingSession.Dispose();
            throw;
        }

        lock (_gate)
        {
            _recordingSession = recordingSession;
            _recordingSubscription = subscription;
            _recordingFileUsage = recordingFileUsage;
            _isRecording = true;
        }
    }

    /// <summary>No-op when not currently recording, so StopAsync/Dispose can call this unconditionally.</summary>
    public async Task StopRecordingAsync()
    {
        IRecordingSession? recordingSession;
        IDisposable? subscription;
        IDisposable? recordingFileUsage;

        lock (_gate)
        {
            if (!_isRecording || _recordingSession is null)
            {
                return;
            }

            recordingSession = _recordingSession;
            subscription = _recordingSubscription;
            recordingFileUsage = _recordingFileUsage;
            _recordingSession = null;
            _recordingSubscription = null;
            _recordingFileUsage = null;
            _isRecording = false;
        }

        subscription?.Dispose();
        try
        {
            await recordingSession!.StopAsync().ConfigureAwait(false);
        }
        finally
        {
            recordingSession.Dispose();
            recordingFileUsage?.Dispose();
        }
    }

    /// <summary>
    /// Recordings are organized per camera (Epic-012) so Playback can later list "this camera's
    /// recordings" -- falls back to the flat root when no camera id was supplied (e.g. existing
    /// tests that construct MediaController without one).
    /// </summary>
    private string BuildRecordingFilePath()
    {
        var root = _cameraId.HasValue
            ? RecordingPathProvider.GetCameraRecordingDirectory(_cameraId.Value)
            : RecordingPathProvider.GetRecordingRoot();
        var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.mp4";
        return Path.Combine(root, fileName);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _explicitStopRequested = true;
        _controllerCts?.Cancel();

        try
        {
            _connectionLoopTask?.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // best-effort during dispose
        }

        try
        {
            StopRecordingAsync().GetAwaiter().GetResult();
        }
        catch
        {
            // best-effort during dispose
        }

        _rendererSubscription?.Dispose();
        _renderer.Dispose();
        _controllerCts?.Dispose();
    }

    private async Task ConnectionLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            SetState(_reconnectAttempts == 0 ? MediaControllerState.Connecting : MediaControllerState.Reconnecting);

            // Captured locally (not a shared field) so a late-arriving event from an old,
            // just-cleaned-up session can never complete a *different* iteration's completion
            // source — each iteration's handler closes over its own instance.
            var sessionEndedTcs = new TaskCompletionSource<MediaSessionState>(TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<MediaSessionStateChangedEventArgs> stateChangedHandler = (_, e) => HandleSessionStateChanged(e, sessionEndedTcs);

            var session = _sessionFactory(_rtspUrl);
            session.PacketReceived += HandlePacketReceived;
            session.StateChanged += stateChangedHandler;
            _session = session;

            var opened = false;
            try
            {
                await session.OpenAsync(cancellationToken).ConfigureAwait(false);
                opened = true;
            }
            catch (OperationCanceledException)
            {
                CleanupCurrentSession(stateChangedHandler);
                break;
            }
            catch (Exception ex)
            {
                // The underlying MediaError was already captured via HandleSessionStateChanged.
                // _reconnectAttempts increments further down, after cleanup -- +1 here names the attempt that just failed.
                AppLog.Warning($"Live View reconnect attempt {_reconnectAttempts + 1} failed for camera {_cameraId}.", ex);
            }

            if (opened)
            {
                _decoder = _decoderFactory(session);

                lock (_gate)
                {
                    _connectedSince = DateTimeOffset.UtcNow;
                    _reconnectAttempts = 0;
                }

                SetState(MediaControllerState.Connected);

                try
                {
                    await sessionEndedTcs.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // fall through to cleanup below
                }

                lock (_gate)
                {
                    if (_connectedSince.HasValue)
                    {
                        _totalConnectedDuration += DateTimeOffset.UtcNow - _connectedSince.Value;
                        _connectedSince = null;
                    }
                }
            }

            CleanupCurrentSession(stateChangedHandler);

            if (cancellationToken.IsCancellationRequested || _explicitStopRequested)
            {
                break;
            }

            lock (_gate)
            {
                _reconnectAttempts++;
            }

            if (_reconnectAttempts > _maxReconnectAttempts)
            {
                SetState(MediaControllerState.Error);
                return;
            }

            SetState(MediaControllerState.Reconnecting);

            try
            {
                await Task.Delay(_reconnectDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        if (!_explicitStopRequested)
        {
            SetState(MediaControllerState.Disconnected);
        }
    }

    private void CleanupCurrentSession(EventHandler<MediaSessionStateChangedEventArgs> stateChangedHandler)
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
        // Independent of decode, per ADR-002: Recording attaches at the encoded tier and must
        // not depend on decode succeeding or a Renderer being attached.
        _encodedDispatcher.Dispatch(e.Packet);

        try
        {
            var decoder = _decoder;
            var decoded = decoder?.Decode(e.Packet);
            if (decoded is not null)
            {
                _dispatcher.Dispatch(decoded);
            }
        }
        catch (Exception ex)
        {
            // Item F evidence collection: this exception was previously only ever visible via
            // Statistics.LastError, which nothing in VSP.UI currently reads while the controller
            // stays Connected -- making a persistently-throwing decode/conversion path silently
            // invisible end-to-end. Logging it here does not change the swallow-and-continue
            // behavior (still required to keep a live session alive past one bad frame).
            AppLog.Warning($"Live View decode/conversion exception for camera {_cameraId}: {ex.Message}", ex);
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

    private void RecordError(MediaError error)
    {
        lock (_gate)
        {
            _lastError = error;
            _lastErrorAt = DateTimeOffset.UtcNow;
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
