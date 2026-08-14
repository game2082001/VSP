using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VSP.Core.Commands;
using VSP.Core.Logging;
using VSP.Core.MVVM;
using VSP.Device.Drivers.ONVIF;
using VSP.Domain;
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
    private readonly Func<string, Dispatcher, Guid, IMediaController> _controllerFactory;
    private readonly IOnvifStreamUriResolver _onvifStreamUriResolver;
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

    public ICommand PlayCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand ResumeCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand StartRecordingCommand { get; }
    public ICommand StopRecordingCommand { get; }

    // Role defaults to Admin -- preserves every pre-Epic-018 call site/test's existing
    // full-featured behavior unchanged; MainWindowViewModel (the one real production call site)
    // always passes the authenticated user's actual role explicitly.
    public LiveViewViewModel(Dispatcher uiDispatcher, Role role = Role.Admin)
        : this(uiDispatcher, static (rtspUrl, dispatcher, cameraId) => new MediaController(rtspUrl, dispatcher, cameraId: cameraId), role)
    {
    }

    internal LiveViewViewModel(
        Dispatcher uiDispatcher,
        Func<string, Dispatcher, Guid, IMediaController> controllerFactory,
        Role role = Role.Admin,
        IOnvifStreamUriResolver? onvifStreamUriResolver = null)
    {
        _uiDispatcher = uiDispatcher;
        _controllerFactory = controllerFactory;
        _onvifStreamUriResolver = onvifStreamUriResolver ?? new OnvifStreamUriResolver();

        // Item F (Stop/Restart control-state defect): Play/Start Live is only ever offered once
        // there is a controller to (re)start from -- i.e. Disconnected (after Stop) or Error --
        // and is never a substitute for Resume, which stays exclusive to Paused.
        PlayCommand = new RelayCommand(Play, () => State is MediaControllerState.Disconnected or MediaControllerState.Error);
        PauseCommand = new RelayCommand(() => _ = PauseAsync(), () => State == MediaControllerState.Connected);
        ResumeCommand = new RelayCommand(() => _ = ResumeAsync(), () => State == MediaControllerState.Paused);
        StopCommand = new RelayCommand(() => _ = StopAsync(), () => State is MediaControllerState.Connected or MediaControllerState.Paused);
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

        // ConnectionType handled explicitly: ONVIF resolves its stream URI dynamically via
        // the Media service and must never require (or fall back to) a manually-configured
        // RtspUrl -- everything else keeps the existing RtspUrl/RtspPort path unchanged.
        if (camera.ConnectionType == DeviceConnectionType.ONVIF)
        {
            LoadOnvifCamera(camera);
        }
        else
        {
            LoadRtspUrlCamera(camera);
        }
    }

    private void LoadRtspUrlCamera(Camera camera)
    {
        if (string.IsNullOrWhiteSpace(camera.RtspUrl))
        {
            StatusMessage = $"{camera.Name} has no stream URL configured. Set it in Camera Detail first.";
            RaiseAllChanged();
            return;
        }

        if (!RtspEndpointResolver.TryResolve(camera.RtspUrl, camera.RtspPort, out var effectiveUri))
        {
            // Unparseable RtspUrl: no Uri to attach credentials to. Same fallback the pre-
            // credential-fix code already had for this edge case -- not a new gap.
            StartControllerWithUrl(camera, camera.RtspUrl);
            return;
        }

        StartAuthenticatedController(camera, effectiveUri);
    }

    private void LoadOnvifCamera(Camera camera)
    {
        StatusMessage = $"Resolving ONVIF stream for {camera.Name}...";

        var resolution = _onvifStreamUriResolver.Resolve(camera);

        // Deliberately no fallback to camera.RtspUrl on failure -- a broken ONVIF resolution
        // must stay visible as an ONVIF failure, not be silently masked by a manually-entered
        // URL that happens to be present.
        if (!resolution.IsSuccess || string.IsNullOrWhiteSpace(resolution.StreamUri))
        {
            StatusMessage = $"{camera.Name}: ONVIF stream resolution failed ({DescribeFailure(resolution.FailureReason)}). {resolution.FailureMessage}";
            RaiseAllChanged();
            return;
        }

        if (!Uri.TryCreate(resolution.StreamUri, UriKind.Absolute, out var streamUri))
        {
            StartControllerWithUrl(camera, resolution.StreamUri);
            return;
        }

        StartAuthenticatedController(camera, streamUri);
    }

    /// <summary>
    /// The one call site both ONVIF and RTSP funnel through -- guarantees identical
    /// authentication behavior for both (the shared Live View credential defect fix).
    /// Credentials are attached only to this runtime URI, handed straight to the controller
    /// factory; camera.RtspUrl/the resolved ONVIF StreamUri are never touched or persisted.
    /// </summary>
    private void StartAuthenticatedController(Camera camera, Uri effectiveUri)
    {
        var authenticatedUri = RtspCredentialUriBuilder.Build(effectiveUri, camera.Username, camera.Password);
        LogCredentialBoundaryDiagnostics(camera, effectiveUri, authenticatedUri);
        StartControllerWithUrl(camera, authenticatedUri.AbsoluteUri);
    }

    /// <summary>
    /// Task-AI00B Phase 7C: one sanitized diagnostic line per call, proving whether credential
    /// injection changed only the URI's userinfo component. Never logs either <see cref="Uri"/>,
    /// their UserInfo, or any path/query value -- only booleans/enum names derived from them.
    /// </summary>
    private static void LogCredentialBoundaryDiagnostics(Camera camera, Uri preInjectionUri, Uri runtimeUri)
    {
        AppLog.Info(
            "Live View credential boundary: " +
            $"cameraConnectionType={camera.ConnectionType}, " +
            $"usernamePresent={!string.IsNullOrWhiteSpace(camera.Username)}, " +
            $"passwordPresent={!string.IsNullOrWhiteSpace(camera.Password)}, " +
            $"preUriHasUserInfo={!string.IsNullOrEmpty(preInjectionUri.UserInfo)}, " +
            $"runtimeUriHasUserInfo={!string.IsNullOrEmpty(runtimeUri.UserInfo)}, " +
            $"runtimeUriSchemeMatchesPreUri={runtimeUri.Scheme == preInjectionUri.Scheme}, " +
            $"runtimeUriHostMatchesPreUri={runtimeUri.Host == preInjectionUri.Host}, " +
            $"runtimeUriPortMatchesPreUri={runtimeUri.Port == preInjectionUri.Port}, " +
            $"runtimeUriPathMatchesPreUri={runtimeUri.AbsolutePath == preInjectionUri.AbsolutePath}, " +
            $"runtimeUriQueryMatchesPreUri={runtimeUri.Query == preInjectionUri.Query}.");
    }

    private void StartControllerWithUrl(Camera camera, string effectiveRtspUrl)
    {
        StatusMessage = $"Connecting to {camera.Name}...";

        // RC1-R04: camera.Id (the Camera instance already in scope here, not a global/current
        // property) is threaded straight into the factory so recordings for this camera land
        // under RecordingPathProvider's per-camera directory -- the same one RecordingCatalog
        // already scans for Playback.
        var controller = _controllerFactory(effectiveRtspUrl, _uiDispatcher, camera.Id);
        controller.StateChanged += HandleControllerStateChanged;
        controller.Renderer.FrameRendered += HandleFrameRendered;
        _controller = controller;

        RaiseAllChanged();
        _ = controller.StartAsync(CancellationToken.None);
    }

    private static string DescribeFailure(OnvifStreamUriFailureReason? reason)
    {
        return reason switch
        {
            OnvifStreamUriFailureReason.DeviceServiceUnavailable => "Device Service unreachable",
            OnvifStreamUriFailureReason.MediaServiceUnavailable => "Media Service unavailable",
            OnvifStreamUriFailureReason.AuthenticationFailed => "authentication failed",
            OnvifStreamUriFailureReason.NoMediaProfiles => "no media profiles",
            OnvifStreamUriFailureReason.GetStreamUriFailed => "GetStreamUri failed",
            OnvifStreamUriFailureReason.InvalidStreamUri => "invalid stream URI",
            _ => "unknown error"
        };
    }

    /// <summary>
    /// Item F: restarts Live View for the currently-loaded camera -- from Stop (Disconnected) or
    /// from Error alike, always via the same <see cref="LoadCamera"/> path so ONVIF stream URI
    /// resolution and credential attachment are redone from scratch, and the previous controller
    /// is detached/disposed (see <see cref="LoadCamera"/>'s leading <see cref="DetachController"/>
    /// call) before a new one is created. Never touches Pause/Resume, which stay exclusive to the
    /// Paused state.
    /// </summary>
    private void Play()
    {
        if (Camera is null)
        {
            return;
        }

        LoadCamera(Camera);
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
        _controller.Renderer.FrameRendered -= HandleFrameRendered;
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

    /// <summary>
    /// Item F fix: <see cref="IFrameRenderer.CurrentFrameSource"/> is updated in place by the
    /// renderer on every frame (no new object identity), so WPF's binding never re-pulls it
    /// unless something explicitly raises PropertyChanged -- this was previously never wired,
    /// so the Image control stayed bound to the null it read at Connected (before the first
    /// frame existed) forever, even while frames kept rendering successfully underneath it.
    /// Subscribed/unsubscribed alongside StateChanged in StartControllerWithUrl/DetachController,
    /// so a late event from an already-detached controller's renderer can never reach here.
    /// </summary>
    private void HandleFrameRendered(object? sender, EventArgs e)
    {
        _uiDispatcher.BeginInvoke(new Action(() => OnPropertyChanged(nameof(CurrentFrameSource))));
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
        (PlayCommand as RelayCommand)?.RaiseCanExecuteChanged();
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
