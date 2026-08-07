using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VSP.Core.Commands;
using VSP.Core.MVVM;
using VSP.Domain.Entities;
using VSP.Domain.Enums;
using VSP.Player.Control;
using VSP.Player.Entities;
using VSP.Player.Interfaces;

namespace VSP.UI.ViewModels;

/// <summary>
/// Hosts one <see cref="IMediaController"/> for the camera selected from the Camera Workspace.
/// Camera selection always originates from CameraListViewModel via LiveViewCameraCoordinator,
/// never from a picker internal to this ViewModel/view.
/// </summary>
public class LiveViewViewModel : ObservableObject, IDisposable
{
    private readonly Dispatcher _uiDispatcher;
    private readonly Func<string, Dispatcher, IMediaController> _controllerFactory;
    private IMediaController? _controller;

    private Camera? _camera;
    public Camera? Camera
    {
        get => _camera;
        private set => SetProperty(ref _camera, value);
    }

    private bool _hasCamera;
    public bool HasCamera
    {
        get => _hasCamera;
        private set => SetProperty(ref _hasCamera, value);
    }

    private string _statusMessage = "Select a camera from the Camera Workspace to begin.";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ImageSource? CurrentFrameSource => _controller?.Renderer.CurrentFrameSource;

    public MediaControllerState State => _controller?.State ?? MediaControllerState.Idle;

    public IDispatcherMetrics? DispatcherMetrics => _controller?.DispatcherMetrics;

    public bool IsRecording => _controller?.IsRecording ?? false;

    public ICommand PauseCommand { get; }
    public ICommand ResumeCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand StartRecordingCommand { get; }
    public ICommand StopRecordingCommand { get; }

    // Role defaults to Admin -- preserves every pre-Epic-018 call site/test's existing
    // full-featured behavior unchanged; MainWindowViewModel (the one real production call site)
    // always passes the authenticated user's actual role explicitly.
    public LiveViewViewModel(Dispatcher uiDispatcher, Role role = Role.Admin)
        : this(uiDispatcher, static (rtspUrl, dispatcher) => new MediaController(rtspUrl, dispatcher), role)
    {
    }

    internal LiveViewViewModel(Dispatcher uiDispatcher, Func<string, Dispatcher, IMediaController> controllerFactory, Role role = Role.Admin)
    {
        _uiDispatcher = uiDispatcher;
        _controllerFactory = controllerFactory;

        PauseCommand = new RelayCommand(() => _ = PauseAsync(), () => State == MediaControllerState.Connected);
        ResumeCommand = new RelayCommand(() => _ = ResumeAsync(), () => State == MediaControllerState.Paused);
        StopCommand = new RelayCommand(() => _ = StopAsync(), () => HasCamera);
        // Epic-018 Decision 1 (§8.1): Recording is Admin-only. Operator keeps the rest of Live
        // View (§2.5/§4.5) -- only these two commands are role-gated.
        StartRecordingCommand = new RelayCommand(() => _ = StartRecordingAsync(), () => State == MediaControllerState.Connected && !IsRecording && role == Role.Admin);
        StopRecordingCommand = new RelayCommand(() => _ = StopRecordingAsync(), () => IsRecording && role == Role.Admin);
    }

    public void LoadCamera(Camera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        DetachController();

        Camera = camera;
        HasCamera = true;

        if (string.IsNullOrWhiteSpace(camera.RtspUrl))
        {
            StatusMessage = $"{camera.Name} has no stream URL configured. Set it in Camera Detail first.";
            RaiseAllChanged();
            return;
        }

        StatusMessage = $"Connecting to {camera.Name}...";

        var controller = _controllerFactory(camera.RtspUrl, _uiDispatcher);
        controller.StateChanged += HandleControllerStateChanged;
        _controller = controller;

        RaiseAllChanged();
        _ = controller.StartAsync(CancellationToken.None);
    }

    private async Task PauseAsync()
    {
        if (_controller is null)
        {
            return;
        }

        try
        {
            await _controller.PauseAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // Ignore: state changed concurrently (e.g. reconnecting) before the command executed.
        }
    }

    private async Task ResumeAsync()
    {
        if (_controller is null)
        {
            return;
        }

        try
        {
            await _controller.ResumeAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // Ignore: state changed concurrently before the command executed.
        }
    }

    private async Task StopAsync()
    {
        if (_controller is null)
        {
            return;
        }

        await _controller.StopAsync().ConfigureAwait(false);
        StatusMessage = Camera is not null ? $"{Camera.Name} stopped." : "Stopped.";
    }

    private async Task StartRecordingAsync()
    {
        if (_controller is null)
        {
            return;
        }

        try
        {
            await _controller.StartRecordingAsync().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // Ignore: state changed concurrently (e.g. disconnected) before the command executed.
        }

        _ = _uiDispatcher.BeginInvoke(new Action(RaiseAllChanged));
    }

    private async Task StopRecordingAsync()
    {
        if (_controller is null)
        {
            return;
        }

        await _controller.StopRecordingAsync().ConfigureAwait(false);
        _ = _uiDispatcher.BeginInvoke(new Action(RaiseAllChanged));
    }

    private void DetachController()
    {
        if (_controller is null)
        {
            return;
        }

        _controller.StateChanged -= HandleControllerStateChanged;
        _controller.Dispose();
        _controller = null;
    }

    private void HandleControllerStateChanged(object? sender, MediaControllerStateChangedEventArgs e)
    {
        _uiDispatcher.BeginInvoke(new Action(() =>
        {
            StatusMessage = BuildStatusMessage(e);
            RaiseAllChanged();
        }));
    }

    private string BuildStatusMessage(MediaControllerStateChangedEventArgs e)
    {
        var cameraName = Camera?.Name ?? "camera";
        return e.State switch
        {
            MediaControllerState.Connecting => $"Connecting to {cameraName}...",
            MediaControllerState.Connected => $"Live: {cameraName}",
            MediaControllerState.Reconnecting => $"Connection to {cameraName} lost. Reconnecting...",
            MediaControllerState.Paused => $"{cameraName} paused.",
            MediaControllerState.Disconnected => $"{cameraName} disconnected.",
            MediaControllerState.Error => e.Error is not null
                ? $"{cameraName}: {e.Error.Message}"
                : $"{cameraName}: connection failed.",
            _ => StatusMessage
        };
    }

    private void RaiseAllChanged()
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(CurrentFrameSource));
        OnPropertyChanged(nameof(DispatcherMetrics));
        OnPropertyChanged(nameof(IsRecording));
        (PauseCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ResumeCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (StopCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (StartRecordingCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (StopRecordingCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        DetachController();
    }
}
