using System.Diagnostics;
using VSP.Player.Decoder;
using VSP.Player.Entities;
using VSP.Player.Recording;
using Xunit;

namespace VSP.Tests.Player;

/// <summary>
/// Real, zero-fake validation of the stream-copy recording writer. Mirrors
/// RtspMediaSessionIntegrationTests: the bundled ffmpeg.exe first encodes a genuine short
/// source, RtspMediaSession opens/demuxes it for real, and FfmpegRecordingSession muxes the
/// resulting packets into a real output file via real avformat/avcodec calls -- no encode call
/// is ever made by the recording writer, so a successfully decodable, correctly-dimensioned
/// output file is direct evidence of stream-copy (not re-encode).
///
/// Packets are collected from the source session first, then fed into the recording writer,
/// rather than wiring the writer live off session.PacketReceived: for a local-file source the
/// demux read loop (started inside OpenAsync, before this test can attach any handler) can run
/// to completion in well under a millisecond, so a live subscription races the read loop and
/// can miss packets non-deterministically. Collecting first removes that race entirely.
/// </summary>
public class RecordingIntegrationTests : IDisposable
{
    private static readonly string FfmpegExecutablePath =
        Path.Combine(AppContext.BaseDirectory, "ffmpeg", "win-x64", "ffmpeg.exe");

    private readonly string _sourceFilePath =
        Path.Combine(Path.GetTempPath(), $"vsp-recording-source-{Guid.NewGuid():N}.mjpeg");

    private readonly string _outputFilePath =
        Path.Combine(Path.GetTempPath(), $"vsp-recording-output-{Guid.NewGuid():N}.mp4");

    [Fact]
    public async Task Recording_AgainstRealFfmpegSource_ProducesPlayableStreamCopiedFile()
    {
        Assert.True(File.Exists(FfmpegExecutablePath), $"Bundled ffmpeg.exe not found at {FfmpegExecutablePath}.");

        await EncodeRealTestSourceAsync();
        Assert.True(File.Exists(_sourceFilePath), "ffmpeg did not produce the expected test source file.");

        List<EncodedFrame> capturedPackets;

        using (var session = new RtspMediaSession(_sourceFilePath, TimeSpan.FromSeconds(5)))
        {
            var packets = new List<EncodedFrame>();
            var sessionEndedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // Subscribed before OpenAsync so no packet can be missed by the read loop, which
            // starts as part of OpenAsync itself.
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
                    sessionEndedTcs.TrySetResult();
                }
            };

            await session.OpenAsync(CancellationToken.None);

            // A file source reaches real EOF (Faulted) once fully read; that is the correct,
            // deterministic signal that every packet has been delivered -- not a fixed count.
            var completed = await Task.WhenAny(sessionEndedTcs.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.Same(sessionEndedTcs.Task, completed);

            await session.CloseAsync();

            lock (packets)
            {
                capturedPackets = new List<EncodedFrame>(packets);
            }
        }

        Assert.True(capturedPackets.Count > 0, "No packets were demuxed from the real FFmpeg-encoded source.");

        using (var session = new RtspMediaSession(_sourceFilePath, TimeSpan.FromSeconds(5)))
        {
            // Re-opened only so FfmpegRecordingSession can read codec parameters/time_base;
            // the actual packets written are the ones already captured above.
            await session.OpenAsync(CancellationToken.None);

            using var recordingSession = new FfmpegRecordingSession(session);
            await recordingSession.StartAsync(_outputFilePath, CancellationToken.None);
            recordingSession.Start();

            foreach (var packet in capturedPackets)
            {
                recordingSession.OnFrame(packet);
            }

            recordingSession.Stop();
            await recordingSession.StopAsync();
            await session.CloseAsync();
        }

        Assert.True(File.Exists(_outputFilePath), "FfmpegRecordingSession did not produce an output file.");
        Assert.True(new FileInfo(_outputFilePath).Length > 0, "Recorded output file is empty.");

        List<EncodedFrame> verifyPackets;

        using (var verifySession = new RtspMediaSession(_outputFilePath, TimeSpan.FromSeconds(5)))
        {
            var packets = new List<EncodedFrame>();
            var sessionEndedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            // Subscribed before OpenAsync -- same reasoning as the capture phase above: the
            // read loop for this small local file can finish before a post-Open subscription
            // would ever attach.
            verifySession.PacketReceived += (_, e) =>
            {
                lock (packets)
                {
                    packets.Add(e.Packet);
                }
            };
            verifySession.StateChanged += (_, se) =>
            {
                if (se.State is MediaSessionState.Faulted or MediaSessionState.Closed)
                {
                    sessionEndedTcs.TrySetResult();
                }
            };

            await verifySession.OpenAsync(CancellationToken.None);
            Assert.Equal(MediaSessionState.Open, verifySession.State);

            var completed = await Task.WhenAny(sessionEndedTcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(sessionEndedTcs.Task, completed);

            await verifySession.CloseAsync();

            lock (packets)
            {
                verifyPackets = new List<EncodedFrame>(packets);
            }
        }

        Assert.True(verifyPackets.Count > 0, "The recorded output file produced no packets when re-opened.");

        using (var verifySession = new RtspMediaSession(_outputFilePath, TimeSpan.FromSeconds(5)))
        {
            await verifySession.OpenAsync(CancellationToken.None);

            using var decoder = new FfmpegVideoDecoder(verifySession);

            DecodedFrame? decodedFrame = null;
            foreach (var packet in verifyPackets)
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
            Assert.NotNull(decodedFrame.PixelBuffer);
            Assert.Contains(decodedFrame.PixelBuffer, b => b != 0);

            await verifySession.CloseAsync();
        }
    }

    private async Task EncodeRealTestSourceAsync()
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
            if (File.Exists(_sourceFilePath))
            {
                File.Delete(_sourceFilePath);
            }

            if (File.Exists(_outputFilePath))
            {
                File.Delete(_outputFilePath);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
