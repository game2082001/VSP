using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;
using FFmpeg.AutoGen.Bindings.DynamicallyLinked;
using VSP.Core.Logging;
using VSP.Player.Entities;
using VSP.Player.Interfaces;

namespace VSP.Player.Decoder;

/// <summary>
/// FFmpeg-backed <see cref="VideoSourceKind.RecordedFile"/> implementation of
/// <see cref="IMediaSession"/> (Epic-012, closing ADR-002's v3 Playback row). Mirrors
/// <see cref="RtspMediaSession"/>'s open/read-loop/dispose shape and native-call locking pattern
/// (deliberately -- avformat_open_input is protocol-agnostic, so the two classes share almost the
/// same native call sequence), but differs where a file genuinely needs to: no RTSP transport
/// options, a real-time-paced read loop with a pause gate (so "Pause" actually stops the file
/// position from advancing, not just the renderer), a natural EOF-&gt;Closed transition instead of
/// Faulted, and <see cref="IPlaybackControl"/> for seek/duration. RtspMediaSession itself is left
/// untouched -- this is a second, small implementation of the unchanged IMediaSession contract,
/// not a modification of the already-shipped Live path.
/// </summary>
public sealed class RecordedFileMediaSession : IMediaSession, IFfmpegDemuxSource, IPlaybackControl
{
    private readonly string _filePath;

    // Kept alive for the session's lifetime: the native interrupt_callback holds a raw function
    // pointer derived from this delegate, so it must not become eligible for collection.
    private readonly unsafe AVIOInterruptCB_callback _interruptCallback;

    private readonly object _stateGate = new();
    private MediaSessionState _state = MediaSessionState.Idle;

    // Guards every native call that touches _formatContext, matching RtspMediaSession's pattern:
    // CloseFormatContext()/TrySeek() take this same lock before touching it, so a seek can never
    // race a read, and Dispose can never free it mid-read.
    private readonly object _nativeGate = new();

    private unsafe AVFormatContext* _formatContext;
    private int _videoStreamIndex = -1;
    private TimeSpan? _duration;
    private volatile bool _interruptRequested;
    private CancellationTokenSource? _readLoopCts;
    private Task? _readLoopTask;

    // SEC-3 (Task 3A): mirrors RtspMediaSession's identical fields/logic -- see
    // EncodedPacketGuard for the bound/threshold rationale shared by both sessions. Only ever
    // touched from within ReadLoop's single thread (production) or by a focused test driving a
    // freshly-constructed, never-opened session directly.
    private int _consecutiveRejectedPackets;
    private long _lastRejectedPacketLogTickCount = long.MinValue;
    private const long RejectedPacketLogIntervalMilliseconds = 5 * 60 * 1000;

    // Pause gate: signaled (open) = reading; reset (closed) = the loop blocks before its next
    // packet read. Unlike Live's Pause (renderer-only), a paused file must stop advancing.
    private readonly ManualResetEventSlim _playGate = new(initialState: true);

    // Local-delta pacing: sleeps by the gap between consecutive packets' presentation timestamps
    // rather than tracking an absolute wall-clock baseline, so a seek or a pause/resume needs no
    // baseline recomputation -- just clear this back to null and the very next packet is
    // delivered immediately, with pacing resuming naturally from there.
    private TimeSpan? _lastDeliveredPts;
    private static readonly TimeSpan MaxPacingChunk = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan MaxPacingGap = TimeSpan.FromSeconds(2);

    public unsafe RecordedFileMediaSession(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path must not be empty.", nameof(filePath));
        }

        _filePath = filePath;
        _interruptCallback = InterruptCallback;

        FfmpegNativeLibraryLoader.EnsureInitialized();
    }

    public VideoSourceKind Kind => VideoSourceKind.RecordedFile;

    public MediaSessionState State
    {
        get { lock (_stateGate) { return _state; } }
    }

    TimeSpan? IPlaybackControl.Duration
    {
        get { lock (_nativeGate) { return _duration; } }
    }

    public event EventHandler<MediaSessionStateChangedEventArgs>? StateChanged;

    public event EventHandler<EncodedPacketReceivedEventArgs>? PacketReceived;

    public async Task OpenAsync(CancellationToken cancellationToken)
    {
        var current = State;
        if (current is not (MediaSessionState.Idle or MediaSessionState.Closed or MediaSessionState.Faulted))
        {
            throw new InvalidOperationException($"Cannot open a session in state {current}.");
        }

        _interruptRequested = false;
        TransitionTo(MediaSessionState.Opening);

        using var cancellationRegistration = cancellationToken.Register(() => _interruptRequested = true);

        try
        {
            await Task.Run(OpenFormatContext, cancellationToken).ConfigureAwait(false);
        }
        catch (MediaSessionOpenException ex)
        {
            TransitionTo(MediaSessionState.Faulted, ex.Error);
            throw;
        }

        TransitionTo(MediaSessionState.Open);

        _playGate.Set();
        _readLoopCts = new CancellationTokenSource();
        _readLoopTask = Task.Run(() => ReadLoop(_readLoopCts.Token));
    }

    public async Task CloseAsync()
    {
        _interruptRequested = true;
        _playGate.Set();
        _readLoopCts?.Cancel();

        if (_readLoopTask is not null)
        {
            try
            {
                await _readLoopTask.ConfigureAwait(false);
            }
            catch
            {
                // Read loop failures are already surfaced via StateChanged.
            }
        }

        CloseFormatContext();
        TransitionTo(MediaSessionState.Closed);
    }

    public void Dispose()
    {
        _interruptRequested = true;
        _playGate.Set();
        _readLoopCts?.Cancel();
        try
        {
            _readLoopTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // best-effort during dispose
        }

        CloseFormatContext();
        _readLoopCts?.Dispose();
        _playGate.Dispose();
    }

    /// <summary>Internal seam: exposes codec parameters to FfmpegVideoDecoder only.</summary>
    internal unsafe AVCodecParameters* GetVideoCodecParameters()
    {
        lock (_nativeGate)
        {
            if (_formatContext is null || _videoStreamIndex < 0)
            {
                return null;
            }

            return _formatContext->streams[_videoStreamIndex]->codecpar;
        }
    }

    internal unsafe AVRational GetVideoStreamTimeBase()
    {
        lock (_nativeGate)
        {
            if (_formatContext is null || _videoStreamIndex < 0)
            {
                return new AVRational { num = 0, den = 1 };
            }

            return _formatContext->streams[_videoStreamIndex]->time_base;
        }
    }

    unsafe AVCodecParameters* IFfmpegDemuxSource.GetVideoCodecParameters() => GetVideoCodecParameters();

    unsafe AVRational IFfmpegDemuxSource.GetVideoStreamTimeBase() => GetVideoStreamTimeBase();

    void IPlaybackControl.PauseReading() => _playGate.Reset();

    void IPlaybackControl.ResumeReading() => _playGate.Set();

    unsafe TimeSpan? IPlaybackControl.TrySeek(TimeSpan target)
    {
        lock (_nativeGate)
        {
            if (_formatContext is null || _videoStreamIndex < 0)
            {
                return null;
            }

            var timeBase = _formatContext->streams[_videoStreamIndex]->time_base;
            if (timeBase.den == 0)
            {
                return null;
            }

            var targetTimestamp = (long)(target.TotalSeconds / ffmpeg.av_q2d(timeBase));

            var seekResult = DynamicallyLinkedBindings.av_seek_frame(
                _formatContext, _videoStreamIndex, targetTimestamp, ffmpeg.AVSEEK_FLAG_BACKWARD);

            if (seekResult < 0)
            {
                return null;
            }

            // Next packet is delivered without a pacing delay -- there is no meaningful "gap"
            // from whatever played before the seek.
            _lastDeliveredPts = null;
            return target;
        }
    }

    private unsafe void OpenFormatContext()
    {
        var formatContext = DynamicallyLinkedBindings.avformat_alloc_context();
        if (formatContext is null)
        {
            throw new MediaSessionOpenException(new MediaError
            {
                Category = MediaErrorCategory.Unknown,
                Message = "Failed to allocate AVFormatContext."
            });
        }

        formatContext->interrupt_callback.callback = _interruptCallback;
        formatContext->interrupt_callback.opaque = null;

        int openResult = DynamicallyLinkedBindings.avformat_open_input(&formatContext, _filePath, null, null);

        if (openResult < 0)
        {
            throw new MediaSessionOpenException(
                FfmpegErrorTranslator.FromResult(MediaErrorCategory.Connection, openResult, "Failed to open recorded file"));
        }

        int streamInfoResult = DynamicallyLinkedBindings.avformat_find_stream_info(formatContext, null);
        if (streamInfoResult < 0)
        {
            DynamicallyLinkedBindings.avformat_close_input(&formatContext);
            throw new MediaSessionOpenException(
                FfmpegErrorTranslator.FromResult(MediaErrorCategory.Protocol, streamInfoResult, "Failed to read recorded file stream information"));
        }

        var videoStreamIndex = -1;
        for (var i = 0u; i < formatContext->nb_streams; i++)
        {
            if (formatContext->streams[i]->codecpar->codec_type == AVMediaType.AVMEDIA_TYPE_VIDEO)
            {
                videoStreamIndex = (int)i;
                break;
            }
        }

        if (videoStreamIndex < 0)
        {
            DynamicallyLinkedBindings.avformat_close_input(&formatContext);
            throw new MediaSessionOpenException(new MediaError
            {
                Category = MediaErrorCategory.Protocol,
                Message = "No video stream found in recorded file."
            });
        }

        lock (_nativeGate)
        {
            _formatContext = formatContext;
            _videoStreamIndex = videoStreamIndex;
            _duration = formatContext->duration > 0
                ? TimeSpan.FromSeconds(formatContext->duration / (double)ffmpeg.AV_TIME_BASE)
                : null;
        }
    }

    private unsafe void ReadLoop(CancellationToken cancellationToken)
    {
        var packet = DynamicallyLinkedBindings.av_packet_alloc();
        var cancellationHandle = cancellationToken.WaitHandle;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (WaitHandle.WaitAny(new[] { _playGate.WaitHandle, cancellationHandle }) == 1)
                {
                    return;
                }

                int readResult;
                bool isEof;
                MediaError? packetError = null;

                lock (_nativeGate)
                {
                    if (_formatContext is null)
                    {
                        // Closed concurrently between iterations; nothing left to read.
                        return;
                    }

                    readResult = DynamicallyLinkedBindings.av_read_frame(_formatContext, packet);
                    isEof = readResult == ffmpeg.AVERROR_EOF;

                    if (readResult >= 0)
                    {
                        try
                        {
                            if (packet->stream_index == _videoStreamIndex)
                            {
                                packetError = ProcessReceivedPacket(packet, cancellationToken);
                            }
                        }
                        finally
                        {
                            DynamicallyLinkedBindings.av_packet_unref(packet);
                        }
                    }
                }

                // SEC-3 (Task 3A): see RtspMediaSession.ReadLoop's identical comment -- a genuine
                // packet-processing failure faults the session through the same path an
                // av_read_frame failure already used below, so MediaController's existing
                // reconnect logic activates without any change to MediaController itself.
                if (packetError is not null)
                {
                    if (!_interruptRequested && !cancellationToken.IsCancellationRequested)
                    {
                        TransitionTo(MediaSessionState.Faulted, packetError);
                    }

                    return;
                }

                if (readResult < 0)
                {
                    if (isEof)
                    {
                        TransitionTo(MediaSessionState.Closed);
                    }
                    else if (!_interruptRequested && !cancellationToken.IsCancellationRequested)
                    {
                        TransitionTo(MediaSessionState.Faulted,
                            FfmpegErrorTranslator.FromResult(MediaErrorCategory.Protocol, readResult, "Recorded file read failed"));
                    }

                    return;
                }
            }
        }
        finally
        {
            DynamicallyLinkedBindings.av_packet_free(&packet);
        }
    }

    /// <summary>
    /// Sleeps (in bounded chunks, so cancellation/Dispose stays responsive) by the gap between
    /// this packet's presentation timestamp and the previous one, so playback proceeds at
    /// roughly real-time pace instead of "as fast as disk I/O allows". Called while _nativeGate
    /// is held, deliberately: a seek from another thread also takes that lock, so it can never
    /// interleave mid-pace with a stale read in flight.
    /// </summary>
    private unsafe void PaceAndRaisePacketReceived(AVPacket* packet, CancellationToken cancellationToken)
    {
        var timeBase = _formatContext->streams[_videoStreamIndex]->time_base;

        var pts = packet->pts == ffmpeg.AV_NOPTS_VALUE ? packet->dts : packet->pts;
        var dts = packet->dts == ffmpeg.AV_NOPTS_VALUE ? pts : packet->dts;

        var presentationTimestamp = ToTimeSpan(pts, timeBase);
        var decodeTimestamp = ToTimeSpan(dts, timeBase);

        if (_lastDeliveredPts.HasValue && presentationTimestamp > _lastDeliveredPts.Value)
        {
            var gap = presentationTimestamp - _lastDeliveredPts.Value;
            if (gap < MaxPacingGap)
            {
                SleepInChunks(gap, cancellationToken);
            }
        }

        _lastDeliveredPts = presentationTimestamp;

        var data = new byte[packet->size];
        if (packet->size > 0)
        {
            Marshal.Copy((IntPtr)packet->data, data, 0, packet->size);
        }

        var encodedFrame = new EncodedFrame
        {
            Data = data,
            Timestamp = new FrameTimestamp
            {
                PresentationTimestamp = presentationTimestamp,
                DecodeTimestamp = decodeTimestamp
            },
            IsKeyFrame = (packet->flags & ffmpeg.AV_PKT_FLAG_KEY) != 0
        };

        PacketReceived?.Invoke(this, new EncodedPacketReceivedEventArgs { Packet = encodedFrame });
    }

    /// <summary>
    /// SEC-3 (Task 3A): guards <see cref="PaceAndRaisePacketReceived"/>'s managed allocation
    /// against an oversized/negative encoded packet size before it happens (and before pacing
    /// sleeps for a packet that will be dropped anyway), and against any other exception dispatch
    /// itself might throw. Mirrors RtspMediaSession.ProcessReceivedPacket exactly.
    /// </summary>
    private unsafe MediaError? ProcessReceivedPacket(AVPacket* packet, CancellationToken cancellationToken)
    {
        switch (EvaluateAndTrackPacketSize(packet->size, out var sizeFault))
        {
            case PacketSizeDecision.Fault:
                return sizeFault;

            case PacketSizeDecision.Drop:
                return null;

            default:
                try
                {
                    PaceAndRaisePacketReceived(packet, cancellationToken);
                    return null;
                }
                catch (Exception ex)
                {
                    return new MediaError
                    {
                        Category = MediaErrorCategory.Decode,
                        Message = $"Encoded packet processing failed: {ex.Message}",
                        Exception = ex
                    };
                }
        }
    }

    /// <summary>
    /// SEC-3 (Task 3A): mirrors RtspMediaSession.EvaluateAndTrackPacketSize exactly -- see that
    /// method's doc comment for the full rationale. faultError is non-null if and only if the
    /// decision is Fault.
    /// </summary>
    internal PacketSizeDecision EvaluateAndTrackPacketSize(int packetSize, out MediaError? faultError)
    {
        faultError = null;

        if (EncodedPacketGuard.IsPacketSizeAcceptable(packetSize))
        {
            _consecutiveRejectedPackets = 0;
            return PacketSizeDecision.Accept;
        }

        _consecutiveRejectedPackets++;
        LogRejectedPacketIfDue(packetSize, _consecutiveRejectedPackets);

        if (!EncodedPacketGuard.ShouldEscalateToFault(_consecutiveRejectedPackets))
        {
            return PacketSizeDecision.Drop;
        }

        faultError = new MediaError
        {
            Category = MediaErrorCategory.Protocol,
            Message = $"Rejected {_consecutiveRejectedPackets} consecutive encoded packets outside the accepted " +
                      $"size bound (0..{EncodedPacketGuard.MaxEncodedPacketSizeBytes} bytes); treating the session as faulted."
        };
        return PacketSizeDecision.Fault;
    }

    /// <summary>Mirrors RtspMediaSession.LogRejectedPacketIfDue exactly -- see that method's doc
    /// comment for the rate-limiting rationale. Never logs a credential or URI.</summary>
    private void LogRejectedPacketIfDue(int size, int consecutiveCount)
    {
        var now = Environment.TickCount64;
        var mustLog = _lastRejectedPacketLogTickCount == long.MinValue
            || FfmpegVideoDecoder.IsDiagnosticsIntervalElapsed(_lastRejectedPacketLogTickCount, now, RejectedPacketLogIntervalMilliseconds)
            || EncodedPacketGuard.ShouldEscalateToFault(consecutiveCount);

        if (!mustLog)
        {
            return;
        }

        _lastRejectedPacketLogTickCount = now;
        AppLog.Warning(
            $"Playback: rejected an encoded packet with size={size} bytes (consecutive rejections={consecutiveCount}); " +
            $"accepted range is 0..{EncodedPacketGuard.MaxEncodedPacketSizeBytes} bytes.");
    }

    private static void SleepInChunks(TimeSpan delay, CancellationToken cancellationToken)
    {
        var remaining = delay;
        while (remaining > TimeSpan.Zero)
        {
            var chunk = remaining < MaxPacingChunk ? remaining : MaxPacingChunk;
            if (cancellationToken.WaitHandle.WaitOne(chunk))
            {
                return;
            }

            remaining -= chunk;
        }
    }

    private static unsafe TimeSpan ToTimeSpan(long timestamp, AVRational timeBase)
    {
        if (timestamp == ffmpeg.AV_NOPTS_VALUE || timeBase.den == 0)
        {
            return TimeSpan.Zero;
        }

        return TimeSpan.FromSeconds(timestamp * ffmpeg.av_q2d(timeBase));
    }

    private unsafe void CloseFormatContext()
    {
        lock (_nativeGate)
        {
            if (_formatContext is not null)
            {
                var formatContext = _formatContext;
                DynamicallyLinkedBindings.avformat_close_input(&formatContext);
                _formatContext = null;
            }

            _videoStreamIndex = -1;
        }
    }

    private unsafe int InterruptCallback(void* opaque) => _interruptRequested ? 1 : 0;

    private void TransitionTo(MediaSessionState state, MediaError? error = null)
    {
        lock (_stateGate)
        {
            if (_state == state)
            {
                return;
            }

            _state = state;
        }

        StateChanged?.Invoke(this, new MediaSessionStateChangedEventArgs { State = state, Error = error });
    }
}
