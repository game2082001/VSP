using System.Windows.Threading;
using VSP.Player.Control;
using VSP.Player.Decoder;
using VSP.Player.Entities;
using VSP.Player.Interfaces;
using Xunit;

namespace VSP.Tests.Player;

public class PlaybackControllerTests
{
    [Fact]
    public async Task StartAsync_SuccessfulOpen_ReachesConnectedAndSetsDuration()
    {
        var session = new FakeMediaSession(shouldSucceedOpen: true, duration: TimeSpan.FromSeconds(30));
        using var controller = CreateController(_ => session, _ => new FakeVideoDecoder());

        await controller.StartAsync(CancellationToken.None);

        Assert.True(await WaitForStateAsync(controller, MediaControllerState.Connected));
        Assert.Equal(TimeSpan.FromSeconds(30), controller.Duration);
    }

    [Fact]
    public async Task SessionReachesClosed_TransitionsToDisconnectedNotError()
    {
        var session = new FakeMediaSession(shouldSucceedOpen: true, duration: TimeSpan.FromSeconds(5));
        using var controller = CreateController(_ => session, _ => new FakeVideoDecoder());

        await controller.StartAsync(CancellationToken.None);
        Assert.True(await WaitForStateAsync(controller, MediaControllerState.Connected));

        session.SimulateClosed();

        Assert.True(await WaitForStateAsync(controller, MediaControllerState.Disconnected));
    }

    [Fact]
    public async Task SessionFaults_TransitionsToError()
    {
        var session = new FakeMediaSession(shouldSucceedOpen: true, duration: null);
        using var controller = CreateController(_ => session, _ => new FakeVideoDecoder());

        await controller.StartAsync(CancellationToken.None);
        Assert.True(await WaitForStateAsync(controller, MediaControllerState.Connected));

        session.SimulateFault(new MediaError { Category = MediaErrorCategory.Protocol, Message = "boom" });

        Assert.True(await WaitForStateAsync(controller, MediaControllerState.Error));
    }

    [Fact]
    public async Task PauseAsync_PausesSessionReading_ResumeAsyncResumes()
    {
        var session = new FakeMediaSession(shouldSucceedOpen: true, duration: TimeSpan.FromSeconds(5));
        using var controller = CreateController(_ => session, _ => new FakeVideoDecoder());

        await controller.StartAsync(CancellationToken.None);
        Assert.True(await WaitForStateAsync(controller, MediaControllerState.Connected));

        await controller.PauseAsync();
        Assert.Equal(MediaControllerState.Paused, controller.State);
        Assert.True(session.PauseReadingCalled);

        await controller.ResumeAsync();
        Assert.Equal(MediaControllerState.Connected, controller.State);
        Assert.True(session.ResumeReadingCalled);
    }

    [Fact]
    public async Task ClockSeek_CallsTrySeekOnSessionAndResetsDecoder()
    {
        var session = new FakeMediaSession(shouldSucceedOpen: true, duration: TimeSpan.FromSeconds(5));
        var decoder = new FakeVideoDecoder();
        using var controller = CreateController(_ => session, _ => decoder);

        await controller.StartAsync(CancellationToken.None);
        Assert.True(await WaitForStateAsync(controller, MediaControllerState.Connected));

        session.SeekResult = TimeSpan.FromSeconds(2);
        var achieved = controller.Clock.Seek(TimeSpan.FromSeconds(2));

        Assert.Equal(TimeSpan.FromSeconds(2), achieved);
        Assert.Equal(TimeSpan.FromSeconds(2), session.LastSeekTarget);
        Assert.True(decoder.ResetCalled);
        Assert.Equal(TimeSpan.FromSeconds(2), controller.Clock.CurrentPresentationTime);
    }

    [Fact]
    public async Task ClockSeek_WhenSessionRejectsSeek_ReturnsNullAndDoesNotResetDecoder()
    {
        var session = new FakeMediaSession(shouldSucceedOpen: true, duration: TimeSpan.FromSeconds(5));
        var decoder = new FakeVideoDecoder();
        using var controller = CreateController(_ => session, _ => decoder);

        await controller.StartAsync(CancellationToken.None);
        Assert.True(await WaitForStateAsync(controller, MediaControllerState.Connected));

        session.SeekResult = null;
        var achieved = controller.Clock.Seek(TimeSpan.FromSeconds(2));

        Assert.Null(achieved);
        Assert.False(decoder.ResetCalled);
    }

    [Fact]
    public async Task StartRecordingAsyncAndStopRecordingAsync_Throw()
    {
        var session = new FakeMediaSession(shouldSucceedOpen: true, duration: null);
        using var controller = CreateController(_ => session, _ => new FakeVideoDecoder());

        await Assert.ThrowsAsync<NotSupportedException>(() => controller.StartRecordingAsync());
        await Assert.ThrowsAsync<NotSupportedException>(() => controller.StopRecordingAsync());
        Assert.False(controller.IsRecording);
    }

    [Fact]
    public async Task StopAsync_StopsCleanly()
    {
        var session = new FakeMediaSession(shouldSucceedOpen: true, duration: TimeSpan.FromSeconds(5));
        using var controller = CreateController(_ => session, _ => new FakeVideoDecoder());

        await controller.StartAsync(CancellationToken.None);
        Assert.True(await WaitForStateAsync(controller, MediaControllerState.Connected));

        await controller.StopAsync();

        Assert.Equal(MediaControllerState.Disconnected, controller.State);
    }

    [Fact]
    public async Task OpenAlwaysFails_ReachesError()
    {
        var session = new FakeMediaSession(shouldSucceedOpen: false, duration: null);
        using var controller = CreateController(_ => session, _ => new FakeVideoDecoder());

        await controller.StartAsync(CancellationToken.None);

        Assert.True(await WaitForStateAsync(controller, MediaControllerState.Error));
    }

    private static PlaybackController CreateController(
        Func<string, IMediaSession> sessionFactory,
        Func<IMediaSession, IVideoDecoder> decoderFactory)
    {
        return new PlaybackController(@"C:\fake\recording.mp4", Dispatcher.CurrentDispatcher, sessionFactory, decoderFactory);
    }

    private static Task<bool> WaitForStateAsync(PlaybackController controller, MediaControllerState state, TimeSpan? timeout = null)
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

    internal sealed class FakeMediaSession : IMediaSession, IPlaybackControl
    {
        private readonly bool _shouldSucceedOpen;

        public FakeMediaSession(bool shouldSucceedOpen, TimeSpan? duration)
        {
            _shouldSucceedOpen = shouldSucceedOpen;
            Duration = duration;
        }

        public VideoSourceKind Kind => VideoSourceKind.RecordedFile;

        public MediaSessionState State { get; private set; } = MediaSessionState.Idle;

        public TimeSpan? Duration { get; }

        public TimeSpan? SeekResult { get; set; }

        public TimeSpan? LastSeekTarget { get; private set; }

        public bool PauseReadingCalled { get; private set; }

        public bool ResumeReadingCalled { get; private set; }

        public event EventHandler<MediaSessionStateChangedEventArgs>? StateChanged;

        public event EventHandler<EncodedPacketReceivedEventArgs>? PacketReceived;

        public Task OpenAsync(CancellationToken cancellationToken)
        {
            if (!_shouldSucceedOpen)
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
            State = MediaSessionState.Closed;
            return Task.CompletedTask;
        }

        public void SimulateClosed()
        {
            State = MediaSessionState.Closed;
            StateChanged?.Invoke(this, new MediaSessionStateChangedEventArgs { State = State });
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

        public TimeSpan? TrySeek(TimeSpan target)
        {
            LastSeekTarget = target;
            return SeekResult;
        }

        public void PauseReading() => PauseReadingCalled = true;

        public void ResumeReading() => ResumeReadingCalled = true;

        public void Dispose()
        {
        }
    }

    internal sealed class FakeVideoDecoder : IVideoDecoder
    {
        public bool ResetCalled { get; private set; }

        public DecodedFrame? Decode(EncodedFrame encodedFrame) => null;

        public void Reset() => ResetCalled = true;

        public void Dispose()
        {
        }
    }
}
