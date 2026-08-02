using System.Windows.Threading;
using VSP.Core.Logging;
using VSP.Player.Control;
using VSP.Player.Entities;
using VSP.Player.Interfaces;
using VSP.Tests.Logging;
using Xunit;

namespace VSP.Tests.Player;

[Collection("AppLog")]
public class MediaControllerReconnectTests
{
    [Fact]
    public async Task OpenAlwaysFails_LogsWarningForEachFailedAttempt_WithoutLeakingRtspUrl()
    {
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        var sessions = new List<FakeMediaSession>();
        var controller = CreateController(
            sessions,
            shouldSucceedOpen: () => false,
            out _,
            maxReconnectAttempts: 1,
            reconnectDelay: TimeSpan.FromMilliseconds(20));

        await controller.StartAsync(CancellationToken.None);

        Assert.True(await WaitForStateAsync(controller, MediaControllerState.Error, TimeSpan.FromSeconds(3)));
        Assert.Equal(2, sessions.Count);
        Assert.True(recorder.Calls.Count >= 2);
        Assert.All(recorder.Calls, call =>
        {
            Assert.Equal(LogLevel.Warning, call.Level);
            Assert.NotNull(call.Exception);
            Assert.DoesNotContain("rtsp://fake/stream", call.Message);
        });
    }

    [Fact]
    public async Task StartAsync_SuccessfulOpen_ReachesConnectedAndRecordsStatistics()
    {
        var sessions = new List<FakeMediaSession>();
        var controller = CreateController(sessions, shouldSucceedOpen: () => true, out _);

        await controller.StartAsync(CancellationToken.None);

        Assert.True(await WaitForStateAsync(controller, MediaControllerState.Connected));
        Assert.NotNull(controller.Statistics.ConnectedSince);
        Assert.Equal(0, controller.Statistics.ReconnectAttempts);
    }

    [Fact]
    public async Task PacketReceived_DecodesAndDispatchesFrames()
    {
        var sessions = new List<FakeMediaSession>();
        var controller = CreateController(sessions, shouldSucceedOpen: () => true, out var decoder);

        await controller.StartAsync(CancellationToken.None);
        Assert.True(await WaitForStateAsync(controller, MediaControllerState.Connected));

        sessions[0].SimulatePacket(CreateEncodedFrame());

        Assert.True(await WaitUntilAsync(() => decoder.DecodeCallCount > 0, TimeSpan.FromSeconds(2)));
        Assert.True(await WaitUntilAsync(() => controller.DispatcherMetrics.FramesPerSecond > 0, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task SessionFault_ReconnectsAndReturnsToConnected()
    {
        var sessions = new List<FakeMediaSession>();
        var controller = CreateController(
            sessions,
            shouldSucceedOpen: () => true,
            out _,
            reconnectDelay: TimeSpan.FromMilliseconds(30));

        await controller.StartAsync(CancellationToken.None);
        Assert.True(await WaitForStateAsync(controller, MediaControllerState.Connected));

        sessions[0].SimulateFault(new MediaError { Category = MediaErrorCategory.Protocol, Message = "simulated network drop" });

        Assert.True(await WaitUntilAsync(() => sessions.Count >= 2, TimeSpan.FromSeconds(2)));
        Assert.True(await WaitForStateAsync(controller, MediaControllerState.Connected));
        Assert.Equal("simulated network drop", controller.Statistics.LastError?.Message);
        Assert.True(sessions[0].WasDisposed);
    }

    [Fact]
    public async Task OpenAlwaysFails_ExceedsMaxAttemptsAndReachesError()
    {
        var sessions = new List<FakeMediaSession>();
        var controller = CreateController(
            sessions,
            shouldSucceedOpen: () => false,
            out _,
            maxReconnectAttempts: 1,
            reconnectDelay: TimeSpan.FromMilliseconds(20));

        await controller.StartAsync(CancellationToken.None);

        Assert.True(await WaitForStateAsync(controller, MediaControllerState.Error, TimeSpan.FromSeconds(3)));
        Assert.Equal(2, sessions.Count);
    }

    [Fact]
    public async Task StopAsync_StopsWithoutFurtherReconnectAttempts()
    {
        var sessions = new List<FakeMediaSession>();
        var controller = CreateController(sessions, shouldSucceedOpen: () => true, out _);

        await controller.StartAsync(CancellationToken.None);
        Assert.True(await WaitForStateAsync(controller, MediaControllerState.Connected));

        await controller.StopAsync();

        Assert.Equal(MediaControllerState.Disconnected, controller.State);
        Assert.True(sessions[0].WasClosed);

        var countAfterStop = sessions.Count;
        await Task.Delay(150);
        Assert.Equal(countAfterStop, sessions.Count);
    }

    [Fact]
    public async Task PauseAndResume_ToggleStateAndRejectInvalidTransitions()
    {
        var sessions = new List<FakeMediaSession>();
        var controller = CreateController(sessions, shouldSucceedOpen: () => true, out _);

        await controller.StartAsync(CancellationToken.None);
        Assert.True(await WaitForStateAsync(controller, MediaControllerState.Connected));

        await controller.PauseAsync();
        Assert.Equal(MediaControllerState.Paused, controller.State);

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.PauseAsync());

        await controller.ResumeAsync();
        Assert.Equal(MediaControllerState.Connected, controller.State);

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.ResumeAsync());
    }

    [Fact]
    public async Task Dispose_StopsLoopAndRejectsFurtherUse()
    {
        var sessions = new List<FakeMediaSession>();
        var controller = CreateController(sessions, shouldSucceedOpen: () => true, out _);

        await controller.StartAsync(CancellationToken.None);
        Assert.True(await WaitForStateAsync(controller, MediaControllerState.Connected));

        controller.Dispose();

        Assert.Throws<ObjectDisposedException>(() => controller.StartAsync(CancellationToken.None).GetAwaiter().GetResult());
    }

    private static MediaController CreateController(
        List<FakeMediaSession> sessions,
        Func<bool> shouldSucceedOpen,
        out FakeVideoDecoder decoder,
        int maxReconnectAttempts = 5,
        TimeSpan? reconnectDelay = null)
    {
        var capturedDecoder = new FakeVideoDecoder();
        decoder = capturedDecoder;

        IMediaSession SessionFactory(string url)
        {
            var session = new FakeMediaSession(shouldSucceedOpen);
            sessions.Add(session);
            return session;
        }

        return new MediaController(
            "rtsp://fake/stream",
            Dispatcher.CurrentDispatcher,
            SessionFactory,
            _ => capturedDecoder,
            maxReconnectAttempts,
            reconnectDelay ?? TimeSpan.FromMilliseconds(30));
    }

    private static EncodedFrame CreateEncodedFrame()
    {
        return new EncodedFrame
        {
            Data = [1, 2, 3, 4],
            Timestamp = new FrameTimestamp { DecodeTimestamp = TimeSpan.Zero, PresentationTimestamp = TimeSpan.Zero },
            IsKeyFrame = true
        };
    }

    private static Task<bool> WaitForStateAsync(IMediaController controller, MediaControllerState state, TimeSpan? timeout = null)
    {
        return WaitUntilAsync(() => controller.State == state, timeout ?? TimeSpan.FromSeconds(2));
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

    internal sealed class FakeMediaSession : IMediaSession
    {
        private readonly Func<bool> _shouldSucceedOpen;

        public FakeMediaSession(Func<bool> shouldSucceedOpen)
        {
            _shouldSucceedOpen = shouldSucceedOpen;
        }

        public VideoSourceKind Kind => VideoSourceKind.Live;

        public MediaSessionState State { get; private set; } = MediaSessionState.Idle;

        public bool WasClosed { get; private set; }

        public bool WasDisposed { get; private set; }

        public event EventHandler<MediaSessionStateChangedEventArgs>? StateChanged;

        public event EventHandler<EncodedPacketReceivedEventArgs>? PacketReceived;

        public Task OpenAsync(CancellationToken cancellationToken)
        {
            if (!_shouldSucceedOpen())
            {
                var error = new MediaError { Category = MediaErrorCategory.Connection, Message = "simulated open failure" };
                State = MediaSessionState.Faulted;
                StateChanged?.Invoke(this, new MediaSessionStateChangedEventArgs { State = State, Error = error });
                throw new InvalidOperationException("simulated open failure");
            }

            State = MediaSessionState.Open;
            StateChanged?.Invoke(this, new MediaSessionStateChangedEventArgs { State = State });
            return Task.CompletedTask;
        }

        public Task CloseAsync()
        {
            WasClosed = true;
            State = MediaSessionState.Closed;
            StateChanged?.Invoke(this, new MediaSessionStateChangedEventArgs { State = State });
            return Task.CompletedTask;
        }

        public void SimulateFault(MediaError error)
        {
            State = MediaSessionState.Faulted;
            StateChanged?.Invoke(this, new MediaSessionStateChangedEventArgs { State = State, Error = error });
        }

        public void SimulatePacket(EncodedFrame frame)
        {
            PacketReceived?.Invoke(this, new EncodedPacketReceivedEventArgs { Packet = frame });
        }

        public void Dispose()
        {
            WasDisposed = true;
        }
    }

    internal sealed class FakeVideoDecoder : IVideoDecoder
    {
        public int DecodeCallCount { get; private set; }

        public bool WasDisposed { get; private set; }

        public DecodedFrame? Decode(EncodedFrame encodedFrame)
        {
            DecodeCallCount++;
            return new DecodedFrame
            {
                Storage = FrameStorage.Cpu,
                Width = 2,
                Height = 2,
                PixelFormat = FramePixelFormat.Bgra32,
                Timestamp = encodedFrame.Timestamp,
                PixelBuffer = new byte[2 * 2 * 4],
                Stride = 2 * 4
            };
        }

        public void Reset()
        {
        }

        public void Dispose()
        {
            WasDisposed = true;
        }
    }
}
