using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.MVVM;
using VSP.Device.Drivers;
using VSP.Device.Services;
using VSP.Domain.Entities;

namespace VSP.UI.ViewModels;

public class DashboardViewModel : ObservableObject
{
    private readonly CameraQueryService _cameraQueryService;
    private readonly DriverRegistry _driverRegistry;
    private bool _isLoaded;

    private CameraDashboardSummary _summary =
        CameraDashboardSummaryBuilder.Build(Array.Empty<Camera>(), Array.Empty<DriverDescriptor>());
    public CameraDashboardSummary Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    private bool _hasError;
    public bool HasError
    {
        get => _hasError;
        private set => SetProperty(ref _hasError, value);
    }

    private string _statusMessage = "Loading dashboard...";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private string _lastRefreshedText = "Never";
    public string LastRefreshedText
    {
        get => _lastRefreshedText;
        private set => SetProperty(ref _lastRefreshedText, value);
    }

    public ICommand RefreshCommand { get; }

    public DashboardViewModel(CameraQueryService cameraQueryService, DriverRegistry driverRegistry)
    {
        _cameraQueryService = cameraQueryService ?? throw new ArgumentNullException(nameof(cameraQueryService));
        _driverRegistry = driverRegistry ?? throw new ArgumentNullException(nameof(driverRegistry));

        RefreshCommand = new RelayCommand(() => _ = RefreshAsync(), () => !IsLoading);
    }

    public async Task LoadAsync()
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        IsLoading = true;
        HasError = false;
        StatusMessage = "Refreshing...";

        try
        {
            var cameras = await _cameraQueryService.GetAllAsync();
            Summary = CameraDashboardSummaryBuilder.Build(cameras, _driverRegistry.GetAll());
            LastRefreshedText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            StatusMessage = $"Loaded {cameras.Count} camera(s).";
        }
        catch (Exception ex)
        {
            HasError = true;
            StatusMessage = $"Failed to load dashboard data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
