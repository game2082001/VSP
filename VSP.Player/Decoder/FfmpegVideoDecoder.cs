using FFmpeg.AutoGen.Abstractions;
using FFmpeg.AutoGen.Bindings.DynamicallyLinked;
using VSP.Core.Logging;
using VSP.Player.Entities;
using VSP.Player.Interfaces;

namespace VSP.Player.Decoder;

/// <summary>
/// FFmpeg-backed software decoder. Produces only CPU-resident <see cref="DecodedFrame"/>s
/// (BGRA32, matching WPF's native pixel layout) via swscale. No FFmpeg type crosses out of
/// this class's public surface.
/// </summary>
public sealed unsafe class FfmpegVideoDecoder : IVideoDecoder
{
    private readonly AVCodecContext* _codecContext;
    private readonly AVFrame* _decodedFrame;
    private readonly AVPacket* _packet;

    private SwsContext* _swsContext;
    private int _scaledWidth;
    private int _scaledHeight;

    // Guards every native call against a concurrent Dispose(): Dispose() takes this same lock
    // before freeing native pointers, so it can never run while Decode()/Reset() are in flight.
    private readonly object _nativeGate = new();

    private bool _disposed;

    // Item F evidence-collection instrumentation (read-only diagnostics only -- no decode,
    // pixel-conversion, or extradata handling behavior is affected by any of this). All fields
    // below are only ever touched while holding _nativeGate (Decode() already holds it for its
    // whole body), so no additional synchronization is needed. Aggregated and startup/milestone
    // only per the approved diagnostic Task Plan: never one log line per packet/frame.
    //
    // Task-AI00B Phase 12A: the periodic summary below was originally gated by decode-attempt
    // count (every 50 attempts, ~2s at 25fps) -- fine for a short investigation, but unbounded
    // log volume over a multi-hour production Live View session. Replaced with a time gate
    // (Environment.TickCount64, checked only at existing call sites -- no timer/thread added)
    // targeting ~5 minutes between summaries. IsDiagnosticsIntervalElapsed is a pure function so
    // this cadence is unit-testable without a live native decode session.
    private const long DiagnosticsSummaryIntervalMilliseconds = 5 * 60 * 1000;
    private long _lastDiagnosticsSummaryTickCount = Environment.TickCount64;
    private long _decodeAttempts;
    private long _sendPacketFailures;
    private string? _lastSendPacketError;
    private long _receiveFrameEagainCount;
    private long _receiveFrameHardFailures;
    private string? _lastReceiveFrameError;
    private long _decodedFrameSuccesses;
    private bool _firstFrameLogged;

    // Task-AI00B Phase 5: BGRA conversion (sws_scale) diagnostics -- same read-only,
    // aggregated/first-frame-only discipline as the Item F fields above. These fields observe
    // the ConvertToBgra32 boundary only; they never alter cache-key, rebuild, or conversion
    // behavior (see ConvertToBgra32 for the explicit "detect only, do not rebuild" boundary).
    private AVPixelFormat? _scaledSourceFormat;
    private bool _firstConversionLogged;
    private long _swsScaleFailures;
    private int _lastSwsScaleReturn;
    private long _swsScaleShortOutputs;
    private long _swsContextFormatChanges;
    private long _sampledAllZeroFrames;

    /// <summary>
    /// Internal: the only public-facing surface for decode is <see cref="IVideoDecoder"/>, reached
    /// via <c>MediaController</c>/<c>PlaybackController</c>'s own factories -- both within this
    /// assembly. The constructor itself must stay internal because its parameter type,
    /// <see cref="IFfmpegDemuxSource"/>, is internal (no FFmpeg-adjacent type may appear in a
    /// public signature, per ADR-002/ADR-003).
    /// </summary>
    internal FfmpegVideoDecoder(IFfmpegDemuxSource session)
    {
        FfmpegNativeLibraryLoader.EnsureInitialized();

        var codecParameters = session.GetVideoCodecParameters();
        if (codecParameters is null)
        {
            throw new InvalidOperationException("Media session has no open video stream to decode.");
        }

        var codec = DynamicallyLinkedBindings.avcodec_find_decoder(codecParameters->codec_id);
        if (codec is null)
        {
            throw new InvalidOperationException($"No FFmpeg decoder available for codec {codecParameters->codec_id}.");
        }

        _codecContext = DynamicallyLinkedBindings.avcodec_alloc_context3(codec);
        if (_codecContext is null)
        {
            throw new InvalidOperationException("Failed to allocate AVCodecContext.");
        }

        var paramsResult = DynamicallyLinkedBindings.avcodec_parameters_to_context(_codecContext, codecParameters);
        if (paramsResult < 0)
        {
            throw new InvalidOperationException(
                FfmpegErrorTranslator.FromResult(MediaErrorCategory.Decode, paramsResult, "Failed to copy codec parameters").Message);
        }

        var openResult = DynamicallyLinkedBindings.avcodec_open2(_codecContext, codec, null);
        if (openResult < 0)
        {
            throw new InvalidOperationException(
                FfmpegErrorTranslator.FromResult(MediaErrorCategory.Decode, openResult, "Failed to open codec").Message);
        }

        var declaredPixelFormat = DynamicallyLinkedBindings.av_get_pix_fmt_name((AVPixelFormat)codecParameters->format) ?? "unknown";
        AppLog.Info(
            $"Live View decoder opened: codec={DynamicallyLinkedBindings.avcodec_get_name(codecParameters->codec_id)} " +
            $"({codecParameters->codec_id}), declared {codecParameters->width}x{codecParameters->height}, pixfmt={declaredPixelFormat}.");

        _decodedFrame = DynamicallyLinkedBindings.av_frame_alloc();
        _packet = DynamicallyLinkedBindings.av_packet_alloc();
    }

    public DecodedFrame? Decode(EncodedFrame encodedFrame)
    {
        lock (_nativeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            _decodeAttempts++;

            DynamicallyLinkedBindings.av_packet_unref(_packet);

            fixed (byte* dataPtr = encodedFrame.Data)
            {
                _packet->data = dataPtr;
                _packet->size = encodedFrame.Data.Length;

                var sendResult = DynamicallyLinkedBindings.avcodec_send_packet(_codecContext, _packet);
                if (sendResult < 0)
                {
                    // A transient decode condition (e.g. decoder still buffering) is expected and
                    // non-fatal for a live stream; only genuinely unrecoverable codec state should
                    // ever reach here, and even then dropping this frame keeps the session alive.
                    _sendPacketFailures++;
                    _lastSendPacketError = FfmpegErrorTranslator.FromResult(MediaErrorCategory.Decode, sendResult, "avcodec_send_packet").Message;
                    LogDiagnosticsSummaryIfDue();
                    return null;
                }
            }

            var receiveResult = DynamicallyLinkedBindings.avcodec_receive_frame(_codecContext, _decodedFrame);
            if (receiveResult < 0)
            {
                if (receiveResult == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                {
                    // Expected/normal: decoder needs more input before it can emit a frame (e.g.
                    // still buffering reference frames). Not an error -- tracked separately from
                    // genuine hard failures below so a stuck-forever EAGAIN loop is distinguishable
                    // from real decode errors in the periodic summary.
                    _receiveFrameEagainCount++;
                }
                else
                {
                    _receiveFrameHardFailures++;
                    _lastReceiveFrameError = FfmpegErrorTranslator.FromResult(MediaErrorCategory.Decode, receiveResult, "avcodec_receive_frame").Message;
                }

                LogDiagnosticsSummaryIfDue();
                return null;
            }

            _decodedFrameSuccesses++;
            if (!_firstFrameLogged)
            {
                _firstFrameLogged = true;
                var actualPixelFormat = (AVPixelFormat)_decodedFrame->format;
                AppLog.Info(
                    $"Live View: first decoded frame produced after {_decodeAttempts} packet(s): " +
                    $"{_decodedFrame->width}x{_decodedFrame->height}, pixfmt={actualPixelFormat}.");
            }

            try
            {
                return ConvertToBgra32(encodedFrame.Timestamp);
            }
            finally
            {
                DynamicallyLinkedBindings.av_frame_unref(_decodedFrame);
                LogDiagnosticsSummaryIfDue();
            }
        }
    }

    /// <summary>
    /// Pure time-gate check (Task-AI00B Phase 12A), extracted so the periodic-diagnostics cadence
    /// is unit-testable without a live native decode session. Not itself a timer -- only ever
    /// consulted synchronously from within an already-occurring <see cref="Decode"/> call.
    /// </summary>
    internal static bool IsDiagnosticsIntervalElapsed(long lastLoggedTickCount, long nowTickCount, long intervalMilliseconds)
        => nowTickCount - lastLoggedTickCount >= intervalMilliseconds;

    /// <summary>
    /// Periodic, aggregated decode-health summary (Item F evidence collection) -- fires at most
    /// once every <see cref="DiagnosticsSummaryIntervalMilliseconds"/>, never per-packet/per-frame.
    /// </summary>
    private void LogDiagnosticsSummaryIfDue()
    {
        var now = Environment.TickCount64;
        if (!IsDiagnosticsIntervalElapsed(_lastDiagnosticsSummaryTickCount, now, DiagnosticsSummaryIntervalMilliseconds))
        {
            return;
        }

        _lastDiagnosticsSummaryTickCount = now;

        AppLog.Info(
            $"Live View decode summary: attempts={_decodeAttempts}, decoded={_decodedFrameSuccesses}, " +
            $"sendPacketFailures={_sendPacketFailures} (last: {_lastSendPacketError ?? "none"}), " +
            $"receiveEagain={_receiveFrameEagainCount}, receiveHardFailures={_receiveFrameHardFailures} " +
            $"(last: {_lastReceiveFrameError ?? "none"}), " +
            $"swsScaleFailures={_swsScaleFailures}, swsScaleShortOutputs={_swsScaleShortOutputs}, " +
            $"lastSwsScaleReturn={_lastSwsScaleReturn}, swsContextFormatChanges={_swsContextFormatChanges}, " +
            $"sampledAllZeroFrames={_sampledAllZeroFrames}.");
    }

    public void Reset()
    {
        lock (_nativeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            DynamicallyLinkedBindings.avcodec_flush_buffers(_codecContext);
        }
    }

    public void Dispose()
    {
        lock (_nativeGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_swsContext is not null)
            {
                DynamicallyLinkedBindings.sws_freeContext(_swsContext);
                _swsContext = null;
            }

            var packet = _packet;
            if (packet is not null)
            {
                DynamicallyLinkedBindings.av_packet_free(&packet);
            }

            var frame = _decodedFrame;
            if (frame is not null)
            {
                DynamicallyLinkedBindings.av_frame_free(&frame);
            }

            var codecContext = _codecContext;
            if (codecContext is not null)
            {
                DynamicallyLinkedBindings.avcodec_free_context(&codecContext);
            }
        }
    }

    private DecodedFrame ConvertToBgra32(FrameTimestamp timestamp)
    {
        var width = _decodedFrame->width;
        var height = _decodedFrame->height;
        var sourceFormat = (AVPixelFormat)_decodedFrame->format;

        // Diagnostics only (Task-AI00B Phase 5): detect a source pixel format that differs from
        // the one the cached _swsContext was actually built for, while it is about to be reused
        // unchanged. This must NOT influence the rebuild decision below -- the cache key stays
        // width/height only in this Phase; adding pixel format to it is a behavioral fix that is
        // explicitly out of scope here.
        var contextWillBeReused = _swsContext is not null && _scaledWidth == width && _scaledHeight == height;
        if (contextWillBeReused && _scaledSourceFormat.HasValue && _scaledSourceFormat.Value != sourceFormat)
        {
            _swsContextFormatChanges++;
        }

        if (_swsContext is null || _scaledWidth != width || _scaledHeight != height)
        {
            if (_swsContext is not null)
            {
                DynamicallyLinkedBindings.sws_freeContext(_swsContext);
            }

            _swsContext = DynamicallyLinkedBindings.sws_getContext(
                width, height, sourceFormat,
                width, height, AVPixelFormat.AV_PIX_FMT_BGRA,
                (int)SwsFlags.SWS_BILINEAR, null, null, null);

            if (_swsContext is null)
            {
                throw new InvalidOperationException("Failed to create swscale conversion context.");
            }

            _scaledWidth = width;
            _scaledHeight = height;
            _scaledSourceFormat = sourceFormat;
        }

        var stride = width * 4;
        var buffer = new byte[stride * height];

        int returnedLines;
        fixed (byte* bufferPtr = buffer)
        {
            var dstData = new byte_ptr8 { [0] = bufferPtr };
            var dstLinesize = new int8 { [0] = stride };

            returnedLines = DynamicallyLinkedBindings.sws_scale(_swsContext, _decodedFrame->data, _decodedFrame->linesize, 0, height, dstData, dstLinesize);
        }

        // Diagnostics only: record the outcome, never throw or otherwise change what is returned
        // below -- a negative/short return from sws_scale is evidence to gather, not a fault to
        // handle differently in this Phase.
        _lastSwsScaleReturn = returnedLines;
        if (returnedLines < 0)
        {
            _swsScaleFailures++;
        }
        else if (returnedLines < height)
        {
            _swsScaleShortOutputs++;
        }

        // First converted frame + the same time-gated cadence as LogDiagnosticsSummaryIfDue: never
        // a per-frame scan, and the sample itself is a small fixed set of points, never the full
        // buffer (see SampleBufferHasNonZeroContent). Read-only check -- does not itself advance
        // _lastDiagnosticsSummaryTickCount; only LogDiagnosticsSummaryIfDue does that.
        if (!_firstConversionLogged || IsDiagnosticsIntervalElapsed(_lastDiagnosticsSummaryTickCount, Environment.TickCount64, DiagnosticsSummaryIntervalMilliseconds))
        {
            var sampledNonZero = SampleBufferHasNonZeroContent(buffer, stride, height);
            if (!sampledNonZero)
            {
                _sampledAllZeroFrames++;
            }

            if (!_firstConversionLogged)
            {
                _firstConversionLogged = true;
                var codecName = DynamicallyLinkedBindings.avcodec_get_name(_codecContext->codec_id) ?? "unknown";
                AppLog.Info(
                    $"Live View: first BGRA conversion: codec={codecName}, sourcePixfmt={sourceFormat}, " +
                    $"{width}x{height}, swsScaleReturnedLines={returnedLines}, expectedLines={height}, " +
                    $"sampledNonZero={sampledNonZero}, contextPixfmt={_scaledSourceFormat}.");
            }
        }

        return new DecodedFrame
        {
            Storage = FrameStorage.Cpu,
            Width = width,
            Height = height,
            PixelFormat = FramePixelFormat.Bgra32,
            Timestamp = timestamp,
            PixelBuffer = buffer,
            Stride = stride
        };
    }

    /// <summary>
    /// Bounded, fixed-cost check for whether a BGRA buffer contains any non-zero byte, sampled at
    /// a small fixed grid of points rather than scanning the whole buffer. This is a coarse
    /// black-frame indicator only (a true positive is certain; an all-zero sample does not prove
    /// the whole frame is black) -- diagnostic use only, per Task-AI00B Phase 5.
    /// </summary>
    internal static bool SampleBufferHasNonZeroContent(byte[] buffer, int stride, int height)
    {
        if (buffer.Length == 0 || stride <= 0 || height <= 0)
        {
            return false;
        }

        Span<double> rowFractions = [0.0, 0.5, 0.99];
        Span<double> colFractions = [0.0, 0.25, 0.5, 0.75, 0.99];

        foreach (var rowFraction in rowFractions)
        {
            var rowStart = (int)(rowFraction * (height - 1)) * stride;
            foreach (var colFraction in colFractions)
            {
                var byteOffset = rowStart + (int)(colFraction * (stride - 1));
                if (byteOffset >= 0 && byteOffset < buffer.Length && buffer[byteOffset] != 0)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
