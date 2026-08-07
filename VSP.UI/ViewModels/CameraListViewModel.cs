using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.MVVM;
using VSP.Device.Services;
using VSP.Domain.Entities;
using VSP.Domain.Enums;
using VSP.UI.Services;

namespace VSP.UI.ViewModels;

public class CameraListViewModel : ObservableObject
{
    private readonly CameraQueryService _cameraQueryService;
    private readonly LiveViewCameraCoordinator? _liveViewCoordinator;
    private readonly Role _role;
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
        set
        {
            if (SetProperty(ref _selectedCamera, value))
            {
                (ViewLiveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }

    private bool _isLoaded;

    public Role Role => _role;

    public ObservableCollection<CameraListItemViewModel> Cameras { get; } = new();
    public IReadOnlyList<string> BrandOptions { get; } = new[] { "All", "Hikvision", "Dahua", "VIVOTEK" };
    public IReadOnlyList<string> StatusOptions { get; } = new[] { "All", "Online", "Offline" };
    public string TotalCamerasText => $"Total: {Cameras.Count} cameras";

    public int SelectedItemCount => Cameras.Count(camera => camera.IsSelected);

    private bool _isShowingDiscovery;
    public bool IsShowingDiscovery
    {
        get => _isShowingDiscovery;
        private set
        {
            if (SetProperty(ref _isShowingDiscovery, value))
            {
                OnPropertyChanged(nameof(IsShowingCameraList));
            }
        }
    }

    public bool IsShowingCameraList => !IsShowingDiscovery;

    public ICommand SearchCommand { get; }
    public ICommand ClearCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand AddCameraCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand BatchEditCommand { get; }
    public ICommand BatchConnectionTestCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ShowDiscoveryCommand { get; }
    public ICommand ShowCameraListCommand { get; }
    public ICommand ViewLiveCommand { get; }

    public event Action? RequestAddCamera;
    public event Action? RequestImport;
    public event Action<IReadOnlyList<Camera>>? RequestBatchEdit;
    public event Action<IReadOnlyList<Camera>>? RequestBatchConnectionTest;
    public event Action<IReadOnlyList<Camera>>? RequestExport;

    // Role defaults to Admin -- preserves every pre-Epic-018 call site/test's existing
    // full-featured behavior unchanged; the two real production call sites (CameraListView.xaml.cs)
    // always pass the authenticated user's actual role explicitly.
    public CameraListViewModel(CameraQueryService cameraQueryService, LiveViewCameraCoordinator? liveViewCoordinator = null, Role role = Role.Admin)
    {
        _cameraQueryService = cameraQueryService;
        _liveViewCoordinator = liveViewCoordinator;
        _role = role;
        SearchCommand = new RelayCommand(ApplySearch);
        ClearCommand = new RelayCommand(ClearSearch);
        RefreshCommand = new RelayCommand(() => _ = RefreshAsync());
        AddCameraCommand = new RelayCommand(RaiseAddCameraRequest, () => _role == Role.Admin);
        ImportCommand = new RelayCommand(RaiseImportRequest, () => _role == Role.Admin);
        BatchEditCommand = new RelayCommand(RaiseBatchEditRequest, () => SelectedItemCount >= 2 && _role == Role.Admin);
        BatchConnectionTestCommand = new RelayCommand(RaiseBatchConnectionTestRequest, () => SelectedItemCount >= 1 && _role == Role.Admin);
        ExportCommand = new RelayCommand(RaiseExportRequest, () => Cameras.Count > 0 && _role == Role.Admin);
        ShowDiscoveryCommand = new RelayCommand(() => IsShowingDiscovery = true, () => _role == Role.Admin);
        ShowCameraListCommand = new RelayCommand(() => IsShowingDiscovery = false);
        ViewLiveCommand = new RelayCommand(RaiseViewLiveRequest, () => SelectedCamera is not null);
    }

    public async Task LoadAsync()
    {
        if (_isLoaded)
        {
            return;
        }

        await ReloadAsync(isRefresh: false, selectedCameraId: null);
    }

    public async Task RefreshAsync(Guid? selectedCameraId = null)
    {
        var rememberedSelectedCameraId = selectedCameraId ?? SelectedCamera?.SourceCamera.Id;
        await ReloadAsync(isRefresh: true, rememberedSelectedCameraId);
    }

    private async Task ReloadAsync(bool isRefresh, Guid? selectedCameraId)
    {
        try
        {
            StatusMessage = isRefresh ? "Refreshing camera list..." : "Loading cameras...";
            var cameras = await _cameraQueryService.GetAllAsync();
            var rebuiltCameras = cameras
                .Select(CameraListItemViewModel.FromCamera)
                .ToList();

            foreach (var oldCamera in _allCameras)
            {
                oldCamera.PropertyChanged -= HandleCameraItemPropertyChanged;
            }

            _allCameras.Clear();
            foreach (var camera in rebuiltCameras)
            {
                camera.PropertyChanged += HandleCameraItemPropertyChanged;
                _allCameras.Add(camera);
            }

            ApplyFilters(updateStatusMessage: false);
            RestoreSelectionAfterFilters(selectedCameraId);
            _isLoaded = true;
            StatusMessage = isRefresh
                ? "Camera list refreshed."
                : GetStatusMessage(SearchKeyword.Trim());
        }
        catch (Exception ex)
        {
            if (!isRefresh)
            {
                _allCameras.Clear();
                Cameras.Clear();
                OnPropertyChanged(nameof(TotalCamerasText));
                StatusMessage = $"Failed to load cameras: {ex.Message}";
                return;
            }

            StatusMessage = "Failed to refresh camera list.";
        }
    }

    private void ApplySearch()
    {
        ApplyFilters();
    }

    private void ApplyFilters(bool updateStatusMessage = true)
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
        OnPropertyChanged(nameof(SelectedItemCount));
        (BatchEditCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (BatchConnectionTestCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportCommand as RelayCommand)?.RaiseCanExecuteChanged();

        if (updateStatusMessage)
        {
            StatusMessage = GetStatusMessage(keyword);
        }
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

    private void RestoreSelectionAfterFilters(Guid? selectedCameraId)
    {
        if (selectedCameraId is null)
        {
            SelectedCamera = null;
            return;
        }

        SelectedCamera = Cameras.FirstOrDefault(camera => camera.SourceCamera.Id == selectedCameraId.Value);
    }

    private bool HasActiveFilters()
    {
        return !string.Equals(SelectedBrand, "All", StringComparison.OrdinalIgnoreCase) ||
               !string.Equals(SelectedStatus, "All", StringComparison.OrdinalIgnoreCase);
    }

    private void RaiseViewLiveRequest()
    {
        var camera = SelectedCamera?.SourceCamera;
        if (camera is null)
        {
            return;
        }

        _liveViewCoordinator?.RequestLiveView(camera);
    }

    private void RaiseAddCameraRequest()
    {
        RequestAddCamera?.Invoke();
    }

    private void RaiseImportRequest()
    {
        RequestImport?.Invoke();
    }

    private void RaiseBatchEditRequest()
    {
        var selected = Cameras
            .Where(camera => camera.IsSelected)
            .Select(camera => camera.SourceCamera)
            .ToList();

        if (selected.Count < 2)
        {
            return;
        }

        RequestBatchEdit?.Invoke(selected);
    }

    private void RaiseBatchConnectionTestRequest()
    {
        var selected = Cameras
            .Where(camera => camera.IsSelected)
            .Select(camera => camera.SourceCamera)
            .ToList();

        if (selected.Count < 1)
        {
            return;
        }

        RequestBatchConnectionTest?.Invoke(selected);
    }

    private void RaiseExportRequest()
    {
        var visible = Cameras
            .Select(camera => camera.SourceCamera)
            .ToList();

        if (visible.Count == 0)
        {
            return;
        }

        RequestExport?.Invoke(visible);
    }

    private void HandleCameraItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CameraListItemViewModel.IsSelected))
        {
            return;
        }

        OnPropertyChanged(nameof(SelectedItemCount));
        (BatchEditCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (BatchConnectionTestCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
