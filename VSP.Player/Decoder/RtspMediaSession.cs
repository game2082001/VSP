using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Abstractions;
using FFmpeg.AutoGen.Bindings.DynamicallyLinked;
using VSP.Core.Logging;
using VSP.Player.Entities;
using VSP.Player.Interfaces;

namespace VSP.Player.Decoder;

/// <summary>
/// FFmpeg-backed live RTSP implementation of <see cref="IMediaSession"/>. No FFmpeg type is
/// exposed through the interface; <see cref="GetVideoCodecParameters"/> and
/// <see cref="GetVideoStreamTimeBase"/> are internal-only seams used by the paired concrete
/// FFmpeg consumers within this assembly (<c>FfmpegVideoDecoder</c>, <c>FfmpegRecordingSession</c>)
/// that need access to source stream state beyond the neutral <see cref="IMediaSession"/> contract.
/// </summary>
public sealed class RtspMediaSession : IMediaSession, IFfmpegDemuxSource
{
    private readonly string _rtspUrl;
    private readonly TimeSpan _openTimeout;

    // Kept alive for the session's lifetime: the native interrupt_callback holds a raw function
    // pointer derived from this delegate, so it must not become eligible for collection.
    private readonly unsafe AVIOInterruptCB_callback _interruptCallback;

    private readonly object _stateGate = new();
    private MediaSessionState _state = MediaSessionState.Idle;

    // Guards every native call that touches _formatContext: CloseFormatContext() takes this same
    // lock before freeing/nulling it, so it can never run concurrently with a read in flight.
    private readonly object _nativeGate = new();

    private unsafe AVFormatContext* _formatContext;
    private int _videoStreamIndex = -1;
    private volatile bool _interruptRequested;
    private CancellationTokenSource? _readLoopCts;
    private Task? _readLoopTask;

    // SEC-3 (Task 3A): only ever touched from within ReadLoop's single thread (production) or by
    // a focused test driving a freshly-constructed, never-opened session directly -- never needs
    // its own lock. See EncodedPacketGuard for the bound/threshold rationale.
    private readonly PacketRejectionTracker _packetRejectionTracker = new("Live View RTSP");

    public unsafe RtspMediaSession(string rtspUrl, TimeSpan? openTimeout = null)
    {
        if (string.IsNullOrWhiteSpace(rtspUrl))
        {
            throw new ArgumentException("RTSP URL must not be empty.", nameof(rtspUrl));
        }

        _rtspUrl = rtspUrl;
        _openTimeout = openTimeout ?? TimeSpan.FromSeconds(5);
        _interruptCallback = InterruptCallback;

        FfmpegNativeLibraryLoader.EnsureInitialized();
    }

    public VideoSourceKind Kind => VideoSourceKind.Live;

    public MediaSessionState State
    {
        get { lock (_stateGate) { return _state; } }
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

        // Without this, an external cancellationToken has no way to interrupt the blocking
        // native open call in progress on the Task.Run below: OpenFormatContext never observes
        // the token itself, and _interruptRequested was previously only ever set by CloseAsync/
        // Dispose, which can't run until this call returns — a chicken-and-egg deadlock-adjacent
        // hang. Registering here wires external cancellation into the already-present
        // AVIOInterruptCB so the native call actually aborts promptly.
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

        _readLoopCts = new CancellationTokenSource();
        _readLoopTask = Task.Run(() => ReadLoop(_readLoopCts.Token));
    }

    public async Task CloseAsync()
    {
        _interruptRequested = true;
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
    }

    /// <summary>Internal seam: exposes codec parameters to paired FFmpeg consumers only.</summary>
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

    /// <summary>
    /// Internal seam: the source stream's time_base, needed by <c>FfmpegRecordingSession</c> to
    /// rescale packet timestamps into the output container's stream time_base at write time.
    /// </summary>
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

    // Explicit implementation: keeps the two internal methods above (already used directly by
    // FfmpegRecordingSession against the concrete type) untouched, while letting
    // FfmpegVideoDecoder depend on the neutral IFfmpegDemuxSource seam instead.
    unsafe AVCodecParameters* IFfmpegDemuxSource.GetVideoCodecParameters() => GetVideoCodecParameters();

    unsafe AVRational IFfmpegDemuxSource.GetVideoStreamTimeBase() => GetVideoStreamTimeBase();

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

        AVDictionary* options = null;
        DynamicallyLinkedBindings.av_dict_set(&options, "rtsp_transport", "tcp", 0);
        DynamicallyLinkedBindings.av_dict_set(&options, "stimeout", ((long)_openTimeout.TotalMilliseconds * 1000).ToString(), 0);

        int openResult = DynamicallyLinkedBindings.avformat_open_input(&formatContext, _rtspUrl, null, &options);
        DynamicallyLinkedBindings.av_dict_free(&options);

        if (openResult < 0)
        {
            throw new MediaSessionOpenException(
                FfmpegErrorTranslator.FromResult(MediaErrorCategory.Connection, openResult, "Failed to open RTSP source"));
        }

        int streamInfoResult = DynamicallyLinkedBindings.avformat_find_stream_info(formatContext, null);
        if (streamInfoResult < 0)
        {
            DynamicallyLinkedBindings.avformat_close_input(&formatContext);
            throw new MediaSessionOpenException(
                FfmpegErrorTranslator.FromResult(MediaErrorCategory.Protocol, streamInfoResult, "Failed to read RTSP stream information"));
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
                Message = "No video stream found in RTSP source."
            });
        }

        LogSelectedStreamDiagnostics(formatContext, videoStreamIndex);

        lock (_nativeGate)
        {
            _formatContext = formatContext;
            _videoStreamIndex = videoStreamIndex;
        }
    }

    /// <summary>
    /// One-time, startup-only diagnostic (Item F evidence collection): records exactly what the
    /// real source reported for the selected video stream -- codec, dimensions, pixel format --
    /// so a real-device retest can show what codec/geometry is actually in play without needing
    /// per-packet logging.
    /// </summary>
    private static unsafe void LogSelectedStreamDiagnostics(AVFormatContext* formatContext, int videoStreamIndex)
    {
        var codecpar = formatContext->streams[videoStreamIndex]->codecpar;
        var codecName = DynamicallyLinkedBindings.avcodec_get_name(codecpar->codec_id);
        var pixelFormatName = DynamicallyLinkedBindings.av_get_pix_fmt_name((AVPixelFormat)codecpar->format) ?? "unknown";

        AppLog.Info(
            $"Live View RTSP stream opened: video stream index={videoStreamIndex}, codec={codecName} " +
            $"({codecpar->codec_id}), declared {codecpar->width}x{codecpar->height}, pixfmt={pixelFormatName}.");
    }

    private unsafe void ReadLoop(CancellationToken cancellationToken)
    {
        var packet = DynamicallyLinkedBindings.av_packet_alloc();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int readResult;
                MediaError? packetError = null;

                lock (_nativeGate)
                {
                    if (_formatContext is null)
                    {
                        // Closed concurrently between iterations; nothing left to read.
                        return;
                    }

                    readResult = DynamicallyLinkedBindings.av_read_frame(_formatContext, packet);

                    if (readResult >= 0)
                    {
                        try
                        {
                            if (packet->stream_index == _videoStreamIndex)
                            {
                                packetError = ProcessReceivedPacket(packet);
                            }
                        }
                        finally
                        {
                            DynamicallyLinkedBindings.av_packet_unref(packet);
                        }
                    }
                }

                // SEC-3 (Task 3A): a genuine packet-processing failure -- either the consecutive-
                // rejection streak reached EncodedPacketGuard's escalation threshold, or dispatch
                // itself threw -- faults the session through the exact same path an av_read_frame
                // failure already used below, so MediaController's existing reconnect logic
                // activates without any change to MediaController itself. This is what closes the
                // "silent ReadLoop death" gap: no exception can propagate out of this method
                // uncaught anymore.
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
                    if (!_interruptRequested && !cancellationToken.IsCancellationRequested)
                    {
                        TransitionTo(MediaSessionState.Faulted,
                            FfmpegErrorTranslator.FromResult(MediaErrorCategory.Protocol, readResult, "RTSP read failed"));
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
    /// SEC-3 (Task 3A): guards <see cref="RaisePacketReceived"/>'s managed allocation against an
    /// oversized/negative encoded packet size before it happens, and against any other exception
    /// the dispatch itself might throw. Returns null for the common case (accepted and
    /// successfully dispatched, or safely dropped without ending the session); non-null only once
    /// a genuine processing failure -- an unbroken run of rejections reaching
    /// EncodedPacketGuard.MaxConsecutiveRejectedPackets, or a thrown exception -- means the
    /// session itself should fault.
    /// </summary>
    private unsafe MediaError? ProcessReceivedPacket(AVPacket* packet)
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
                    RaisePacketReceived(packet);
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
    /// SEC-3 (Task 3A): delegates to this instance's <see cref="PacketRejectionTracker"/>. Internal
    /// (not private), and taking only a plain int rather than an AVPacket*, specifically so a
    /// focused test can drive real instance state directly -- no FFmpeg-adjacent type crosses this
    /// seam (per ADR-002/ADR-003's existing discipline for this class), and no native session needs
    /// to be open, since this runs before any native call. faultError is non-null if and only if
    /// the decision is Fault.
    /// </summary>
    internal PacketSizeDecision EvaluateAndTrackPacketSize(int packetSize, out MediaError? faultError) =>
        _packetRejectionTracker.EvaluateAndTrack(packetSize, out faultError);

    private unsafe void RaisePacketReceived(AVPacket* packet)
    {
        var timeBase = _formatContext->streams[_videoStreamIndex]->time_base;

        var pts = packet->pts == ffmpeg.AV_NOPTS_VALUE ? packet->dts : packet->pts;
        var dts = packet->dts == ffmpeg.AV_NOPTS_VALUE ? pts : packet->dts;

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
                PresentationTimestamp = ToTimeSpan(pts, timeBase),
                DecodeTimestamp = ToTimeSpan(dts, timeBase)
            },
            IsKeyFrame = (packet->flags & ffmpeg.AV_PKT_FLAG_KEY) != 0
        };

        // Task-AI00B Phase 12A: the per-50-packet "packets received" counter/log previously here
        // was removed -- it had no reader beyond its own log line (confirmed: no Statistics
        // surface, no reconnect/health logic depended on it) and added no diagnostic value not
        // already covered by the one-time stream-opened log above and FfmpegVideoDecoder's decode
        // summary, while logging unboundedly (~every 2s) for the life of a long-running session.
        PacketReceived?.Invoke(this, new EncodedPacketReceivedEventArgs { Packet = encodedFrame });
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
