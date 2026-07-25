using VSP.Device.Interfaces;
using VSP.Device.Services;
using VSP.Domain.Enums;
using VSP.UI.ViewModels;
using EntityCamera = VSP.Domain.Entities.Camera;
using Xunit;

namespace VSP.Tests.Camera;

public class CameraListViewModelBatchSelectionTests
{
    [Fact]
    public async Task SelectedItemCount_ReflectsSelectedItems()
    {
        var viewModel = await CreateLoadedViewModelAsync(CreateCamera("A"), CreateCamera("B"), CreateCamera("C"));

        viewModel.Cameras[0].IsSelected = true;
        viewModel.Cameras[1].IsSelected = true;

        Assert.Equal(2, viewModel.SelectedItemCount);
    }

    [Fact]
    public async Task BatchEditCommand_IsDisabledWithFewerThanTwoSelected()
    {
        var viewModel = await CreateLoadedViewModelAsync(CreateCamera("A"), CreateCamera("B"));

        Assert.False(viewModel.BatchEditCommand.CanExecute(null));

        viewModel.Cameras[0].IsSelected = true;

        Assert.False(viewModel.BatchEditCommand.CanExecute(null));

        viewModel.Cameras[1].IsSelected = true;

        Assert.True(viewModel.BatchEditCommand.CanExecute(null));
    }

    [Fact]
    public async Task BatchEditCommand_RaisesRequestBatchEditWithSelectedSourceCameras()
    {
        var viewModel = await CreateLoadedViewModelAsync(CreateCamera("A"), CreateCamera("B"), CreateCamera("C"));
        viewModel.Cameras[0].IsSelected = true;
        viewModel.Cameras[2].IsSelected = true;

        IReadOnlyList<EntityCamera>? raised = null;
        viewModel.RequestBatchEdit += cameras => raised = cameras;

        viewModel.BatchEditCommand.Execute(null);

        Assert.NotNull(raised);
        Assert.Equal(2, raised!.Count);
        Assert.Contains(raised, c => c.Name == "A");
        Assert.Contains(raised, c => c.Name == "C");
    }

    [Fact]
    public async Task BatchEditCommand_WithFewerThanTwoSelected_DoesNotRaiseRequestBatchEdit()
    {
        var viewModel = await CreateLoadedViewModelAsync(CreateCamera("A"), CreateCamera("B"));
        viewModel.Cameras[0].IsSelected = true;

        var raised = false;
        viewModel.RequestBatchEdit += _ => raised = true;

        viewModel.BatchEditCommand.Execute(null);

        Assert.False(raised);
    }

    [Fact]
    public async Task BatchConnectionTestCommand_IsDisabledWithNoneSelected()
    {
        var viewModel = await CreateLoadedViewModelAsync(CreateCamera("A"), CreateCamera("B"));

        Assert.False(viewModel.BatchConnectionTestCommand.CanExecute(null));

        viewModel.Cameras[0].IsSelected = true;

        Assert.True(viewModel.BatchConnectionTestCommand.CanExecute(null));
    }

    [Fact]
    public async Task BatchConnectionTestCommand_RaisesRequestBatchConnectionTestWithSelectedSourceCameras()
    {
        var viewModel = await CreateLoadedViewModelAsync(CreateCamera("A"), CreateCamera("B"), CreateCamera("C"));
        viewModel.Cameras[1].IsSelected = true;

        IReadOnlyList<EntityCamera>? raised = null;
        viewModel.RequestBatchConnectionTest += cameras => raised = cameras;

        viewModel.BatchConnectionTestCommand.Execute(null);

        Assert.NotNull(raised);
        Assert.Equal(1, raised!.Count);
        Assert.Contains(raised, c => c.Name == "B");
    }

    [Fact]
    public async Task BatchConnectionTestCommand_WithNoneSelected_DoesNotRaiseRequestBatchConnectionTest()
    {
        var viewModel = await CreateLoadedViewModelAsync(CreateCamera("A"));

        var raised = false;
        viewModel.RequestBatchConnectionTest += _ => raised = true;

        viewModel.BatchConnectionTestCommand.Execute(null);

        Assert.False(raised);
    }

    [Fact]
    public async Task ExportCommand_IsDisabledWhenListIsEmpty()
    {
        var viewModel = new CameraListViewModel(new CameraQueryService(new FakeCameraRepository()));
        await viewModel.LoadAsync();

        Assert.False(viewModel.ExportCommand.CanExecute(null));
    }

    [Fact]
    public async Task ExportCommand_IsEnabledWhenListIsNonEmpty()
    {
        var viewModel = await CreateLoadedViewModelAsync(CreateCamera("A"));

        Assert.True(viewModel.ExportCommand.CanExecute(null));
    }

    [Fact]
    public async Task ExportCommand_RaisesRequestExportWithAllVisibleCameras()
    {
        var viewModel = await CreateLoadedViewModelAsync(CreateCamera("A"), CreateCamera("B"));

        IReadOnlyList<EntityCamera>? raised = null;
        viewModel.RequestExport += cameras => raised = cameras;

        viewModel.ExportCommand.Execute(null);

        Assert.NotNull(raised);
        Assert.Equal(2, raised!.Count);
    }

    [Fact]
    public async Task ExportCommand_RaisesRequestExportWithOnlyFilteredCameras()
    {
        var viewModel = await CreateLoadedViewModelAsync(
            CreateCamera("A", CameraBrand.Hikvision),
            CreateCamera("B", CameraBrand.Dahua));
        viewModel.SelectedBrand = "Hikvision";

        IReadOnlyList<EntityCamera>? raised = null;
        viewModel.RequestExport += cameras => raised = cameras;

        viewModel.ExportCommand.Execute(null);

        Assert.NotNull(raised);
        Assert.Equal(1, raised!.Count);
        Assert.Contains(raised, c => c.Name == "A");
    }

    private static async Task<CameraListViewModel> CreateLoadedViewModelAsync(params EntityCamera[] cameras)
    {
        var viewModel = new CameraListViewModel(new CameraQueryService(new FakeCameraRepository(cameras)));
        await viewModel.LoadAsync();
        return viewModel;
    }

    private static EntityCamera CreateCamera(string name, CameraBrand brand = CameraBrand.Unknown)
    {
        return new EntityCamera { Name = name, IpAddress = "192.168.1.1", Brand = brand };
    }

    private sealed class FakeCameraRepository : ICameraRepository
    {
        private readonly List<EntityCamera> _cameras;

        public FakeCameraRepository(params EntityCamera[] cameras)
        {
            _cameras = cameras.ToList();
        }

        public IEnumerable<EntityCamera> GetAll() => _cameras;

        public EntityCamera? GetById(Guid id) => _cameras.FirstOrDefault(c => c.Id == id);

        public void Add(EntityCamera camera) => _cameras.Add(camera);

        public void Update(EntityCamera camera)
        {
        }

        public void Delete(Guid id)
        {
        }
    }
}
