using System.Diagnostics;
using VSP.Core.Logging;
using VSP.Player.Decoder;
using VSP.Player.Entities;
using VSP.Tests.Logging;
using Xunit;

namespace VSP.Tests.Player;

/// <summary>
/// Real, zero-fake validation of the FFmpeg ingest + decode pipeline, against both MJPEG and
/// H.264 -- the codec class Item F's real-device retest needs covered. The bundled ffmpeg.exe
/// first encodes a genuine short stream to a temp file; <see cref="RtspMediaSession"/> and
/// <see cref="FfmpegVideoDecoder"/> then open, demux, and decode that file for real via
/// avformat/avcodec/swscale — exercising every FFmpeg interop call this Epic added.
///
/// Note: this validates the demux/decode pipeline against real encoded bytes, not RTSP's own
/// network handshake (DESCRIBE/SETUP/PLAY). The bundled FFmpeg build's RTSP muxer has no
/// server/listen capability (confirmed via `ffmpeg -h muxer=rtsp`: no rtsp_flags option is
/// exposed on the encoding side, only on the demuxer/input side), so no local RTSP server could
/// be stood up to exercise that handshake specifically. <see cref="RtspMediaSession"/> does not
/// require its input to be network RTSP — avformat_open_input is protocol-agnostic and the
/// rtsp_transport/stimeout AVOptions are silently ignored for non-RTSP inputs — so pointing it
/// at a real file still exercises 100% real FFmpeg interop code with no mocks.
///
/// Both codec scenarios live as two <c>[Fact]</c>s in this one class deliberately, not as two
/// separate test classes: two independent real <see cref="RtspMediaSession"/>/
/// <see cref="FfmpegVideoDecoder"/> lifecycles running as separate xunit test *classes* in the
/// same test process was observed to reliably starve the second one's packet delivery (reproduced
/// with unmodified pre-existing code, confirmed unrelated to Item F's diagnostic instrumentation,
/// and confirmed NOT reproducible when both codecs run as sibling <c>[Fact]</c>s inside one class
/// -- even explicit xunit collection serialization across the two classes did not fix it). This
/// is a test-execution-boundary quirk, not a product defect; keeping both scenarios in one class
/// is the smallest reliable structure and needs no product-code change.
///
/// Why <c>h264_mf</c> and not <c>libx264</c> for the H.264 stream: the bundled FFmpeg build is
/// <c>--disable-gpl --disable-nonfree</c> (confirmed via <c>ffmpeg -version</c>'s configuration
/// line), so no GPL-licensed x264/x265 encoder is present -- only decode-side native h264/hevc
/// support (also confirmed via <c>ffmpeg -decoders</c>) plus hardware/OS encoders
/// (h264_mf/hevc_mf, *_amf, *_nvenc, *_qsv, *_vulkan). <c>h264_mf</c> uses Windows' built-in
/// software H.264 encoder MFT and was confirmed working in this environment.
///
/// Why no HEVC counterpart: per the approved diagnostic Task Plan, HEVC/H.265 integration
/// coverage is added only if the bundled FFmpeg can generate the synthetic test stream reliably
/// without a new external dependency. It cannot: <c>hevc_mf</c> (the same-shape MediaFoundation
/// path used for H.264 above) fails on this machine with "Error submitting video frame to the
/// encoder ... Generic error in an external library" (HRESULT 0x80004005) -- Windows ships no
/// built-in software HEVC encoder MFT; HEVC encode via MediaFoundation requires either GPU
/// hardware acceleration (AMD/NVENC/QSV, also present in this build's encoder list but not
/// guaranteed available on a build agent) or the separately-installed "HEVC Video Extensions"
/// Store package -- not a reliable, dependency-free precondition. This does not weaken Item F's
/// real-device evidence collection: HEVC/H.264 identification on the actual camera comes from
/// the startup diagnostic added to <see cref="RtspMediaSession"/>/<see cref="FfmpegVideoDecoder"/>,
/// not from this test.
///
/// Task-AI00B Phase 12A: also verifies, against these same two real decode passes (no extra
/// native pipeline run added), that the one-time diagnostics (stream opened, decoder opened,
/// first decoded frame, first BGRA conversion) still fire, and that the periodic decode-summary
/// diagnostic does not fire for a 20-frame run (far under the new ~5-minute gate), and that the
/// removed per-packet counter log never appears at all.
/// </summary>
[Collection("AppLog")]
public class RtspMediaSessionIntegrationTests : IDisposable
{
    private static readonly string FfmpegExecutablePath =
        Path.Combine(AppContext.BaseDirectory, "ffmpeg", "win-x64", "ffmpeg.exe");

    private readonly string _mjpegFilePath = Path.Combine(Path.GetTempPath(), $"vsp-live-view-test-{Guid.NewGuid():N}.mjpeg");
    private readonly string _h264FilePath = Path.Combine(Path.GetTempPath(), $"vsp-live-view-test-{Guid.NewGuid():N}.h264");

    [Fact]
    public async Task OpenAsync_AgainstRealFfmpegEncodedStream_ReceivesAndDecodesRealFrames()
    {
        await EncodeRealTestStreamAsync(_mjpegFilePath, "mjpeg", "yuvj420p", "mjpeg");
        await OpenReceiveDecodeAndAssertAsync(_mjpegFilePath);
    }

    [Fact]
    public async Task OpenAsync_AgainstRealH264EncodedStream_ReceivesAndDecodesRealFrames()
    {
        await EncodeRealTestStreamAsync(_h264FilePath, "h264_mf", "yuv420p", "h264");
        await OpenReceiveDecodeAndAssertAsync(_h264FilePath);
    }

    private static async Task OpenReceiveDecodeAndAssertAsync(string filePath)
    {
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        Assert.True(File.Exists(filePath), "ffmpeg did not produce the expected test stream file.");

        // Generous timeouts: under the full ~847-test parallel suite run, CPU contention alone
        // (not any product defect) was observed to push this well past a 5s budget intermittently
        // even though the same scenario completes in well under a second in isolation.
        using var session = new RtspMediaSession(filePath, TimeSpan.FromSeconds(10));

        var packetGate = new object();
        var bufferedPackets = new Queue<EncodedFrame>();
        TaskCompletionSource<EncodedFrame>? nextPacketWaiter = null;

        EventHandler<EncodedPacketReceivedEventArgs>? packetHandler = null;
        packetHandler = (_, e) =>
        {
            TaskCompletionSource<EncodedFrame>? waiter;
            lock (packetGate)
            {
                waiter = nextPacketWaiter;
                if (waiter is null)
                {
                    bufferedPackets.Enqueue(e.Packet);
                }
                else
                {
                    nextPacketWaiter = null;
                }
            }

            waiter?.TrySetResult(e.Packet);
        };
        session.PacketReceived += packetHandler;

        try
        {
            // Subscribe before OpenAsync starts the read loop. Under full-suite load, this short
            // synthetic stream can deliver packets immediately after open; buffering packets until
            // the decoder is constructed prevents the test from losing them at that boundary.
            await session.OpenAsync(CancellationToken.None);
            Assert.Equal(MediaSessionState.Open, session.State);

            using var decoder = new FfmpegVideoDecoder(session);

            DecodedFrame? decodedFrame = null;
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);

            while (decodedFrame is null && DateTime.UtcNow < deadline)
            {
                var pendingPacket = await WaitForBufferedPacketAsync(TimeSpan.FromSeconds(15));
                Assert.True(pendingPacket.Data.Length > 0);
                decodedFrame = decoder.Decode(pendingPacket);
            }

            Assert.NotNull(decodedFrame);
            Assert.Equal(FrameStorage.Cpu, decodedFrame!.Storage);
            Assert.Equal(FramePixelFormat.Bgra32, decodedFrame.PixelFormat);
            Assert.Equal(320, decodedFrame.Width);
            Assert.Equal(240, decodedFrame.Height);
            Assert.NotNull(decodedFrame.PixelBuffer);
            Assert.Equal(decodedFrame.Stride * decodedFrame.Height, decodedFrame.PixelBuffer!.Length);

            // A real decoded testsrc frame must contain actual image data, not an all-zero buffer.
            Assert.Contains(decodedFrame.PixelBuffer, b => b != 0);
        }
        finally
        {
            session.PacketReceived -= packetHandler;
        }

        await session.CloseAsync();
        Assert.Equal(MediaSessionState.Closed, session.State);

        // Task-AI00B Phase 12A: one-time diagnostics must still fire.
        Assert.Contains(recorder.Calls, c => c.Message.StartsWith("Live View RTSP stream opened:"));
        Assert.Contains(recorder.Calls, c => c.Message.StartsWith("Live View decoder opened:"));
        Assert.Contains(recorder.Calls, c => c.Message.StartsWith("Live View: first decoded frame produced"));
        Assert.Contains(recorder.Calls, c => c.Message.StartsWith("Live View: first BGRA conversion:"));

        // Regression guards: the periodic decode summary must not fire for a 20-frame run (far
        // under the new ~5-minute gate), and the removed per-packet counter log must never appear.
        Assert.DoesNotContain(recorder.Calls, c => c.Message.StartsWith("Live View decode summary:"));
        Assert.DoesNotContain(recorder.Calls, c => c.Message.StartsWith("Live View RTSP packets received:"));

        // No diagnostic line from this pipeline ever carries a stream URL (this pipeline never
        // handles credentials, but the sanitization discipline is verified regardless).
        Assert.All(recorder.Calls, call => Assert.DoesNotContain("rtsp://", call.Message, StringComparison.OrdinalIgnoreCase));

        async Task<EncodedFrame> WaitForBufferedPacketAsync(TimeSpan timeout)
        {
            Task<EncodedFrame> waitTask;
            lock (packetGate)
            {
                if (bufferedPackets.Count > 0)
                {
                    return bufferedPackets.Dequeue();
                }

                nextPacketWaiter = new TaskCompletionSource<EncodedFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
                waitTask = nextPacketWaiter.Task;
            }

            try
            {
                return await WaitWithTimeoutAsync(waitTask, timeout);
            }
            finally
            {
                lock (packetGate)
                {
                    if (ReferenceEquals(nextPacketWaiter?.Task, waitTask))
                    {
                        nextPacketWaiter = null;
                    }
                }
            }
        }
    }

    private static async Task EncodeRealTestStreamAsync(string outputPath, string codec, string pixelFormat, string containerFormat)
    {
        Assert.True(File.Exists(FfmpegExecutablePath), $"Bundled ffmpeg.exe not found at {FfmpegExecutablePath}.");

        var startInfo = new ProcessStartInfo(FfmpegExecutablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (var arg in new[]
        {
            "-hide_banner", "-loglevel", "error", "-y",
            "-f", "lavfi", "-i", "testsrc=size=320x240:rate=10",
            "-frames:v", "20",
            "-c:v", codec,
            "-pix_fmt", pixelFormat,
            "-f", containerFormat,
            outputPath
        })
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start ffmpeg encoder.");
        process.OutputDataReceived += static (_, _) => { };
        process.ErrorDataReceived += static (_, _) => { };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(15));
        Assert.Equal(0, process.ExitCode);
    }

    private static async Task<T> WaitWithTimeoutAsync<T>(Task<T> task, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        Assert.True(ReferenceEquals(completed, task), "Timed out waiting for the media session.");
        return await task;
    }

    public void Dispose()
    {
        TryDelete(_mjpegFilePath);
        TryDelete(_h264FilePath);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
