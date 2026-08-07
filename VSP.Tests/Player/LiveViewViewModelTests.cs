using System.Windows.Media;
using System.Windows.Threading;
using VSP.Domain.Enums;
using VSP.Player.Entities;
using VSP.Player.Interfaces;
using VSP.UI.ViewModels;
using Xunit;
using EntityCamera = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Player;

public class LiveViewViewModelTests
{
    [Fact]
    public void LoadCamera_WithRtspUrl_StartsController()
    {
        var controller = new FakeMediaController();
        var viewModel = new LiveViewViewModel(Dispatcher.CurrentDispatcher, (_, _) => controller);

        var camera = new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://192.168.1.10/stream", ConnectionType = DeviceConnectionType.RTSP };
        viewModel.LoadCamera(camera);

        Assert.True(viewModel.HasCamera);
        Assert.Equal(camera, viewModel.Camera);
        Assert.True(controller.StartCalled);
    }

    [Fact]
    public void LoadCamera_WithoutRtspUrl_DoesNotStartControllerAndReportsStatus()
    {
        var controller = new FakeMediaController();
        var viewModel = new LiveViewViewModel(Dispatcher.CurrentDispatcher, (_, _) => controller);

        var camera = new EntityCamera { Name = "Unconfigured", RtspUrl = "", ConnectionType = DeviceConnectionType.RTSP };
        viewModel.LoadCamera(camera);

        Assert.True(viewModel.HasCamera);
        Assert.False(controller.StartCalled);
        Assert.Contains("no stream URL configured", viewModel.StatusMessage);
    }

    [Fact]
    public void ControllerStateChanged_ToConnected_UpdatesStatusAndState()
    {
        var controller = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var viewModel = new LiveViewViewModel(dispatcher, (_, _) => controller);

        viewModel.LoadCamera(new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://host/stream", ConnectionType = DeviceConnectionType.RTSP });
        controller.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        Assert.Equal(MediaControllerState.Connected, viewModel.State);
        Assert.Contains("Live:", viewModel.StatusMessage);
    }

    [Fact]
    public void ControllerStateChanged_ToError_IncludesErrorMessage()
    {
        var controller = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var viewModel = new LiveViewViewModel(dispatcher, (_, _) => controller);

        viewModel.LoadCamera(new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://host/stream", ConnectionType = DeviceConnectionType.RTSP });
        controller.RaiseStateChanged(
            MediaControllerState.Error,
            new MediaError { Category = MediaErrorCategory.Connection, Message = "connection refused" });
        PumpDispatcher(dispatcher);

        Assert.Contains("connection refused", viewModel.StatusMessage);
    }

    [Fact]
    public void PauseCommand_OnlyExecutableWhenConnected()
    {
        var controller = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var viewModel = new LiveViewViewModel(dispatcher, (_, _) => controller);

        viewModel.LoadCamera(new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://host/stream", ConnectionType = DeviceConnectionType.RTSP });

        Assert.False(viewModel.PauseCommand.CanExecute(null));

        controller.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        Assert.True(viewModel.PauseCommand.CanExecute(null));
    }

    [Fact]
    public void StartRecordingCommand_OnlyExecutableWhenConnectedAndNotRecording()
    {
        var controller = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var viewModel = new LiveViewViewModel(dispatcher, (_, _) => controller);

        viewModel.LoadCamera(new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://host/stream", ConnectionType = DeviceConnectionType.RTSP });

        Assert.False(viewModel.StartRecordingCommand.CanExecute(null));

        controller.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        Assert.True(viewModel.StartRecordingCommand.CanExecute(null));
        Assert.False(viewModel.StopRecordingCommand.CanExecute(null));

        // FakeMediaController.StartRecordingAsync/StopRecordingAsync complete synchronously
        // (Task.CompletedTask), so the queued RaiseAllChanged dispatcher callback is already
        // pending by the time Execute returns -- no real async handoff, so no need to leave
        // this thread (PumpDispatcher below must run on the same thread that created
        // `dispatcher`; hopping via Task.Delay here would move the continuation to an
        // arbitrary thread-pool thread and hang PushFrame on the wrong thread).
        viewModel.StartRecordingCommand.Execute(null);
        PumpDispatcher(dispatcher);

        Assert.True(controller.StartRecordingCalled);
        Assert.True(viewModel.IsRecording);
        Assert.False(viewModel.StartRecordingCommand.CanExecute(null));
        Assert.True(viewModel.StopRecordingCommand.CanExecute(null));

        viewModel.StopRecordingCommand.Execute(null);
        PumpDispatcher(dispatcher);

        Assert.True(controller.StopRecordingCalled);
        Assert.False(viewModel.IsRecording);
    }

    // Epic-018 Decision 1 (§8.1): Recording is Admin-only. Previously ungated -- a gap discovered
    // during Milestone 18D's manual-validation preparation, fixed under the same approved
    // decision, not a new feature.
    [Fact]
    public void OperatorRole_StartAndStopRecordingCommands_ReportCannotExecuteEvenWhenConnected()
    {
        var controller = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var viewModel = new LiveViewViewModel(dispatcher, (_, _) => controller, Role.Operator);

        viewModel.LoadCamera(new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://host/stream", ConnectionType = DeviceConnectionType.RTSP });
        controller.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        Assert.False(viewModel.StartRecordingCommand.CanExecute(null));

        // Defense in depth: even if Start were forced (RelayCommand.Execute does not itself
        // consult CanExecute), IsRecording would still be false, so Stop stays blocked too.
        Assert.False(viewModel.StopRecordingCommand.CanExecute(null));
    }

    [Fact]
    public void AdminRole_StartRecordingCommand_UnaffectedRegressionGuard()
    {
        var controller = new FakeMediaController();
        var dispatcher = Dispatcher.CurrentDispatcher;
        var viewModel = new LiveViewViewModel(dispatcher, (_, _) => controller, Role.Admin);

        viewModel.LoadCamera(new EntityCamera { Name = "Front Door", RtspUrl = "rtsp://host/stream", ConnectionType = DeviceConnectionType.RTSP });
        controller.RaiseStateChanged(MediaControllerState.Connected);
        PumpDispatcher(dispatcher);

        Assert.True(viewModel.StartRecordingCommand.CanExecute(null));
    }

    private static void PumpDispatcher(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    internal sealed class FakeMediaController : IMediaController
    {
        public MediaControllerState State { get; private set; } = MediaControllerState.Idle;

        public MediaSessionStatistics Statistics { get; } = MediaSessionStatistics.Empty;

        public IFrameRenderer Renderer { get; } = new FakeFrameRenderer();

        public IDispatcherMetrics DispatcherMetrics { get; } = new FakeDispatcherMetrics();

        public IMediaClock Clock { get; } = new VSP.Player.Pipeline.MediaClock();

        public TimeSpan? Duration => null;

        public bool IsRecording { get; private set; }

        public bool StartCalled { get; private set; }

        public bool StopCalled { get; private set; }

        public bool DisposeCalled { get; private set; }

        public bool StartRecordingCalled { get; private set; }

        public bool StopRecordingCalled { get; private set; }

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

        public Task PauseAsync() => Task.CompletedTask;

        public Task ResumeAsync() => Task.CompletedTask;

        public Task StartRecordingAsync()
        {
            StartRecordingCalled = true;
            IsRecording = true;
            return Task.CompletedTask;
        }

        public Task StopRecordingAsync()
        {
            StopRecordingCalled = true;
            IsRecording = false;
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            DisposeCalled = true;
        }

        public void RaiseStateChanged(MediaControllerState state, MediaError? error = null)
        {
            State = state;
            StateChanged?.Invoke(this, new MediaControllerStateChangedEventArgs { State = state, Error = error });
        }
    }

    internal sealed class FakeFrameRenderer : IFrameRenderer
    {
        public ImageSource? CurrentFrameSource => null;

        public bool IsActive { get; private set; }

        public event EventHandler? FrameRendered;

        public void OnFrame(DecodedFrame frame)
        {
        }

        public void Start() => IsActive = true;

        public void Stop() => IsActive = false;

        public void Dispose()
        {
        }
    }

    internal sealed class FakeDispatcherMetrics : IDispatcherMetrics
    {
        public double FramesPerSecond => 0;

        public TimeSpan AverageLatency => TimeSpan.Zero;

        public int QueueLength => 0;

        public long DroppedFrameCount => 0;

        public event EventHandler? MetricsUpdated;
    }
}
