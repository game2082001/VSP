using System.Diagnostics;
using VSP.Player.Decoder;
using VSP.Player.Entities;
using Xunit;

namespace VSP.Tests.Player;

/// <summary>
/// Real, zero-fake validation of the Playback Foundation (Epic-012) file-backed session --
/// mirrors RtspMediaSessionIntegrationTests/RecordingIntegrationTests: the bundled ffmpeg.exe
/// first encodes a genuine short source, then RecordedFileMediaSession opens/demuxes/paces it
/// for real via avformat/av_read_frame/av_seek_frame.
/// </summary>
public class RecordedFileMediaSessionTests : IDisposable
{
    private static readonly string FfmpegExecutablePath =
        Path.Combine(AppContext.BaseDirectory, "ffmpeg", "win-x64", "ffmpeg.exe");

    // .mp4, not raw .mjpeg (unlike RtspMediaSessionIntegrationTests' source): Duration and Seek
    // are both meaningless against a raw elementary stream with no container-level index/duration
    // metadata -- an .mp4 container is what a real Epic-011 recording actually is, and what these
    // two capabilities need to be tested against.
    private readonly string _sourceFilePath =
        Path.Combine(Path.GetTempPath(), $"vsp-playback-source-{Guid.NewGuid():N}.mp4");

    [Fact]
    public async Task OpenAsync_AgainstRealFfmpegEncodedFile_PlaysBackDecodesAndReachesClosedAtEof()
    {
        Assert.True(File.Exists(FfmpegExecutablePath), $"Bundled ffmpeg.exe not found at {FfmpegExecutablePath}.");

        await EncodeRealTestSourceAsync(frameCount: 20, rate: 10);
        Assert.True(File.Exists(_sourceFilePath), "ffmpeg did not produce the expected test source file.");

        using var session = new RecordedFileMediaSession(_sourceFilePath);
        Assert.Equal(VideoSourceKind.RecordedFile, session.Kind);

        var packets = new List<EncodedFrame>();
        var sessionEndedTcs = new TaskCompletionSource<MediaSessionState>(TaskCreationOptions.RunContinuationsAsynchronously);

        session.PacketReceived += (_, e) =>
        {
            lock (packets)
            {
                packets.Add(e.Packet);
            }
        };
        session.StateChanged += (_, se) =>
        {
            if (se.State is MediaSessionState.Faulted or MediaSessionState.Closed)
            {
                sessionEndedTcs.TrySetResult(se.State);
            }
        };

        var stopwatch = Stopwatch.StartNew();
        await session.OpenAsync(CancellationToken.None);
        Assert.Equal(MediaSessionState.Open, session.State);

        var playbackControl = Assert.IsAssignableFrom<IPlaybackControl>(session);
        Assert.NotNull(playbackControl.Duration);
        Assert.True(playbackControl.Duration!.Value > TimeSpan.Zero);

        using var decoder = new FfmpegVideoDecoder((IFfmpegDemuxSource)session);

        var completed = await Task.WhenAny(sessionEndedTcs.Task, Task.Delay(TimeSpan.FromSeconds(15)));
        Assert.Same(sessionEndedTcs.Task, completed);
        stopwatch.Stop();

        // A file source reaching EOF is a graceful, normal completion for Playback -- unlike a
        // live source (where the same condition is a fault requiring reconnect), Closed is the
        // correct terminal state here.
        Assert.Equal(MediaSessionState.Closed, await sessionEndedTcs.Task);
        Assert.Equal(MediaSessionState.Closed, session.State);

        // 20 frames @ 10fps is a 2s source; real-time pacing should make this take noticeably
        // longer than an unpaced "read as fast as disk I/O allows" pass would (well under 100ms).
        Assert.True(stopwatch.Elapsed > TimeSpan.FromMilliseconds(500),
            $"Playback finished in {stopwatch.Elapsed} -- pacing does not appear to be applied.");

        List<EncodedFrame> capturedPackets;
        lock (packets)
        {
            capturedPackets = new List<EncodedFrame>(packets);
        }

        Assert.True(capturedPackets.Count > 0, "No packets were demuxed from the real FFmpeg-encoded source.");

        DecodedFrame? decodedFrame = null;
        foreach (var packet in capturedPackets)
        {
            decodedFrame = decoder.Decode(packet);
            if (decodedFrame is not null)
            {
                break;
            }
        }

        Assert.NotNull(decodedFrame);
        Assert.Equal(320, decodedFrame!.Width);
        Assert.Equal(240, decodedFrame.Height);
        Assert.Contains(decodedFrame.PixelBuffer!, b => b != 0);

        await session.CloseAsync();
    }

    [Fact]
    public async Task PauseReading_StopsPacketFlow_ResumeReadingContinues()
    {
        Assert.True(File.Exists(FfmpegExecutablePath), $"Bundled ffmpeg.exe not found at {FfmpegExecutablePath}.");

        await EncodeRealTestSourceAsync(frameCount: 40, rate: 20);

        using var session = new RecordedFileMediaSession(_sourceFilePath);
        var packetCount = 0;
        session.PacketReceived += (_, _) => Interlocked.Increment(ref packetCount);

        await session.OpenAsync(CancellationToken.None);
        var playbackControl = Assert.IsAssignableFrom<IPlaybackControl>(session);

        // Let a few packets flow, then pause -- the count must stop advancing while paused.
        Assert.True(await WaitUntilAsync(() => Volatile.Read(ref packetCount) > 0, TimeSpan.FromSeconds(3)));
        playbackControl.PauseReading();

        // PauseReading only takes effect before the read loop's *next* iteration -- a packet
        // already past that check when Pause was requested can still land. Give that one packet
        // (at most) time to arrive before snapshotting the count this assertion holds stable.
        await Task.Delay(100);
        var countAfterPause = Volatile.Read(ref packetCount);
        await Task.Delay(300);
        Assert.Equal(countAfterPause, Volatile.Read(ref packetCount));

        playbackControl.ResumeReading();
        Assert.True(await WaitUntilAsync(() => Volatile.Read(ref packetCount) > countAfterPause, TimeSpan.FromSeconds(3)));

        await session.CloseAsync();
    }

    [Fact]
    public async Task TrySeek_ToMidpoint_ReturnsAchievedPositionAndContinuesReading()
    {
        Assert.True(File.Exists(FfmpegExecutablePath), $"Bundled ffmpeg.exe not found at {FfmpegExecutablePath}.");

        // 60 frames @ 30fps = 2s source, long enough to seek into the middle of.
        await EncodeRealTestSourceAsync(frameCount: 60, rate: 30);

        using var session = new RecordedFileMediaSession(_sourceFilePath);
        var packetsAfterSeek = 0;
        session.PacketReceived += (_, _) => Interlocked.Increment(ref packetsAfterSeek);

        await session.OpenAsync(CancellationToken.None);
        var playbackControl = Assert.IsAssignableFrom<IPlaybackControl>(session);

        var achieved = playbackControl.TrySeek(TimeSpan.FromSeconds(1));
        Assert.NotNull(achieved);

        Assert.True(await WaitUntilAsync(() => Volatile.Read(ref packetsAfterSeek) > 0, TimeSpan.FromSeconds(3)),
            "No packet was delivered after seeking.");

        await session.CloseAsync();
    }

    private async Task EncodeRealTestSourceAsync(int frameCount, int rate)
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
            "-f", "lavfi", "-i", $"testsrc=size=320x240:rate={rate}",
            "-frames:v", frameCount.ToString(),
            "-c:v", "mjpeg",
            "-pix_fmt", "yuvj420p",
            _sourceFilePath
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

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(10);
        }

        return condition();
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_sourceFilePath))
            {
                File.Delete(_sourceFilePath);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
