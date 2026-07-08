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
        set => SetProperty(ref _selectedBrand, value);
    }

    private string _selectedStatus = "All";
    public string SelectedStatus
    {
        get => _selectedStatus;
        set => SetProperty(ref _selectedStatus, value);
    }

    private bool _isLoaded;

    public ObservableCollection<CameraListItemViewModel> Cameras { get; } = new();
    public IReadOnlyList<string> BrandOptions { get; } = new[] { "All" };
    public IReadOnlyList<string> StatusOptions { get; } = new[] { "All" };
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

            ApplySearch();
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
        IEnumerable<CameraListItemViewModel> filteredCameras = _allCameras;
        var keyword = SearchKeyword.Trim();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filteredCameras = _allCameras.Where(camera =>
                ContainsKeyword(camera.Name, keyword) ||
                ContainsKeyword(camera.IpAddress, keyword));
        }

        Cameras.Clear();

        foreach (var camera in filteredCameras)
        {
            Cameras.Add(camera);
        }

        OnPropertyChanged(nameof(TotalCamerasText));
        StatusMessage = GetStatusMessage(keyword);
    }

    private void ClearSearch()
    {
        SearchKeyword = string.Empty;
        ApplySearch();
    }

    private string GetStatusMessage(string keyword)
    {
        if (_allCameras.Count == 0)
        {
            return "No cameras found.";
        }

        if (string.IsNullOrWhiteSpace(keyword))
        {
            return $"Loaded {Cameras.Count} camera(s).";
        }

        return Cameras.Count == 0
            ? "No cameras matched the search keyword."
            : $"Found {Cameras.Count} camera(s).";
    }

    private static bool ContainsKeyword(string value, string keyword)
    {
        return value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static void PlaceholderAction()
    {
    }
}
