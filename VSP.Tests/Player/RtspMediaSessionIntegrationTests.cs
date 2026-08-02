using System.Diagnostics;
using VSP.Player.Decoder;
using VSP.Player.Entities;
using Xunit;

namespace VSP.Tests.Player;

/// <summary>
/// Real, zero-fake validation of the FFmpeg ingest + decode pipeline. The bundled ffmpeg.exe
/// first encodes a genuine short MJPEG stream to a temp file; <see cref="RtspMediaSession"/>
/// and <see cref="FfmpegVideoDecoder"/> then open, demux, and decode that file for real via
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
/// </summary>
public class RtspMediaSessionIntegrationTests : IDisposable
{
    private static readonly string FfmpegExecutablePath =
        Path.Combine(AppContext.BaseDirectory, "ffmpeg", "win-x64", "ffmpeg.exe");

    private readonly string _tempFilePath = Path.Combine(Path.GetTempPath(), $"vsp-live-view-test-{Guid.NewGuid():N}.mjpeg");

    [Fact]
    public async Task OpenAsync_AgainstRealFfmpegEncodedStream_ReceivesAndDecodesRealFrames()
    {
        Assert.True(File.Exists(FfmpegExecutablePath), $"Bundled ffmpeg.exe not found at {FfmpegExecutablePath}.");

        await EncodeRealTestStreamAsync();
        Assert.True(File.Exists(_tempFilePath), "ffmpeg did not produce the expected test stream file.");

        using var session = new RtspMediaSession(_tempFilePath, TimeSpan.FromSeconds(5));
        await session.OpenAsync(CancellationToken.None);
        Assert.Equal(MediaSessionState.Open, session.State);

        var firstPacket = await WaitForNextPacketAsync(session, TimeSpan.FromSeconds(5));
        Assert.True(firstPacket.Data.Length > 0);

        using var decoder = new FfmpegVideoDecoder(session);

        DecodedFrame? decodedFrame = null;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        var pendingPacket = firstPacket;

        while (decodedFrame is null && DateTime.UtcNow < deadline)
        {
            decodedFrame = decoder.Decode(pendingPacket);
            if (decodedFrame is null)
            {
                pendingPacket = await WaitForNextPacketAsync(session, TimeSpan.FromSeconds(3));
            }
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

        await session.CloseAsync();
        Assert.Equal(MediaSessionState.Closed, session.State);
    }

    private async Task EncodeRealTestStreamAsync()
    {
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
            "-c:v", "mjpeg",
            "-pix_fmt", "yuvj420p",
            _tempFilePath
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

    private static Task<EncodedFrame> WaitForNextPacketAsync(RtspMediaSession session, TimeSpan timeout)
    {
        var tcs = new TaskCompletionSource<EncodedFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<EncodedPacketReceivedEventArgs>? handler = null;
        handler = (_, e) =>
        {
            tcs.TrySetResult(e.Packet);
            session.PacketReceived -= handler;
        };
        session.PacketReceived += handler;
        return WaitWithTimeoutAsync(tcs.Task, timeout);
    }

    private static async Task<T> WaitWithTimeoutAsync<T>(Task<T> task, TimeSpan timeout)
    {
        var completed = await Task.WhenAny(task, Task.Delay(timeout));
        Assert.True(ReferenceEquals(completed, task), "Timed out waiting for the media session.");
        return await task;
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempFilePath))
            {
                File.Delete(_tempFilePath);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
