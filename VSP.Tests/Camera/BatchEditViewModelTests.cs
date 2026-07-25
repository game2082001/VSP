using VSP.Device.Interfaces;
using VSP.Domain.Enums;
using VSP.UI.ViewModels;
using EntityCamera = VSP.Domain.Entities.Camera;
using Xunit;

namespace VSP.Tests.Camera;

public class BatchEditViewModelTests
{
    [Fact]
    public void Apply_WithNoFieldSelected_DoesNotUpdateAndShowsMessage()
    {
        var repository = new RecordingCameraRepository();
        var viewModel = new BatchEditViewModel(
            new[] { CreateCamera(), CreateCamera() },
            repository);

        viewModel.ApplyCommand.Execute(null);

        Assert.Equal("Select at least one field to apply.", viewModel.StatusMessage);
        Assert.Empty(repository.Updated);
        Assert.False(viewModel.WasApplied);
    }

    [Fact]
    public void Apply_WithBrandSelected_UpdatesBrandOnAllCameras()
    {
        var repository = new RecordingCameraRepository();
        var cameras = new[] { CreateCamera(), CreateCamera() };
        var viewModel = new BatchEditViewModel(cameras, repository)
        {
            ApplyBrand = true,
            Brand = nameof(CameraBrand.Hikvision)
        };

        viewModel.ApplyCommand.Execute(null);

        Assert.Equal(2, repository.Updated.Count);
        Assert.All(repository.Updated, camera => Assert.Equal(CameraBrand.Hikvision, camera.Brand));
        Assert.True(viewModel.WasApplied);
        Assert.Equal(2, viewModel.SuccessCount);
        Assert.Equal(0, viewModel.FailureCount);
    }

    [Fact]
    public void Apply_WithLocationAndUsernameSelected_UpdatesOnlyThoseFields()
    {
        var repository = new RecordingCameraRepository();
        var camera = CreateCamera();
        camera.Brand = CameraBrand.Dahua;
        var viewModel = new BatchEditViewModel(new[] { camera }, repository)
        {
            ApplyLocation = true,
            Location = "Rooftop",
            ApplyUsername = true,
            Username = "admin2"
        };

        viewModel.ApplyCommand.Execute(null);

        var updated = repository.Updated.Single();
        Assert.Equal("Rooftop", updated.Location);
        Assert.Equal("admin2", updated.Username);
        Assert.Equal(CameraBrand.Dahua, updated.Brand);
    }

    [Fact]
    public void Apply_WithRepositoryFailureOnOneCamera_ContinuesAndReportsFailureCount()
    {
        var repository = new RecordingCameraRepository { FailOnCameraName = "bad" };
        var goodCamera = CreateCamera("good");
        var badCamera = CreateCamera("bad");
        var viewModel = new BatchEditViewModel(new[] { goodCamera, badCamera }, repository)
        {
            ApplyLocation = true,
            Location = "New Location"
        };

        viewModel.ApplyCommand.Execute(null);

        Assert.Equal(1, viewModel.SuccessCount);
        Assert.Equal(1, viewModel.FailureCount);
        Assert.Contains("1 failed", viewModel.StatusMessage);
    }

    [Fact]
    public void Apply_RaisesRequestCloseAfterProcessing()
    {
        var repository = new RecordingCameraRepository();
        var viewModel = new BatchEditViewModel(new[] { CreateCamera() }, repository)
        {
            ApplyLocation = true,
            Location = "X"
        };

        var closeRaised = false;
        viewModel.RequestClose += () => closeRaised = true;

        viewModel.ApplyCommand.Execute(null);

        Assert.True(closeRaised);
    }

    [Fact]
    public void Constructor_WithEmptyCameraList_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new BatchEditViewModel(Array.Empty<EntityCamera>(), new RecordingCameraRepository()));
    }

    [Fact]
    public void Constructor_WithNullCameras_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BatchEditViewModel(null!, new RecordingCameraRepository()));
    }

    [Fact]
    public void Constructor_WithNullRepository_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BatchEditViewModel(new[] { CreateCamera() }, null!));
    }

    private static EntityCamera CreateCamera(string name = "Camera")
    {
        return new EntityCamera
        {
            Name = name,
            IpAddress = "192.168.1.1",
            Brand = CameraBrand.Unknown,
            Location = "Original"
        };
    }

    private sealed class RecordingCameraRepository : ICameraRepository
    {
        public List<EntityCamera> Updated { get; } = new();
        public string? FailOnCameraName { get; set; }

        public IEnumerable<EntityCamera> GetAll() => Updated;

        public EntityCamera? GetById(Guid id) => Updated.FirstOrDefault(c => c.Id == id);

        public void Add(EntityCamera camera) => Updated.Add(camera);

        public void Update(EntityCamera camera)
        {
            if (FailOnCameraName is not null && camera.Name == FailOnCameraName)
            {
                throw new InvalidOperationException("Simulated update failure.");
            }

            Updated.Add(camera);
        }

        public void Delete(Guid id)
        {
        }
    }
}
