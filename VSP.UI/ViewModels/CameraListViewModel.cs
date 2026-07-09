using System.Collections.ObjectModel;
using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.MVVM;
using VSP.Device.Services;

namespace VSP.UI.ViewModels;

public class CameraListViewModel : ObservableObject
{
    private readonly CameraQueryService _cameraQueryService;
    private readonly List<CameraListItemViewModel> _allCameras = new();

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

    private string _searchKeyword = string.Empty;
    public string SearchKeyword
    {
        get => _searchKeyword;
        set => SetProperty(ref _searchKeyword, value);
    }

    private string _selectedBrand = "All";
    public string SelectedBrand
    {
        get => _selectedBrand;
        set
        {
            if (SetProperty(ref _selectedBrand, value) && _isLoaded)
            {
                ApplyFilters();
            }
        }
    }

    private string _selectedStatus = "All";
    public string SelectedStatus
    {
        get => _selectedStatus;
        set
        {
            if (SetProperty(ref _selectedStatus, value) && _isLoaded)
            {
                ApplyFilters();
            }
        }
    }

    private CameraListItemViewModel? _selectedCamera;
    public CameraListItemViewModel? SelectedCamera
    {
        get => _selectedCamera;
        set => SetProperty(ref _selectedCamera, value);
    }

    private bool _isLoaded;

    public ObservableCollection<CameraListItemViewModel> Cameras { get; } = new();
    public IReadOnlyList<string> BrandOptions { get; } = new[] { "All", "Hikvision", "Dahua", "VIVOTEK" };
    public IReadOnlyList<string> StatusOptions { get; } = new[] { "All", "Online", "Offline" };
    public string TotalCamerasText => $"Total: {Cameras.Count} cameras";

    public ICommand SearchCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand AddCameraCommand { get; }

    public CameraListViewModel(CameraQueryService cameraQueryService)
    {
        _cameraQueryService = cameraQueryService;
        SearchCommand = new RelayCommand(ApplySearch);
        ClearCommand = new RelayCommand(ClearSearch);
        RefreshCommand = new RelayCommand(PlaceholderAction);
        AddCameraCommand = new RelayCommand(PlaceholderAction);
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

            _allCameras.Clear();

            foreach (var camera in cameras)
            {
                _allCameras.Add(CameraListItemViewModel.FromCamera(camera));
            }

            ApplyFilters();
            _isLoaded = true;
        }
        catch (Exception ex)
        {
            _allCameras.Clear();
            Cameras.Clear();
            OnPropertyChanged(nameof(TotalCamerasText));
            StatusMessage = $"Failed to load cameras: {ex.Message}";
        }
    }

    private void ApplySearch()
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        IEnumerable<CameraListItemViewModel> filteredCameras = _allCameras;
        var keyword = SearchKeyword.Trim();

        if (!string.Equals(SelectedBrand, "All", StringComparison.OrdinalIgnoreCase))
        {
            filteredCameras = filteredCameras.Where(camera =>
                string.Equals(camera.Brand, SelectedBrand, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedStatus, "All", StringComparison.OrdinalIgnoreCase))
        {
            filteredCameras = filteredCameras.Where(camera =>
                string.Equals(camera.Status, SelectedStatus, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filteredCameras = filteredCameras.Where(camera =>
                ContainsKeyword(camera.Name, keyword) ||
                ContainsKeyword(camera.IpAddress, keyword));
        }

        Cameras.Clear();

        foreach (var camera in filteredCameras)
        {
            Cameras.Add(camera);
        }

        if (SelectedCamera is not null && !Cameras.Contains(SelectedCamera))
        {
            SelectedCamera = null;
        }

        OnPropertyChanged(nameof(TotalCamerasText));
        StatusMessage = GetStatusMessage(keyword);
    }

    private void ClearSearch()
    {
        SearchKeyword = string.Empty;
        SelectedBrand = "All";
        SelectedStatus = "All";
        ApplyFilters();
    }

    private string GetStatusMessage(string keyword)
    {
        if (_allCameras.Count == 0)
        {
            return "No cameras found.";
        }

        if (string.IsNullOrWhiteSpace(keyword))
        {
            return HasActiveFilters()
                ? $"Filtered to {Cameras.Count} camera(s)."
                : $"Loaded {Cameras.Count} camera(s).";
        }

        return Cameras.Count == 0
            ? "No cameras matched the search keyword."
            : $"Found {Cameras.Count} camera(s).";
    }

    private static bool ContainsKeyword(string value, string keyword)
    {
        return value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private bool HasActiveFilters()
    {
        return !string.Equals(SelectedBrand, "All", StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(SelectedStatus, "All", StringComparison.OrdinalIgnoreCase);
    }

    private static void PlaceholderAction()
    {
    }
}
