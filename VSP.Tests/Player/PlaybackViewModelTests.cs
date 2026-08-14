using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using VSP.Device.Interfaces;
using VSP.Device.Services;
using VSP.Player.Entities;
using VSP.Player.Interfaces;
using VSP.Player.Pipeline;
using VSP.Player.Recording;
using VSP.UI.ViewModels;
using Xunit;
using EntityCamera = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Player;

public class PlaybackViewModelTests
{
    [Fact]
    public async Task LoadCamerasAsync_PopulatesCamerasFromRepository()
    {
        var camera = new EntityCamera { Name = "Front Door" };
        var viewModel = CreateViewModel(new FakeCameraRepository(camera), out _);

        await viewModel.LoadCamerasAsync();

        Assert.Single(viewModel.Cameras);
        Assert.Equal(camera, viewModel.Cameras[0]);
    }

    [Fact]
    public void SelectingCamera_WithNoRecordings_ReportsNoneFound()
    {
        var camera = new EntityCamera { Name = "Front Door", Id = Guid.NewGuid() };
        var viewModel = CreateViewModel(new FakeCameraRepository(camera), out _);

        viewModel.SelectedCamera = camera;

        Assert.Empty(viewModel.Recordings);
        Assert.Contains("No recordings found", viewModel.StatusMessage);
    }

    [Fact]
    public void PlayCommand_NotExecutableWithoutSelectedRecording()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(), out _);

        Assert.False(viewModel.PlayCommand.CanExecute(null));
    }

    [Fact]
    public void PlayCommand_ExecutableOnceRecordingSelected()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(), out _);

        viewModel.SelectedRecording = new RecordingItem(new RecordingFileInfo(@"C:\fake\recording.mp4", DateTime.Now));

        Assert.True(viewModel.PlayCommand.CanExecute(null));
    }

    [Fact]
    public void PlayCommand_StartsControllerAgainstSelectedRecordingPath()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(), out var controller);
        viewModel.SelectedRecording = new RecordingItem(new RecordingFileInfo(@"C:\fake\recording.mp4", DateTime.Now));

        viewModel.PlayCommand.Execute(null);

        Assert.True(controller.StartCalled);
        Assert.True(viewModel.HasActiveSession);
    }

    [Fact]
    public void ControllerStateChanged_ToConnected_UpdatesStatusMessage()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(), out var controller);
        var dispatcher = Dispatcher.CurrentDispatcher;
        viewModel.SelectedRecording = new RecordingItem(new RecordingFileInfo(@"C:\fake\recording.mp4", DateTime.Now));

        viewModel.PlayCommand.Execute(null);
        controller.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        Assert.Contains("Playing", viewModel.StatusMessage);
    }

    [Fact]
    public void PauseCommand_OnlyExecutableWhenConnected()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(), out var controller);
        var dispatcher = Dispatcher.CurrentDispatcher;
        viewModel.SelectedRecording = new RecordingItem(new RecordingFileInfo(@"C:\fake\recording.mp4", DateTime.Now));
        viewModel.PlayCommand.Execute(null);

        Assert.False(viewModel.PauseCommand.CanExecute(null));

        controller.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        Assert.True(viewModel.PauseCommand.CanExecute(null));

        viewModel.PauseCommand.Execute(null);
        PumpDispatcher(dispatcher);

        Assert.True(controller.PauseCalled);
    }

    [Fact]
    public void StopCommand_StopsController()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(), out var controller);
        var dispatcher = Dispatcher.CurrentDispatcher;
        viewModel.SelectedRecording = new RecordingItem(new RecordingFileInfo(@"C:\fake\recording.mp4", DateTime.Now));
        viewModel.PlayCommand.Execute(null);
        controller.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        viewModel.StopCommand.Execute(null);
        PumpDispatcher(dispatcher);

        Assert.True(controller.StopCalled);
    }

    [Fact]
    public void Seek_WithNoActiveSession_DoesNotThrow()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(), out _);

        var exception = Record.Exception(() => viewModel.Seek(TimeSpan.FromSeconds(5)));

        Assert.Null(exception);
    }

    [Fact]
    public async Task Seek_WithActiveSession_CallsClockSeek()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(), out var controller);
        viewModel.SelectedRecording = new RecordingItem(new RecordingFileInfo(@"C:\fake\recording.mp4", DateTime.Now));
        viewModel.PlayCommand.Execute(null);

        viewModel.Seek(TimeSpan.FromSeconds(3));

        Assert.True(await WaitUntilAsync(() => controller.SeekCalledWith == TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2)));
    }

    // RC1-R05 (Playback Frame Presentation Lifecycle): direct Playback equivalents of the
    // proven RC1-R03 Live View FrameRendered tests -- CurrentFrameSource is updated in place by
    // the renderer on every frame (no new object identity), so WPF's binding never re-pulls it
    // unless something explicitly raises PropertyChanged, which was previously never wired here.
    [Fact]
    public void FrameRendered_RaisesPropertyChangedForCurrentFrameSource()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(), out var controller);
        var dispatcher = Dispatcher.CurrentDispatcher;
        viewModel.SelectedRecording = new RecordingItem(new RecordingFileInfo(@"C:\fake\recording.mp4", DateTime.Now));

        viewModel.PlayCommand.Execute(null);
        controller.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        // Matches the real-device symptom (RC1-R05 RCA): Connected fires before any frame has
        // decoded, so CurrentFrameSource is still null at this point.
        Assert.Null(viewModel.CurrentFrameSource);

        var raisedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName);

        var fakeRenderer = (LiveViewViewModelTests.FakeFrameRenderer)controller.Renderer;
        fakeRenderer.CurrentFrameSource = new BitmapImage();
        fakeRenderer.RaiseFrameRendered();
        PumpDispatcher(dispatcher);

        Assert.Contains(nameof(PlaybackViewModel.CurrentFrameSource), raisedProperties);
        Assert.Same(fakeRenderer.CurrentFrameSource, viewModel.CurrentFrameSource);
    }

    [Fact]
    public void FrameRendered_AfterStopThenReplacedController_SubscriptionIsRemoved()
    {
        var controller1 = new FakePlaybackController();
        var controller2 = new FakePlaybackController();
        var pendingControllers = new Queue<FakePlaybackController>(new[] { controller1, controller2 });
        var dispatcher = Dispatcher.CurrentDispatcher;
        var viewModel = new PlaybackViewModel(
            new CameraQueryService(new FakeCameraRepository()),
            dispatcher,
            (_, _) => pendingControllers.Dequeue());

        viewModel.SelectedRecording = new RecordingItem(new RecordingFileInfo(@"C:\fake\recording.mp4", DateTime.Now));
        viewModel.PlayCommand.Execute(null);
        controller1.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        // Stop -> Play on the same recording: reproduces the exact real-device sequence from the
        // RC1-R05 RCA. Loading a second controller detaches controller1 -- must unsubscribe from
        // controller1.Renderer.FrameRendered as part of that detach.
        viewModel.StopCommand.Execute(null);
        PumpDispatcher(dispatcher);
        viewModel.PlayCommand.Execute(null);

        var raisedProperties = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raisedProperties.Add(e.PropertyName);

        // Simulates a frame callback from controller1 still in flight (e.g. already queued on
        // the dispatcher) at the moment it was detached.
        var staleRenderer = (LiveViewViewModelTests.FakeFrameRenderer)controller1.Renderer;
        staleRenderer.CurrentFrameSource = new BitmapImage();
        staleRenderer.RaiseFrameRendered();
        PumpDispatcher(dispatcher);

        Assert.DoesNotContain(nameof(PlaybackViewModel.CurrentFrameSource), raisedProperties);
    }

    [Fact]
    public void FrameRendered_FromOldControllerAfterStopPlay_CannotUpdateCurrentPlaybackPresentation()
    {
        var controller1 = new FakePlaybackController();
        var controller2 = new FakePlaybackController();
        var pendingControllers = new Queue<FakePlaybackController>(new[] { controller1, controller2 });
        var dispatcher = Dispatcher.CurrentDispatcher;
        var viewModel = new PlaybackViewModel(
            new CameraQueryService(new FakeCameraRepository()),
            dispatcher,
            (_, _) => pendingControllers.Dequeue());

        viewModel.SelectedRecording = new RecordingItem(new RecordingFileInfo(@"C:\fake\recording.mp4", DateTime.Now));
        viewModel.PlayCommand.Execute(null);
        controller1.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        viewModel.StopCommand.Execute(null);
        PumpDispatcher(dispatcher);
        viewModel.PlayCommand.Execute(null);
        controller2.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        var currentRenderer = (LiveViewViewModelTests.FakeFrameRenderer)controller2.Renderer;
        currentRenderer.CurrentFrameSource = new BitmapImage();
        currentRenderer.RaiseFrameRendered();
        PumpDispatcher(dispatcher);

        Assert.Same(currentRenderer.CurrentFrameSource, viewModel.CurrentFrameSource);

        // A late frame from the now-detached controller1 must never leak into the ViewModel
        // that's now hosting controller2 -- neither by updating CurrentFrameSource nor by
        // spuriously notifying it.
        var staleRenderer = (LiveViewViewModelTests.FakeFrameRenderer)controller1.Renderer;
        staleRenderer.CurrentFrameSource = new BitmapImage();
        staleRenderer.RaiseFrameRendered();
        PumpDispatcher(dispatcher);

        Assert.Same(currentRenderer.CurrentFrameSource, viewModel.CurrentFrameSource);
        Assert.NotSame(staleRenderer.CurrentFrameSource, viewModel.CurrentFrameSource);
    }

    private static PlaybackViewModel CreateViewModel(ICameraRepository repository, out FakePlaybackController controller)
    {
        var capturedController = new FakePlaybackController();
        controller = capturedController;
        return new PlaybackViewModel(
            new CameraQueryService(repository),
            Dispatcher.CurrentDispatcher,
            (_, _) => capturedController);
    }

    private static void PumpDispatcher(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
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

    private sealed class FakeCameraRepository : ICameraRepository
    {
        private readonly List<EntityCamera> _cameras;

        public FakeCameraRepository(params EntityCamera[] cameras)
        {
            _cameras = cameras.ToList();
        }

        public IEnumerable<EntityCamera> GetAll() => _cameras;

        public EntityCamera? GetById(Guid id) => _cameras.FirstOrDefault(camera => camera.Id == id);

        public void Add(EntityCamera camera) => _cameras.Add(camera);

        public void Update(EntityCamera camera)
        {
        }

        public void Delete(Guid id)
        {
        }
    }

    internal sealed class FakePlaybackController : IMediaController
    {
        private readonly PlaybackClock _clock;

        public FakePlaybackController()
        {
            _clock = new PlaybackClock(target =>
            {
                SeekCalledWith = target;
                return target;
            });
        }

        public MediaControllerState State { get; private set; } = MediaControllerState.Idle;

        public MediaSessionStatistics Statistics { get; } = MediaSessionStatistics.Empty;

        public IFrameRenderer Renderer { get; } = new LiveViewViewModelTests.FakeFrameRenderer();

        public IDispatcherMetrics DispatcherMetrics { get; } = new LiveViewViewModelTests.FakeDispatcherMetrics();

        public IMediaClock Clock => _clock;

        public TimeSpan? Duration => TimeSpan.FromSeconds(60);

        public bool IsRecording => false;

        public bool StartCalled { get; private set; }

        public bool StopCalled { get; private set; }

        public bool PauseCalled { get; private set; }

        public TimeSpan? SeekCalledWith { get; private set; }

        public event EventHandler<MediaControllerStateChangedEventArgs>? StateChanged;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            StartCalled = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            StopCalled = true;
            return Task.CompletedTask;
        }

        public Task PauseAsync()
        {
            PauseCalled = true;
            return Task.CompletedTask;
        }

        public Task ResumeAsync() => Task.CompletedTask;

        public Task StartRecordingAsync() => throw new NotSupportedException();

        public Task StopRecordingAsync() => throw new NotSupportedException();

        public void Dispose()
        {
        }

        public void RaiseStateChanged(MediaControllerState state, MediaError? error = null)
        {
            State = state;
            StateChanged?.Invoke(this, new MediaControllerStateChangedEventArgs { State = state, Error = error });
        }
    }

}
