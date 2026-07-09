using VSP.Device.Interfaces;
using VSP.Domain.Enums;
using VSP.UI.ViewModels;
using EntityCamera = VSP.Domain.Entities.Camera;
using Xunit;

namespace VSP.Tests.Camera;

public class CameraDetailViewModelTests
{
    [Fact]
    public void Constructor_MapsCameraFields()
    {
        var viewModel = new CameraDetailViewModel(CreateCamera(), new FakeCameraRepository());

        Assert.Equal("North Gate", viewModel.Name);
        Assert.Equal("Hikvision", viewModel.Brand);
        Assert.Equal("DS-2CD2143G2", viewModel.Model);
        Assert.Equal("192.168.10.15", viewModel.IpAddress);
        Assert.Equal("80", viewModel.HttpPort);
        Assert.Equal("554", viewModel.RtspPort);
        Assert.Equal("8000", viewModel.SdkPort);
        Assert.Equal("admin", viewModel.Username);
        Assert.Equal("********", viewModel.MaskedPassword);
        Assert.Equal("rtsp://192.168.10.15/live", viewModel.RtspUrl);
        Assert.Equal("Online", viewModel.Status);
        Assert.Equal("Yes", viewModel.Recording);
        Assert.Equal("Entrance", viewModel.Location);
        Assert.Equal("2026-07-01 08:30:00", viewModel.CreateTime);
        Assert.Equal("2026-07-02 09:45:00", viewModel.LastModifyTime);
        Assert.False(viewModel.IsEditMode);
        Assert.False(viewModel.IsNewMode);
    }

    [Fact]
    public void EditCommand_EntersEditMode()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository());

        viewModel.EditCommand.Execute(null);

        Assert.True(viewModel.IsEditMode);
        Assert.Equal("Edit mode enabled.", viewModel.StatusMessage);
    }

    [Fact]
    public void NewMode_UsesExplicitDefaultValues()
    {
        var viewModel = new CameraDetailViewModel(new FakeCameraRepository());

        Assert.True(viewModel.IsNewMode);
        Assert.True(viewModel.IsEditMode);
        Assert.Equal("Add Camera", viewModel.Title);
        Assert.Equal("Unknown", viewModel.Brand);
        Assert.Equal("Offline", viewModel.Status);
        Assert.Equal("No", viewModel.Recording);
        Assert.Equal("80", viewModel.HttpPort);
        Assert.Equal("554", viewModel.RtspPort);
        Assert.Equal("8000", viewModel.SdkPort);
        Assert.Equal(string.Empty, viewModel.Username);
        Assert.Equal(string.Empty, viewModel.Password);
        Assert.Equal(string.Empty, viewModel.RtspUrl);
        Assert.Equal("Ready to add camera.", viewModel.StatusMessage);
    }

    [Fact]
    public void Name_RequiresValue()
    {
        var viewModel = CreateEditableViewModel(new FakeCameraRepository());

        viewModel.Name = string.Empty;

        Assert.False(viewModel.IsNameValid);
        Assert.Equal("Name is required.", viewModel.NameError);
        Assert.False(viewModel.IsFormValid);
    }

    [Fact]
    public void IpAddress_MustBeValidIpv4()
    {
        var viewModel = CreateEditableViewModel(new FakeCameraRepository());

        viewModel.IpAddress = "999.1.1.1";

        Assert.False(viewModel.IsIpAddressValid);
        Assert.Equal("Invalid IP address.", viewModel.IpAddressError);
        Assert.False(viewModel.IsFormValid);
    }

    [Fact]
    public void HttpPort_MustBeInRange()
    {
        var viewModel = CreateEditableViewModel(new FakeCameraRepository());

        viewModel.HttpPort = "70000";

        Assert.False(viewModel.IsHttpPortValid);
        Assert.Equal("HTTP port must be between 1 and 65535.", viewModel.HttpPortError);
        Assert.False(viewModel.IsFormValid);
    }

    [Fact]
    public void RtspPort_MustBeInRange()
    {
        var viewModel = CreateEditableViewModel(new FakeCameraRepository());

        viewModel.RtspPort = "0";

        Assert.False(viewModel.IsRtspPortValid);
        Assert.Equal("RTSP port must be between 1 and 65535.", viewModel.RtspPortError);
        Assert.False(viewModel.IsFormValid);
    }

    [Fact]
    public void SdkPort_MustBeInRange()
    {
        var viewModel = CreateEditableViewModel(new FakeCameraRepository());

        viewModel.SdkPort = "-1";

        Assert.False(viewModel.IsSdkPortValid);
        Assert.Equal("SDK port must be between 1 and 65535.", viewModel.SdkPortError);
        Assert.False(viewModel.IsFormValid);
    }

    [Fact]
    public void ValidInput_KeepsFormValid()
    {
        var viewModel = CreateEditableViewModel(new FakeCameraRepository());

        viewModel.Name = "Lobby";
        viewModel.IpAddress = "10.0.0.50";
        viewModel.HttpPort = "80";
        viewModel.RtspPort = "554";
        viewModel.SdkPort = "8000";

        Assert.True(viewModel.IsFormValid);
    }

    [Fact]
    public void SaveCommand_SavesSuccessfully()
    {
        var repository = new FakeCameraRepository();
        var viewModel = CreateEditableViewModel(repository);

        viewModel.Name = "Lobby";
        viewModel.SaveCommand.Execute(null);

        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Equal("Lobby", repository.LastUpdatedCamera!.Name);
        Assert.Equal("Camera saved successfully.", viewModel.StatusMessage);
    }

    [Fact]
    public void SaveCommand_InNewMode_AddsCameraAndRequestsClose()
    {
        var repository = new FakeCameraRepository();
        var viewModel = new CameraDetailViewModel(repository);
        var wasClosed = false;

        viewModel.RequestClose += () => wasClosed = true;
        viewModel.Name = "Lobby";
        viewModel.IpAddress = "10.0.0.50";

        viewModel.SaveCommand.Execute(null);

        Assert.Equal(1, repository.AddCallCount);
        Assert.True(wasClosed);
        Assert.True(viewModel.WasSaved);
        Assert.NotNull(viewModel.SavedCameraId);
        Assert.Equal("Camera added successfully.", viewModel.StatusMessage);
        Assert.Equal("Lobby", repository.LastAddedCamera!.Name);
        Assert.Equal(CameraBrand.Unknown, repository.LastAddedCamera.Brand);
        Assert.Equal(DeviceConnectionType.Unknown, repository.LastAddedCamera.ConnectionType);
        Assert.Equal(CameraStatus.Offline, repository.LastAddedCamera.Status);
        Assert.False(repository.LastAddedCamera.Recording);
    }

    [Fact]
    public void SaveCommand_ValidationBlocksSave()
    {
        var repository = new FakeCameraRepository();
        var viewModel = CreateEditableViewModel(repository);
        viewModel.IpAddress = "bad-ip";

        viewModel.SaveCommand.Execute(null);

        Assert.Equal(0, repository.UpdateCallCount);
        Assert.Equal("Please fix validation errors before saving.", viewModel.StatusMessage);
    }

    [Fact]
    public void SaveCommand_HandlesRepositoryException()
    {
        var repository = new ThrowingCameraRepository();
        var viewModel = CreateEditableViewModel(repository);

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("Failed to save camera: Repository failure.", viewModel.StatusMessage);
    }

    [Fact]
    public void SaveCommand_InNewMode_HandlesAddExceptionWithoutClosing()
    {
        var repository = new ThrowingCameraRepository();
        var viewModel = new CameraDetailViewModel(repository);
        var wasClosed = false;

        viewModel.RequestClose += () => wasClosed = true;
        viewModel.Name = "Lobby";
        viewModel.IpAddress = "10.0.0.50";

        viewModel.SaveCommand.Execute(null);

        Assert.False(wasClosed);
        Assert.False(viewModel.WasSaved);
        Assert.Null(viewModel.SavedCameraId);
        Assert.Equal("Failed to add camera: Repository failure.", viewModel.StatusMessage);
    }

    [Fact]
    public void SaveCommand_UpdatesLastModifyTimeDisplay()
    {
        var repository = new FakeCameraRepository();
        var viewModel = CreateEditableViewModel(repository);

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("2026-07-09 10:30:00", viewModel.LastModifyTime);
    }

    [Fact]
    public void CloseCommand_RaisesRequestClose()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository());
        var wasRaised = false;

        viewModel.RequestClose += () => wasRaised = true;

        viewModel.CloseCommand.Execute(null);

        Assert.True(wasRaised);
    }

    [Fact]
    public void Constructor_HandlesEmptyStringsWithoutThrowing()
    {
        var camera = new EntityCamera
        {
            CreateTime = new DateTime(2026, 7, 1, 8, 30, 0),
            LastModifyTime = new DateTime(2026, 7, 2, 9, 45, 0)
        };

        var viewModel = new CameraDetailViewModel(camera, new FakeCameraRepository());

        Assert.Equal(string.Empty, viewModel.Name);
        Assert.Equal(string.Empty, viewModel.Model);
        Assert.Equal(string.Empty, viewModel.RtspUrl);
        Assert.Equal("Offline", viewModel.Status);
        Assert.Equal("No", viewModel.Recording);
    }

    private static CameraDetailViewModel CreateViewModel(ICameraRepository repository)
    {
        return new CameraDetailViewModel(CreateCamera(), repository);
    }

    private static CameraDetailViewModel CreateEditableViewModel(ICameraRepository repository)
    {
        var viewModel = CreateViewModel(repository);
        viewModel.EditCommand.Execute(null);
        return viewModel;
    }

    private static EntityCamera CreateCamera()
    {
        return new EntityCamera
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Name = "North Gate",
            Brand = CameraBrand.Hikvision,
            Model = "DS-2CD2143G2",
            IpAddress = "192.168.10.15",
            HttpPort = 80,
            RtspPort = 554,
            SdkPort = 8000,
            Username = "admin",
            Password = "p@ssw0rd",
            RtspUrl = "rtsp://192.168.10.15/live",
            Status = CameraStatus.Online,
            Recording = true,
            Location = "Entrance",
            CreateTime = new DateTime(2026, 7, 1, 8, 30, 0),
            LastModifyTime = new DateTime(2026, 7, 2, 9, 45, 0)
        };
    }

    private sealed class FakeCameraRepository : ICameraRepository
    {
        public int AddCallCount { get; private set; }
        public int UpdateCallCount { get; private set; }
        public EntityCamera? LastAddedCamera { get; private set; }
        public EntityCamera? LastUpdatedCamera { get; private set; }

        public IEnumerable<EntityCamera> GetAll()
        {
            return [];
        }

        public EntityCamera? GetById(Guid id)
        {
            return null;
        }

        public void Add(EntityCamera camera)
        {
            AddCallCount++;
            LastAddedCamera = camera;
        }

        public void Update(EntityCamera camera)
        {
            UpdateCallCount++;
            camera.LastModifyTime = new DateTime(2026, 7, 9, 10, 30, 0);
            LastUpdatedCamera = camera;
        }

        public void Delete(Guid id)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class ThrowingCameraRepository : ICameraRepository
    {
        public IEnumerable<EntityCamera> GetAll()
        {
            return [];
        }

        public EntityCamera? GetById(Guid id)
        {
            return null;
        }

        public void Add(EntityCamera camera)
        {
            throw new InvalidOperationException("Repository failure.");
        }

        public void Update(EntityCamera camera)
        {
            throw new InvalidOperationException("Repository failure.");
        }

        public void Delete(Guid id)
        {
            throw new NotSupportedException();
        }
    }
}
