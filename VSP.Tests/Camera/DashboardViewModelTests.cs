using VSP.Device.Drivers;
using VSP.Device.Interfaces;
using VSP.Device.Services;
using VSP.Domain.Enums;
using VSP.UI.ViewModels;
using Xunit;
using EntityCamera = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Camera;

public class DashboardViewModelTests
{
    [Fact]
    public async Task LoadAsync_WithNoCameras_PopulatesEmptySummary()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository());

        await viewModel.LoadAsync();

        Assert.Equal(0, viewModel.Summary.TotalCameras);
        Assert.False(viewModel.HasError);
        Assert.NotEqual("Never", viewModel.LastRefreshedText);
    }

    [Fact]
    public async Task LoadAsync_WithCameras_PopulatesSummaryFromRepository()
    {
        var repository = new FakeCameraRepository(
            new EntityCamera { Name = "Front Door", Status = CameraStatus.Online, ConnectionType = DeviceConnectionType.RTSP });
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync();

        Assert.Equal(1, viewModel.Summary.TotalCameras);
        Assert.Equal(1, viewModel.Summary.OnlineCount);
    }

    [Fact]
    public async Task LoadAsync_CalledTwice_OnlyLoadsOnce()
    {
        var repository = new FakeCameraRepository();
        var viewModel = CreateViewModel(repository);

        await viewModel.LoadAsync();
        await viewModel.LoadAsync();

        Assert.Equal(1, repository.GetAllCallCount);
    }

    [Fact]
    public async Task RefreshAsync_AlwaysReloads()
    {
        var repository = new FakeCameraRepository();
        var viewModel = CreateViewModel(repository);
        await viewModel.LoadAsync();

        await viewModel.RefreshAsync();

        Assert.Equal(2, repository.GetAllCallCount);
    }

    [Fact]
    public async Task RefreshAsync_WhenRepositoryThrows_SetsErrorState()
    {
        var viewModel = CreateViewModel(new ThrowingCameraRepository());

        await viewModel.RefreshAsync();

        Assert.True(viewModel.HasError);
        Assert.StartsWith("Failed to load dashboard data:", viewModel.StatusMessage);
        Assert.False(viewModel.IsLoading);
    }

    [Fact]
    public async Task RefreshAsync_AfterPriorError_ClearsErrorStateOnNextSuccessfulRefresh()
    {
        var repository = new FlakyCameraRepository(failFirstCall: true);
        var viewModel = CreateViewModel(repository);

        await viewModel.RefreshAsync();
        Assert.True(viewModel.HasError);

        await viewModel.RefreshAsync();

        Assert.False(viewModel.HasError);
        Assert.Equal(0, viewModel.Summary.TotalCameras);
    }

    private static DashboardViewModel CreateViewModel(ICameraRepository repository)
    {
        return new DashboardViewModel(new CameraQueryService(repository), DriverRegistry.CreateDefault());
    }

    private sealed class FakeCameraRepository : ICameraRepository
    {
        private readonly List<EntityCamera> _cameras;

        public FakeCameraRepository(params EntityCamera[] cameras)
        {
            _cameras = cameras.ToList();
        }

        public int GetAllCallCount { get; private set; }

        public IEnumerable<EntityCamera> GetAll()
        {
            GetAllCallCount++;
            return _cameras;
        }

        public EntityCamera? GetById(Guid id) => _cameras.FirstOrDefault(camera => camera.Id == id);

        public void Add(EntityCamera camera) => _cameras.Add(camera);

        public void Update(EntityCamera camera)
        {
        }

        public void Delete(Guid id)
        {
        }
    }

    private sealed class ThrowingCameraRepository : ICameraRepository
    {
        public IEnumerable<EntityCamera> GetAll()
        {
            throw new InvalidOperationException("Repository failure.");
        }

        public EntityCamera? GetById(Guid id) => null;

        public void Add(EntityCamera camera)
        {
        }

        public void Update(EntityCamera camera)
        {
        }

        public void Delete(Guid id)
        {
        }
    }

    private sealed class FlakyCameraRepository : ICameraRepository
    {
        private bool _shouldFail;

        public FlakyCameraRepository(bool failFirstCall)
        {
            _shouldFail = failFirstCall;
        }

        public IEnumerable<EntityCamera> GetAll()
        {
            if (_shouldFail)
            {
                _shouldFail = false;
                throw new InvalidOperationException("Repository failure.");
            }

            return [];
        }

        public EntityCamera? GetById(Guid id) => null;

        public void Add(EntityCamera camera)
        {
        }

        public void Update(EntityCamera camera)
        {
        }

        public void Delete(Guid id)
        {
        }
    }
}
