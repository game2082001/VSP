using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using VSP.Core.Commands;
using VSP.Core.MVVM;
using VSP.Device.Services;
using VSP.Domain.Entities;
using VSP.Player.Control;
using VSP.Player.Entities;
using VSP.Player.Interfaces;
using VSP.Player.Recording;

namespace VSP.UI.ViewModels;

/// <summary>Wraps a recording file for display -- not a database row (RecordingCatalog is a filesystem listing).</summary>
public sealed class RecordingItem
{
    public RecordingItem(RecordingFileInfo file)
    {
        FilePath = file.FilePath;
        RecordedAt = file.RecordedAt;
    }

    public string FilePath { get; }

    public DateTime RecordedAt { get; }

    public string DisplayName => RecordedAt.ToString("yyyy-MM-dd HH:mm:ss");
}

/// <summary>
/// Playback Foundation (Epic-012): camera selection, recording list, Play/Pause/Stop/Seek only --
/// no timeline, search, bookmark, snapshot, export, or variable playback rate, per the approved
/// scope. Mirrors LiveViewViewModel's shape (hosts one IMediaController at a time, camera-driven).
/// </summary>
public class PlaybackViewModel : ObservableObject, IDisposable
{
    private readonly Dispatcher _uiDispatcher;
    private readonly CameraQueryService _cameraQueryService;
    private readonly Func<string, Dispatcher, IMediaController> _controllerFactory;
    private readonly DispatcherTimer _positionTimer;
    private IMediaController? _controller;

    public ObservableCollection<Camera> Cameras { get; } = new();

    public ObservableCollection<RecordingItem> Recordings { get; } = new();

    private Camera? _selectedCamera;
    public Camera? SelectedCamera
    {
        get => _selectedCamera;
        set
        {
            if (SetProperty(ref _selectedCamera, value))
            {
                LoadRecordingsForSelectedCamera();
            }
        }
    }

    private RecordingItem? _selectedRecording;
    public RecordingItem? SelectedRecording
    {
        get => _selectedRecording;
        set
        {
            if (SetProperty(ref _selectedRecording, value))
            {
                (PlayCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    private string _statusMessage = "Select a camera to see its recordings.";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ImageSource? CurrentFrameSource => _controller?.Renderer.CurrentFrameSource;

    public MediaControllerState State => _controller?.State ?? MediaControllerState.Idle;

    public TimeSpan CurrentPosition => _controller?.Clock.CurrentPresentationTime ?? TimeSpan.Zero;

    public TimeSpan? Duration => _controller?.Duration;

    public double SeekMaximumSeconds => Duration.HasValue && Duration.Value > TimeSpan.Zero ? Duration.Value.TotalSeconds : 1;

    public double CurrentPositionSeconds => CurrentPosition.TotalSeconds;

    public string PositionText => $"{FormatTime(CurrentPosition)} / {FormatTime(Duration)}";

    public bool HasActiveSession => _controller is not null;

    public ICommand PlayCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand StopCommand { get; }

    public PlaybackViewModel(CameraQueryService cameraQueryService, Dispatcher uiDispatcher)
        : this(cameraQueryService, uiDispatcher, static (filePath, dispatcher) => new PlaybackController(filePath, dispatcher))
    {
    }

    internal PlaybackViewModel(
        CameraQueryService cameraQueryService,
        Dispatcher uiDispatcher,
        Func<string, Dispatcher, IMediaController> controllerFactory)
    {
        _cameraQueryService = cameraQueryService ?? throw new ArgumentNullException(nameof(cameraQueryService));
        _uiDispatcher = uiDispatcher;
        _controllerFactory = controllerFactory;

        _positionTimer = new DispatcherTimer(DispatcherPriority.Background, uiDispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _positionTimer.Tick += (_, _) =>
        {
            OnPropertyChanged(nameof(CurrentPosition));
            OnPropertyChanged(nameof(CurrentPositionSeconds));
            OnPropertyChanged(nameof(PositionText));
        };

        PlayCommand = new RelayCommand(() => _ = PlayAsync(), () => CanPlay());
        PauseCommand = new RelayCommand(() => _ = PauseAsync(), () => State == MediaControllerState.Connected);
        StopCommand = new RelayCommand(() => _ = StopAsync(), () => HasActiveSession);
    }

    public async Task LoadCamerasAsync()
    {
        Cameras.Clear();
        foreach (var camera in await _cameraQueryService.GetAllAsync())
        {
            Cameras.Add(camera);
        }
    }

    /// <summary>Seeks the active session. A no-op (returns quietly) if nothing is playing yet.</summary>
    public void Seek(TimeSpan target)
    {
        var controller = _controller;
        if (controller is null)
        {
            return;
        }

        _ = Task.Run(() =>
        {
            controller.Clock.Seek(target);
            _uiDispatcher.BeginInvoke(new Action(RaiseAllChanged));
        });
    }

    private bool CanPlay()
    {
        if (State == MediaControllerState.Paused)
        {
            return true;
        }

        return SelectedRecording is not null && State is MediaControllerState.Idle or MediaControllerState.Disconnected or MediaControllerState.Error;
    }

    private async Task PlayAsync()
    {
        if (_controller is not null && State == MediaControllerState.Paused)
        {
            try
            {
                await _controller.ResumeAsync().ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                // Ignore: state changed concurrently before the command executed.
            }

            _ = _uiDispatcher.BeginInvoke(new Action(RaiseAllChanged));
            return;
        }

        if (SelectedRecording is null)
        {
            return;
        }

        DetachController();

        StatusMessage = $"Opening {SelectedRecording.DisplayName}...";

        var controller = _controllerFactory(SelectedRecording.FilePath, _uiDispatcher);
        controller.StateChanged += HandleControllerStateChanged;
        _controller = controller;

        RaiseAllChanged();
        await controller.StartAsync(CancellationToken.None).ConfigureAwait(false);
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
            // Ignore: state changed concurrently before the command executed.
        }

        _ = _uiDispatcher.BeginInvoke(new Action(RaiseAllChanged));
    }

    private async Task StopAsync()
    {
        if (_controller is null)
        {
            return;
        }

        await _controller.StopAsync().ConfigureAwait(false);
        StatusMessage = "Stopped.";
        _ = _uiDispatcher.BeginInvoke(new Action(RaiseAllChanged));
    }

    private void LoadRecordingsForSelectedCamera()
    {
        DetachController();
        Recordings.Clear();
        SelectedRecording = null;

        if (SelectedCamera is null)
        {
            StatusMessage = "Select a camera to see its recordings.";
            return;
        }

        var files = RecordingCatalog.ListRecordings(SelectedCamera.Id);
        foreach (var file in files)
        {
            Recordings.Add(new RecordingItem(file));
        }

        StatusMessage = files.Count > 0
            ? $"{files.Count} recording(s) for {SelectedCamera.Name}."
            : $"No recordings found for {SelectedCamera.Name}.";
    }

    private void DetachController()
    {
        _positionTimer.Stop();

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

            if (e.State == MediaControllerState.Connected)
            {
                _positionTimer.Start();
            }
            else
            {
                _positionTimer.Stop();
            }
        }));
    }

    private string BuildStatusMessage(MediaControllerStateChangedEventArgs e)
    {
        var name = SelectedRecording?.DisplayName ?? "recording";
        return e.State switch
        {
            MediaControllerState.Connecting => $"Opening {name}...",
            MediaControllerState.Connected => $"Playing {name}",
            MediaControllerState.Paused => $"{name} paused.",
            MediaControllerState.Disconnected => "Stopped.",
            MediaControllerState.Error => e.Error is not null
                ? $"{name}: {e.Error.Message}"
                : $"{name}: playback failed.",
            _ => StatusMessage
        };
    }

    private static string FormatTime(TimeSpan? time)
    {
        if (!time.HasValue)
        {
            return "--:--";
        }

        return time.Value.TotalHours >= 1
            ? time.Value.ToString(@"h\:mm\:ss")
            : time.Value.ToString(@"mm\:ss");
    }

    private void RaiseAllChanged()
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(CurrentFrameSource));
        OnPropertyChanged(nameof(CurrentPosition));
        OnPropertyChanged(nameof(CurrentPositionSeconds));
        OnPropertyChanged(nameof(Duration));
        OnPropertyChanged(nameof(SeekMaximumSeconds));
        OnPropertyChanged(nameof(PositionText));
        OnPropertyChanged(nameof(HasActiveSession));
        (PlayCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (PauseCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (StopCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void Dispose()
    {
        _positionTimer.Stop();
        DetachController();
    }
}
