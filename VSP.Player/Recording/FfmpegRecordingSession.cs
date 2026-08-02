using FFmpeg.AutoGen.Abstractions;
using FFmpeg.AutoGen.Bindings.DynamicallyLinked;
using VSP.Player.Decoder;
using VSP.Player.Entities;
using VSP.Player.Interfaces;

namespace VSP.Player.Recording;

/// <summary>
/// FFmpeg-backed stream-copy recording writer. Muxes the already-encoded packets received from
/// the encoded-tier Dispatcher directly into an output container via FFmpeg's remux path
/// (<c>avcodec_parameters_copy</c> + <c>av_interleaved_write_frame</c>) -- no decode, no
/// re-encode. Takes the concrete <see cref="RtspMediaSession"/> (not <see cref="IMediaSession"/>)
/// purely to read the source stream's codec parameters/time_base once at <see cref="StartAsync"/>,
/// mirroring <c>FfmpegVideoDecoder</c>'s existing constructor pattern; no FFmpeg type is exposed
/// through <see cref="IRecordingSession"/>.
/// </summary>
public sealed unsafe class FfmpegRecordingSession : IRecordingSession
{
    private readonly RtspMediaSession _session;

    // Guards every native call against a concurrent Dispose()/StopAsync(), matching the
    // established pattern in FfmpegVideoDecoder/RtspMediaSession.
    private readonly object _nativeGate = new();

    private AVFormatContext* _outputContext;
    private AVRational _sourceTimeBase;
    private bool _sawFirstKeyFrame;
    private bool _headerWritten;
    private bool _disposed;

    public FfmpegRecordingSession(RtspMediaSession session, IRecordingMode? mode = null)
    {
        ArgumentNullException.ThrowIfNull(session);

        _session = session;
        Mode = mode ?? new ContinuousRecordingMode();

        FfmpegNativeLibraryLoader.EnsureInitialized();
    }

    public IRecordingMode Mode { get; }

    public bool IsActive { get; private set; }

    public Task StartAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        lock (_nativeGate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_outputContext is not null)
            {
                throw new InvalidOperationException("Recording has already been started.");
            }

            var sourceCodecParameters = _session.GetVideoCodecParameters();
            if (sourceCodecParameters is null)
            {
                throw new InvalidOperationException("Media session has no open video stream to record.");
            }

            _sourceTimeBase = _session.GetVideoStreamTimeBase();

            AVFormatContext* outputContext;
            var allocResult = DynamicallyLinkedBindings.avformat_alloc_output_context2(&outputContext, null, null, filePath);
            if (allocResult < 0 || outputContext is null)
            {
                throw new InvalidOperationException(
                    FfmpegErrorTranslator.FromResult(MediaErrorCategory.Recording, allocResult, "Failed to allocate recording output context").Message);
            }

            var outputStream = DynamicallyLinkedBindings.avformat_new_stream(outputContext, null);
            if (outputStream is null)
            {
                DynamicallyLinkedBindings.avformat_free_context(outputContext);
                throw new InvalidOperationException("Failed to allocate recording output stream.");
            }

            var copyResult = DynamicallyLinkedBindings.avcodec_parameters_copy(outputStream->codecpar, sourceCodecParameters);
            if (copyResult < 0)
            {
                DynamicallyLinkedBindings.avformat_free_context(outputContext);
                throw new InvalidOperationException(
                    FfmpegErrorTranslator.FromResult(MediaErrorCategory.Recording, copyResult, "Failed to copy codec parameters for recording").Message);
            }

            // Reset: the input container's codec tag is not guaranteed valid for the output
            // container's format (e.g. a different fourcc registry), matching FFmpeg's own
            // remuxing.c example. time_base must also be seeded before avformat_write_header --
            // left unset, avformat_new_stream's default is degenerate and every packet's
            // rescaled pts/dts collapses to non-monotonic/zero values, which the muxer silently
            // rejects (a valid-looking, always-empty output file: confirmed via the real-FFmpeg
            // integration test, which failed to read back any packet from the recorded file
            // until this was added).
            outputStream->codecpar->codec_tag = 0;
            outputStream->time_base = _sourceTimeBase;

            if ((outputContext->oformat->flags & ffmpeg.AVFMT_NOFILE) == 0)
            {
                var openResult = DynamicallyLinkedBindings.avio_open(&outputContext->pb, filePath, ffmpeg.AVIO_FLAG_WRITE);
                if (openResult < 0)
                {
                    DynamicallyLinkedBindings.avformat_free_context(outputContext);
                    throw new InvalidOperationException(
                        FfmpegErrorTranslator.FromResult(MediaErrorCategory.Recording, openResult, "Failed to open recording output file").Message);
                }
            }

            var writeHeaderResult = DynamicallyLinkedBindings.avformat_write_header(outputContext, null);
            if (writeHeaderResult < 0)
            {
                CloseOutputContext(outputContext);
                throw new InvalidOperationException(
                    FfmpegErrorTranslator.FromResult(MediaErrorCategory.Recording, writeHeaderResult, "Failed to write recording output header").Message);
            }

            _outputContext = outputContext;
            _headerWritten = true;
            _sawFirstKeyFrame = false;
        }

        return Task.CompletedTask;
    }

    public void Start() => IsActive = true;

    public void Stop() => IsActive = false;

    public void OnFrame(EncodedFrame frame)
    {
        if (!IsActive)
        {
            return;
        }

        lock (_nativeGate)
        {
            if (_disposed || _outputContext is null)
            {
                return;
            }

            if (!Mode.ShouldRecord(new RecordingModeContext { Timestamp = DateTimeOffset.UtcNow }))
            {
                return;
            }

            if (!_sawFirstKeyFrame)
            {
                // Starting mid-GOP produces a file that will not decode from its first frame;
                // wait for a keyframe so the output is playable from byte zero.
                if (!frame.IsKeyFrame)
                {
                    return;
                }

                _sawFirstKeyFrame = true;
            }

            WritePacket(frame);
        }
    }

    private void WritePacket(EncodedFrame frame)
    {
        var packet = DynamicallyLinkedBindings.av_packet_alloc();

        try
        {
            fixed (byte* dataPtr = frame.Data)
            {
                packet->data = dataPtr;
                packet->size = frame.Data.Length;
                packet->stream_index = 0;
                packet->pts = ToTimestamp(frame.Timestamp.PresentationTimestamp, _sourceTimeBase);
                packet->dts = ToTimestamp(frame.Timestamp.DecodeTimestamp, _sourceTimeBase);
                if (frame.IsKeyFrame)
                {
                    packet->flags |= ffmpeg.AV_PKT_FLAG_KEY;
                }

                var outputStream = _outputContext->streams[0];
                DynamicallyLinkedBindings.av_packet_rescale_ts(packet, _sourceTimeBase, outputStream->time_base);

                // Per av_interleaved_write_frame's own contract, a non-reference-counted packet
                // (this one) is copied by libavformat before the call returns, so it is safe to
                // let `fixed` release dataPtr immediately afterward.
                DynamicallyLinkedBindings.av_interleaved_write_frame(_outputContext, packet);
            }
        }
        finally
        {
            DynamicallyLinkedBindings.av_packet_free(&packet);
        }
    }

    private static long ToTimestamp(TimeSpan timestamp, AVRational timeBase)
    {
        if (timeBase.den == 0)
        {
            return 0;
        }

        return (long)Math.Round(timestamp.TotalSeconds / ffmpeg.av_q2d(timeBase));
    }

    public Task StopAsync()
    {
        lock (_nativeGate)
        {
            FinalizeOutput();
        }

        IsActive = false;
        return Task.CompletedTask;
    }

    private void FinalizeOutput()
    {
        if (_outputContext is null)
        {
            return;
        }

        if (_headerWritten)
        {
            DynamicallyLinkedBindings.av_write_trailer(_outputContext);
        }

        CloseOutputContext(_outputContext);
        _outputContext = null;
        _headerWritten = false;
    }

    private static void CloseOutputContext(AVFormatContext* outputContext)
    {
        if ((outputContext->oformat->flags & ffmpeg.AVFMT_NOFILE) == 0 && outputContext->pb is not null)
        {
            DynamicallyLinkedBindings.avio_closep(&outputContext->pb);
        }

        DynamicallyLinkedBindings.avformat_free_context(outputContext);
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
            FinalizeOutput();
        }

        IsActive = false;
    }
}
