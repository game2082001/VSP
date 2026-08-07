using VSP.Device.Interfaces;
using VSP.Device.Services;
using VSP.Domain.Enums;
using VSP.UI.ViewModels;
using EntityCamera = VSP.Domain.Entities.Camera;
using Xunit;

namespace VSP.Tests.Camera;

public class CameraListViewModelTests
{
    [Fact]
    public async Task LoadAsync_HandlesEmptyRepository()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository());

        await viewModel.LoadAsync();

        Assert.Empty(viewModel.Cameras);
        Assert.Equal("No cameras found.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task LoadAsync_MapsMultipleCameras()
    {
        var repository = new FakeCameraRepository(
            CreateCamera("Front Door", "192.168.1.10", CameraBrand.RTSP, CameraStatus.Online, "Gate"),
            CreateCamera("Lobby", "192.168.1.11", CameraBrand.Hikvision, CameraStatus.Offline, "Hall"));
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync();

        Assert.Equal(2, viewModel.Cameras.Count);
        Assert.Equal("Front Door", viewModel.Cameras[0].Name);
        Assert.Equal("192.168.1.10", viewModel.Cameras[0].IpAddress);
        Assert.Equal("RTSP", viewModel.Cameras[0].Brand);
        Assert.Equal("Online", viewModel.Cameras[0].Status);
        Assert.Equal("Gate", viewModel.Cameras[0].Location);
        Assert.Equal("Total: 2 cameras", viewModel.TotalCamerasText);
        Assert.Equal("Loaded 2 camera(s).", viewModel.StatusMessage);
    }

    [Fact]
    public void IsShowingDiscovery_DefaultsToFalse()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository());

        Assert.False(viewModel.IsShowingDiscovery);
        Assert.True(viewModel.IsShowingCameraList);
    }

    [Fact]
    public void ShowDiscoveryCommand_SwitchesToDiscoverySection()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository());

        viewModel.ShowDiscoveryCommand.Execute(null);

        Assert.True(viewModel.IsShowingDiscovery);
        Assert.False(viewModel.IsShowingCameraList);
    }

    [Fact]
    public void ShowCameraListCommand_SwitchesBackToCameraListSection()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository());
        viewModel.ShowDiscoveryCommand.Execute(null);

        viewModel.ShowCameraListCommand.Execute(null);

        Assert.False(viewModel.IsShowingDiscovery);
        Assert.True(viewModel.IsShowingCameraList);
    }

    [Fact]
    public async Task LoadAsync_HandlesRepositoryException()
    {
        var viewModel = CreateViewModel(new ThrowingCameraRepository());

        await viewModel.LoadAsync();

        Assert.Empty(viewModel.Cameras);
        Assert.Equal("Failed to load cameras: Repository failure.", viewModel.StatusMessage);
    }

    [Fact]
    public void CameraListItemViewModel_MapsCameraFields()
    {
        var item = CameraListItemViewModel.FromCamera(
            CreateCamera("Warehouse", "10.0.0.15", CameraBrand.Axis, CameraStatus.Offline, "Storage"));

        Assert.Equal("Warehouse", item.Name);
        Assert.Equal("10.0.0.15", item.IpAddress);
        Assert.Equal("Axis", item.Brand);
        Assert.Equal("Offline", item.Status);
        Assert.Equal("Storage", item.Location);
    }

    [Fact]
    public void Constructor_InitializesToolbarSkeletonState()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository());

        Assert.Equal(string.Empty, viewModel.SearchKeyword);
        Assert.Equal("All", viewModel.SelectedBrand);
        Assert.Equal("All", viewModel.SelectedStatus);
        Assert.Equal(new[] { "All", "Hikvision", "Dahua", "VIVOTEK" }, viewModel.BrandOptions);
        Assert.Equal(new[] { "All", "Online", "Offline" }, viewModel.StatusOptions);
        Assert.Equal("Total: 0 cameras", viewModel.TotalCamerasText);
        Assert.Equal("Ready.", viewModel.StatusMessage);
    }

    [Fact]
    public void ToolbarCommands_ExecuteWithoutThrowing()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository());

        viewModel.SearchCommand.Execute(null);
        viewModel.ClearCommand.Execute(null);
        viewModel.RefreshCommand.Execute(null);
        viewModel.AddCameraCommand.Execute(null);

        Assert.Equal("Camera list refreshed.", viewModel.StatusMessage);
    }

    [Fact]
    public void AddCameraCommand_RaisesRequestAddCamera()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository());
        var wasRaised = false;

        viewModel.RequestAddCamera += () => wasRaised = true;

        viewModel.AddCameraCommand.Execute(null);

        Assert.True(wasRaised);
    }

    [Fact]
    public async Task SearchCommand_FiltersByCameraName()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(
            CreateCamera("Front Door", "192.168.1.10", CameraBrand.RTSP, CameraStatus.Online, "Gate"),
            CreateCamera("Lobby", "192.168.1.11", CameraBrand.Hikvision, CameraStatus.Offline, "Hall")));

        await viewModel.LoadAsync();
        viewModel.SearchKeyword = "front";

        viewModel.SearchCommand.Execute(null);

        Assert.Single(viewModel.Cameras);
        Assert.Equal("Front Door", viewModel.Cameras[0].Name);
        Assert.Equal("Total: 1 cameras", viewModel.TotalCamerasText);
        Assert.Equal("Found 1 camera(s).", viewModel.StatusMessage);
    }

    [Fact]
    public async Task SearchCommand_FiltersByIpAddress()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(
            CreateCamera("Front Door", "192.168.1.10", CameraBrand.RTSP, CameraStatus.Online, "Gate"),
            CreateCamera("Lobby", "10.0.0.25", CameraBrand.Hikvision, CameraStatus.Offline, "Hall")));

        await viewModel.LoadAsync();
        viewModel.SearchKeyword = "10.0.0.25";

        viewModel.SearchCommand.Execute(null);

        Assert.Single(viewModel.Cameras);
        Assert.Equal("Lobby", viewModel.Cameras[0].Name);
    }

    [Fact]
    public async Task SearchCommand_DoesNotSearchExcludedFields()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(
            CreateCamera("Front Door", "192.168.1.10", CameraBrand.RTSP, CameraStatus.Online, "Gate"),
            CreateCamera("Lobby", "10.0.0.25", CameraBrand.Hikvision, CameraStatus.Offline, "Hall")));

        await viewModel.LoadAsync();
        viewModel.SearchKeyword = "hikvision";

        viewModel.SearchCommand.Execute(null);

        Assert.Empty(viewModel.Cameras);
        Assert.Equal("Total: 0 cameras", viewModel.TotalCamerasText);
        Assert.Equal("No cameras matched the search keyword.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task SearchCommand_BlankKeywordRestoresFullList()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(
            CreateCamera("Front Door", "192.168.1.10", CameraBrand.RTSP, CameraStatus.Online, "Gate"),
            CreateCamera("Lobby", "10.0.0.25", CameraBrand.Hikvision, CameraStatus.Offline, "Hall")));

        await viewModel.LoadAsync();
        viewModel.SearchKeyword = "front";
        viewModel.SearchCommand.Execute(null);

        viewModel.SearchKeyword = "   ";
        viewModel.SearchCommand.Execute(null);

        Assert.Equal(2, viewModel.Cameras.Count);
        Assert.Equal("Total: 2 cameras", viewModel.TotalCamerasText);
        Assert.Equal("Loaded 2 camera(s).", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ClearCommand_ClearsKeywordAndRestoresFullList()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(
            CreateCamera("Front Door", "192.168.1.10", CameraBrand.RTSP, CameraStatus.Online, "Gate"),
            CreateCamera("Lobby", "10.0.0.25", CameraBrand.Hikvision, CameraStatus.Offline, "Hall")));

        await viewModel.LoadAsync();
        viewModel.SearchKeyword = "front";
        viewModel.SearchCommand.Execute(null);

        viewModel.ClearCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.SearchKeyword);
        Assert.Equal(2, viewModel.Cameras.Count);
        Assert.Equal("Total: 2 cameras", viewModel.TotalCamerasText);
        Assert.Equal("Loaded 2 camera(s).", viewModel.StatusMessage);
    }

    [Fact]
    public async Task SelectedBrand_FiltersByBrand()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(
            CreateCamera("Front Door", "192.168.1.10", CameraBrand.Hikvision, CameraStatus.Online, "Gate"),
            CreateCamera("Lobby", "10.0.0.25", CameraBrand.Dahua, CameraStatus.Offline, "Hall")));

        await viewModel.LoadAsync();
        viewModel.SelectedBrand = "Hikvision";

        Assert.Single(viewModel.Cameras);
        Assert.Equal("Front Door", viewModel.Cameras[0].Name);
        Assert.Equal("Filtered to 1 camera(s).", viewModel.StatusMessage);
    }

    [Fact]
    public async Task SelectedStatus_FiltersByStatus()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(
            CreateCamera("Front Door", "192.168.1.10", CameraBrand.Hikvision, CameraStatus.Online, "Gate"),
            CreateCamera("Lobby", "10.0.0.25", CameraBrand.Dahua, CameraStatus.Offline, "Hall")));

        await viewModel.LoadAsync();
        viewModel.SelectedStatus = "Offline";

        Assert.Single(viewModel.Cameras);
        Assert.Equal("Lobby", viewModel.Cameras[0].Name);
        Assert.Equal("Filtered to 1 camera(s).", viewModel.StatusMessage);
    }

    [Fact]
    public async Task SearchAndFilters_ComposeTogether()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(
            CreateCamera("Gate A", "192.168.1.10", CameraBrand.Hikvision, CameraStatus.Online, "North"),
            CreateCamera("Gate B", "192.168.1.11", CameraBrand.Hikvision, CameraStatus.Offline, "South"),
            CreateCamera("Gate C", "192.168.1.12", CameraBrand.Dahua, CameraStatus.Online, "West")));

        await viewModel.LoadAsync();
        viewModel.SearchKeyword = "Gate";
        viewModel.SelectedBrand = "Hikvision";
        viewModel.SelectedStatus = "Online";
        viewModel.SearchCommand.Execute(null);

        Assert.Single(viewModel.Cameras);
        Assert.Equal("Gate A", viewModel.Cameras[0].Name);
        Assert.Equal("Found 1 camera(s).", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ClearCommand_ResetsSearchAndFilters()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(
            CreateCamera("Gate A", "192.168.1.10", CameraBrand.Hikvision, CameraStatus.Online, "North"),
            CreateCamera("Gate B", "192.168.1.11", CameraBrand.Dahua, CameraStatus.Offline, "South")));

        await viewModel.LoadAsync();
        viewModel.SearchKeyword = "Gate";
        viewModel.SelectedBrand = "Hikvision";
        viewModel.SelectedStatus = "Online";
        viewModel.SearchCommand.Execute(null);

        viewModel.ClearCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.SearchKeyword);
        Assert.Equal("All", viewModel.SelectedBrand);
        Assert.Equal("All", viewModel.SelectedStatus);
        Assert.Equal(2, viewModel.Cameras.Count);
        Assert.Equal("Loaded 2 camera(s).", viewModel.StatusMessage);
    }

    [Fact]
    public async Task ApplyFilters_ClearsSelectedCamera_WhenFilteredOut()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(
            CreateCamera("Front Door", "192.168.1.10", CameraBrand.Hikvision, CameraStatus.Online, "Gate"),
            CreateCamera("Lobby", "10.0.0.25", CameraBrand.Dahua, CameraStatus.Offline, "Hall")));

        await viewModel.LoadAsync();
        viewModel.SelectedCamera = viewModel.Cameras[0];

        viewModel.SelectedBrand = "Dahua";

        Assert.Null(viewModel.SelectedCamera);
        Assert.Single(viewModel.Cameras);
        Assert.Equal("Lobby", viewModel.Cameras[0].Name);
    }

    [Fact]
    public async Task RefreshAsync_SelectsNewlyAddedCamera_WhenVisible()
    {
        var repository = new FakeCameraRepository(
            CreateCamera("Front Door", "192.168.1.10", CameraBrand.Hikvision, CameraStatus.Online, "Gate"));
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync();

        var addedCamera = CreateCamera("Lobby", "10.0.0.25", CameraBrand.Dahua, CameraStatus.Offline, "Hall");
        repository.AddSeed(addedCamera);

        await viewModel.RefreshAsync(addedCamera.Id);

        Assert.Equal(2, viewModel.Cameras.Count);
        Assert.NotNull(viewModel.SelectedCamera);
        Assert.Equal(addedCamera.Id, viewModel.SelectedCamera!.SourceCamera.Id);
        Assert.Equal("Camera list refreshed.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RefreshAsync_PreservesSearchKeyword()
    {
        var repository = new FakeCameraRepository(
            CreateCamera("Front Door", "192.168.1.10", CameraBrand.Hikvision, CameraStatus.Online, "Gate"),
            CreateCamera("Lobby", "10.0.0.25", CameraBrand.Dahua, CameraStatus.Offline, "Hall"));
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync();
        viewModel.SearchKeyword = "Lobby";
        viewModel.SearchCommand.Execute(null);

        repository.ReplaceAll(
            CreateCamera("Front Door", "192.168.1.10", CameraBrand.Hikvision, CameraStatus.Online, "Gate"),
            CreateCamera("Lobby", "10.0.0.25", CameraBrand.Dahua, CameraStatus.Offline, "Hall"),
            CreateCamera("Warehouse", "10.0.0.30", CameraBrand.Axis, CameraStatus.Offline, "Storage"));

        await viewModel.RefreshAsync();

        Assert.Equal("Lobby", viewModel.SearchKeyword);
        Assert.Single(viewModel.Cameras);
        Assert.Equal("Lobby", viewModel.Cameras[0].Name);
        Assert.Equal("Camera list refreshed.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RefreshAsync_PreservesBrandAndStatusFilters()
    {
        var repository = new FakeCameraRepository(
            CreateCamera("Gate A", "192.168.1.10", CameraBrand.Hikvision, CameraStatus.Online, "North"),
            CreateCamera("Gate B", "192.168.1.11", CameraBrand.Hikvision, CameraStatus.Offline, "South"),
            CreateCamera("Gate C", "192.168.1.12", CameraBrand.Dahua, CameraStatus.Online, "West"));
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync();
        viewModel.SelectedBrand = "Hikvision";
        viewModel.SelectedStatus = "Online";

        repository.ReplaceAll(
            CreateCamera("Gate A", "192.168.1.10", CameraBrand.Hikvision, CameraStatus.Online, "North"),
            CreateCamera("Gate B", "192.168.1.11", CameraBrand.Hikvision, CameraStatus.Offline, "South"),
            CreateCamera("Gate C", "192.168.1.12", CameraBrand.Dahua, CameraStatus.Online, "West"),
            CreateCamera("Gate D", "192.168.1.13", CameraBrand.Hikvision, CameraStatus.Online, "East"));

        await viewModel.RefreshAsync();

        Assert.Equal("Hikvision", viewModel.SelectedBrand);
        Assert.Equal("Online", viewModel.SelectedStatus);
        Assert.Equal(2, viewModel.Cameras.Count);
        Assert.All(viewModel.Cameras, camera => Assert.Equal("Hikvision", camera.Brand));
        Assert.All(viewModel.Cameras, camera => Assert.Equal("Online", camera.Status));
        Assert.Equal("Camera list refreshed.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RefreshAsync_PreservesSelectedCamera_WhenStillVisible()
    {
        var target = CreateCamera("Lobby", "10.0.0.25", CameraBrand.Dahua, CameraStatus.Offline, "Hall");
        var repository = new FakeCameraRepository(
            CreateCamera("Front Door", "192.168.1.10", CameraBrand.Hikvision, CameraStatus.Online, "Gate"),
            target);
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync();
        viewModel.SelectedCamera = viewModel.Cameras[1];

        repository.ReplaceAll(
            CreateCamera("Front Door", "192.168.1.10", CameraBrand.Hikvision, CameraStatus.Online, "Gate"),
            new EntityCamera
            {
                Id = target.Id,
                Name = target.Name,
                IpAddress = target.IpAddress,
                Brand = target.Brand,
                Status = target.Status,
                Location = target.Location
            });

        await viewModel.RefreshAsync();

        Assert.NotNull(viewModel.SelectedCamera);
        Assert.Equal(target.Id, viewModel.SelectedCamera!.SourceCamera.Id);
        Assert.Equal("Camera list refreshed.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RefreshAsync_ClearsSelectedCamera_WhenItNoLongerExists()
    {
        var removed = CreateCamera("Lobby", "10.0.0.25", CameraBrand.Dahua, CameraStatus.Offline, "Hall");
        var repository = new FakeCameraRepository(
            CreateCamera("Front Door", "192.168.1.10", CameraBrand.Hikvision, CameraStatus.Online, "Gate"),
            removed);
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync();
        viewModel.SelectedCamera = viewModel.Cameras[1];

        repository.ReplaceAll(
            CreateCamera("Front Door", "192.168.1.10", CameraBrand.Hikvision, CameraStatus.Online, "Gate"));

        await viewModel.RefreshAsync();

        Assert.Null(viewModel.SelectedCamera);
        Assert.Equal("Camera list refreshed.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RefreshAsync_HandlesExceptionAndPreservesVisibleList()
    {
        var repository = new FakeCameraRepository(
            CreateCamera("Front Door", "192.168.1.10", CameraBrand.Hikvision, CameraStatus.Online, "Gate"),
            CreateCamera("Lobby", "10.0.0.25", CameraBrand.Dahua, CameraStatus.Offline, "Hall"));
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync();
        viewModel.SearchKeyword = "Lobby";
        viewModel.SearchCommand.Execute(null);
        repository.ThrowOnGetAll = true;

        await viewModel.RefreshAsync();

        Assert.Equal("Lobby", viewModel.SearchKeyword);
        Assert.Single(viewModel.Cameras);
        Assert.Equal("Lobby", viewModel.Cameras[0].Name);
        Assert.Equal("Failed to refresh camera list.", viewModel.StatusMessage);
    }

    // Epic-018 Milestone 18C: with an Operator role, every modification/Discovery command
    // reports CanExecute() == false regardless of selection state, while ViewLiveCommand (camera
    // selection) is unaffected by role -- Operator needs it to reach Live View (§2.5).
    [Fact]
    public void OperatorRole_ModificationAndDiscoveryCommands_AllReportCannotExecute()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(), Role.Operator);

        Assert.False(viewModel.AddCameraCommand.CanExecute(null));
        Assert.False(viewModel.ImportCommand.CanExecute(null));
        Assert.False(viewModel.BatchEditCommand.CanExecute(null));
        Assert.False(viewModel.BatchConnectionTestCommand.CanExecute(null));
        Assert.False(viewModel.ExportCommand.CanExecute(null));
        Assert.False(viewModel.ShowDiscoveryCommand.CanExecute(null));
    }

    [Fact]
    public async Task OperatorRole_ViewLiveCommand_UnaffectedByRole()
    {
        var repository = new FakeCameraRepository(
            CreateCamera("Front Door", "192.168.1.10", CameraBrand.RTSP, CameraStatus.Online, "Gate"));
        var viewModel = CreateViewModel(repository, Role.Operator);
        await viewModel.LoadAsync();

        Assert.False(viewModel.ViewLiveCommand.CanExecute(null));

        viewModel.SelectedCamera = viewModel.Cameras[0];

        Assert.True(viewModel.ViewLiveCommand.CanExecute(null));
    }

    [Fact]
    public void AdminRole_ModificationAndDiscoveryCommands_UnaffectedRegressionGuard()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(), Role.Admin);

        Assert.True(viewModel.AddCameraCommand.CanExecute(null));
        Assert.True(viewModel.ImportCommand.CanExecute(null));
        Assert.True(viewModel.ShowDiscoveryCommand.CanExecute(null));
        // BatchEdit/BatchConnectionTest/Export additionally require a selection/non-empty list --
        // already covered by CameraListViewModelBatchSelectionTests -- this only confirms Admin
        // is not blocked by role the way Operator is above.
    }

    private static CameraListViewModel CreateViewModel(ICameraRepository repository)
    {
        return new CameraListViewModel(new CameraQueryService(repository));
    }

    private static CameraListViewModel CreateViewModel(ICameraRepository repository, Role role)
    {
        return new CameraListViewModel(new CameraQueryService(repository), role: role);
    }

    private static EntityCamera CreateCamera(
        string name,
        string ipAddress,
        CameraBrand brand,
        CameraStatus status,
        string location)
    {
        return new EntityCamera
        {
            Name = name,
            IpAddress = ipAddress,
            Brand = brand,
            Status = status,
            Location = location
        };
    }

    private sealed class FakeCameraRepository : ICameraRepository
    {
        private readonly List<EntityCamera> _cameras;
        public bool ThrowOnGetAll { get; set; }

        public FakeCameraRepository(params EntityCamera[] cameras)
        {
            _cameras = cameras.ToList();
        }

        public IEnumerable<EntityCamera> GetAll()
        {
            if (ThrowOnGetAll)
            {
                throw new InvalidOperationException("Repository failure.");
            }

            return _cameras;
        }

        public EntityCamera? GetById(Guid id)
        {
            return _cameras.FirstOrDefault(camera => camera.Id == id);
        }

        public void Add(EntityCamera camera)
        {
            _cameras.Add(camera);
        }

        public void Update(EntityCamera camera)
        {
            throw new NotSupportedException();
        }

        public void Delete(Guid id)
        {
            throw new NotSupportedException();
        }

        public void AddSeed(EntityCamera camera)
        {
            _cameras.Add(camera);
        }

        public void ReplaceAll(params EntityCamera[] cameras)
        {
            _cameras.Clear();
            _cameras.AddRange(cameras);
        }
    }

    private sealed class ThrowingCameraRepository : ICameraRepository
    {
        public IEnumerable<EntityCamera> GetAll()
        {
            throw new InvalidOperationException("Repository failure.");
        }

        public EntityCamera? GetById(Guid id)
        {
            throw new InvalidOperationException("Repository failure.");
        }

        public void Add(EntityCamera camera)
        {
            throw new NotSupportedException();
        }

        public void Update(EntityCamera camera)
        {
            throw new NotSupportedException();
        }

        public void Delete(Guid id)
        {
            throw new NotSupportedException();
        }
    }
}
