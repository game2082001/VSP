using System.Windows.Threading;
using VSP.Player.Control;
using VSP.Player.Entities;
using VSP.Player.Interfaces;
using VSP.Player.Recording;
using Xunit;

namespace VSP.Tests.Player;

public class MediaControllerRecordingTests
{
    [Fact]
    public async Task StartRecordingAsync_WhenConnected_StartsSessionAndDispatchesEncodedFrames()
    {
        var recordingSession = new FakeRecordingSession();
        var sessions = new List<FakeMediaSession>();
        using var controller = CreateController(sessions, _ => recordingSession);

        await controller.StartAsync(CancellationToken.None);
        Assert.True(await WaitUntilAsync(() => controller.State == MediaControllerState.Connected, TimeSpan.FromSeconds(2)));

        await controller.StartRecordingAsync();

        Assert.True(controller.IsRecording);
        Assert.True(recordingSession.StartCalled);
        Assert.True(recordingSession.IsActive);
        Assert.EndsWith(".mp4", recordingSession.FilePath);

        sessions[0].SimulatePacket(CreateEncodedFrame());

        Assert.True(await WaitUntilAsync(() => recordingSession.ReceivedFrameCount > 0, TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task StartRecordingAsync_WhenNotConnected_Throws()
    {
        var sessions = new List<FakeMediaSession>();
        using var controller = CreateController(sessions, _ => new FakeRecordingSession());

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.StartRecordingAsync());
    }

    [Fact]
    public async Task StartRecordingAsync_WhenAlreadyRecording_Throws()
    {
        var sessions = new List<FakeMediaSession>();
        using var controller = CreateController(sessions, _ => new FakeRecordingSession());

        await controller.StartAsync(CancellationToken.None);
        Assert.True(await WaitUntilAsync(() => controller.State == MediaControllerState.Connected, TimeSpan.FromSeconds(2)));

        await controller.StartRecordingAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.StartRecordingAsync());
    }

    [Fact]
    public async Task StopRecordingAsync_WhenNotRecording_IsNoOp()
    {
        var sessions = new List<FakeMediaSession>();
        using var controller = CreateController(sessions, _ => new FakeRecordingSession());

        await controller.StopRecordingAsync();

        Assert.False(controller.IsRecording);
    }

    [Fact]
    public async Task StopRecordingAsync_WhileRecording_FinalizesAndClearsState()
    {
        var recordingSession = new FakeRecordingSession();
        var sessions = new List<FakeMediaSession>();
        using var controller = CreateController(sessions, _ => recordingSession);

        await controller.StartAsync(CancellationToken.None);
        Assert.True(await WaitUntilAsync(() => controller.State == MediaControllerState.Connected, TimeSpan.FromSeconds(2)));

        await controller.StartRecordingAsync();
        await controller.StopRecordingAsync();

        Assert.False(controller.IsRecording);
        Assert.True(recordingSession.StopAsyncCalled);
        Assert.True(recordingSession.DisposeCalled);
    }

    [Fact]
    public async Task StopAsync_WhileRecording_FinalizesRecordingBeforeDisconnecting()
    {
        var recordingSession = new FakeRecordingSession();
        var sessions = new List<FakeMediaSession>();
        using var controller = CreateController(sessions, _ => recordingSession);

        await controller.StartAsync(CancellationToken.None);
        Assert.True(await WaitUntilAsync(() => controller.State == MediaControllerState.Connected, TimeSpan.FromSeconds(2)));

        await controller.StartRecordingAsync();
        await controller.StopAsync();

        Assert.False(controller.IsRecording);
        Assert.True(recordingSession.StopAsyncCalled);
        Assert.Equal(MediaControllerState.Disconnected, controller.State);
    }

    [Fact]
    public async Task PauseAsync_WhileRecording_DoesNotStopRecording()
    {
        var recordingSession = new FakeRecordingSession();
        var sessions = new List<FakeMediaSession>();
        using var controller = CreateController(sessions, _ => recordingSession);

        await controller.StartAsync(CancellationToken.None);
        Assert.True(await WaitUntilAsync(() => controller.State == MediaControllerState.Connected, TimeSpan.FromSeconds(2)));

        await controller.StartRecordingAsync();
        await controller.PauseAsync();

        Assert.True(controller.IsRecording);
        Assert.False(recordingSession.StopAsyncCalled);
    }

    private static MediaController CreateController(
        List<FakeMediaSession> sessions,
        Func<IMediaSession, IRecordingSession> recordingSessionFactory)
    {
        IMediaSession SessionFactory(string url)
        {
            var session = new FakeMediaSession();
            sessions.Add(session);
            return session;
        }

        return new MediaController(
            "rtsp://fake/stream",
            Dispatcher.CurrentDispatcher,
            SessionFactory,
            _ => new FakeVideoDecoder(),
            maxReconnectAttempts: 5,
            reconnectDelay: TimeSpan.FromMilliseconds(30),
            recordingSessionFactory: recordingSessionFactory);
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
        public VideoSourceKind Kind => VideoSourceKind.Live;

        public MediaSessionState State { get; private set; } = MediaSessionState.Idle;

        public event EventHandler<MediaSessionStateChangedEventArgs>? StateChanged;

        public event EventHandler<EncodedPacketReceivedEventArgs>? PacketReceived;

        public Task OpenAsync(CancellationToken cancellationToken)
        {
            State = MediaSessionState.Open;
            StateChanged?.Invoke(this, new MediaSessionStateChangedEventArgs { State = State });
            return Task.CompletedTask;
        }

        public Task CloseAsync()
        {
            State = MediaSessionState.Closed;
            StateChanged?.Invoke(this, new MediaSessionStateChangedEventArgs { State = State });
            return Task.CompletedTask;
        }

        public void SimulatePacket(EncodedFrame frame)
        {
            PacketReceived?.Invoke(this, new EncodedPacketReceivedEventArgs { Packet = frame });
        }

        public void Dispose()
        {
        }
    }

    internal sealed class FakeVideoDecoder : IVideoDecoder
    {
        public DecodedFrame? Decode(EncodedFrame encodedFrame) => null;

        public void Reset()
        {
        }

        public void Dispose()
        {
        }
    }

    internal sealed class FakeRecordingSession : IRecordingSession
    {
        private int _receivedFrameCount;

        public IRecordingMode Mode { get; } = new ContinuousRecordingMode();

        public bool IsActive { get; private set; }

        public bool StartCalled { get; private set; }

        public string? FilePath { get; private set; }

        public bool StopAsyncCalled { get; private set; }

        public bool DisposeCalled { get; private set; }

        public int ReceivedFrameCount => _receivedFrameCount;

        public Task StartAsync(string filePath, CancellationToken cancellationToken)
        {
            StartCalled = true;
            FilePath = filePath;
            return Task.CompletedTask;
        }

        public void Start() => IsActive = true;

        public void Stop() => IsActive = false;

        public void OnFrame(EncodedFrame frame)
        {
            Interlocked.Increment(ref _receivedFrameCount);
        }

        public Task StopAsync()
        {
            StopAsyncCalled = true;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            DisposeCalled = true;
        }
    }
}
