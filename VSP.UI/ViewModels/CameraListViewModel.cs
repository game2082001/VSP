using System.Collections.ObjectModel;
using VSP.Core.MVVM;
using VSP.Device.Services;

namespace VSP.UI.ViewModels;

public class CameraListViewModel : ObservableObject
{
    private readonly CameraQueryService _cameraQueryService;

    private string _title = "Camera List";
    public string Title
    {
        get => _title;
        private set => SetProperty(ref _title, value);
    }

    private string _statusMessage = "Ready.";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private bool _isLoaded;

    public ObservableCollection<CameraListItemViewModel> Cameras { get; } = new();

    public CameraListViewModel(CameraQueryService cameraQueryService)
    {
        _cameraQueryService = cameraQueryService;
    }

    public async Task LoadAsync()
    {
        if (_isLoaded)
        {
            return;
        }

        try
        {
            StatusMessage = "Loading cameras...";
            var cameras = await _cameraQueryService.GetAllAsync();

            Cameras.Clear();

            foreach (var camera in cameras)
            {
                Cameras.Add(CameraListItemViewModel.FromCamera(camera));
            }

            StatusMessage = cameras.Count == 0
                ? "No cameras found."
                : $"Loaded {cameras.Count} camera(s).";
            _isLoaded = true;
        }
        catch (Exception ex)
        {
            Cameras.Clear();
            StatusMessage = $"Failed to load cameras: {ex.Message}";
        }
    }
}
