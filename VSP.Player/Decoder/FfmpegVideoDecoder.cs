using FFmpeg.AutoGen.Abstractions;
using FFmpeg.AutoGen.Bindings.DynamicallyLinked;
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

        _decodedFrame = DynamicallyLinkedBindings.av_frame_alloc();
        _packet = DynamicallyLinkedBindings.av_packet_alloc();
    }

    public DecodedFrame? Decode(EncodedFrame encodedFrame)
    {
        lock (_nativeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

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
                    return null;
                }
            }

            var receiveResult = DynamicallyLinkedBindings.avcodec_receive_frame(_codecContext, _decodedFrame);
            if (receiveResult < 0)
            {
                return null;
            }

            try
            {
                return ConvertToBgra32(encodedFrame.Timestamp);
            }
            finally
            {
                DynamicallyLinkedBindings.av_frame_unref(_decodedFrame);
            }
        }
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
        }

        var stride = width * 4;
        var buffer = new byte[stride * height];

        fixed (byte* bufferPtr = buffer)
        {
            var dstData = new byte_ptr8 { [0] = bufferPtr };
            var dstLinesize = new int8 { [0] = stride };

            DynamicallyLinkedBindings.sws_scale(_swsContext, _decodedFrame->data, _decodedFrame->linesize, 0, height, dstData, dstLinesize);
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
}
