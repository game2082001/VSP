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
        var viewModel = new CameraDetailViewModel(CreateCamera());

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
    }

    [Fact]
    public void EditCommand_EntersEditMode()
    {
        var viewModel = new CameraDetailViewModel(CreateCamera());

        viewModel.EditCommand.Execute(null);

        Assert.True(viewModel.IsEditMode);
        Assert.Equal("Edit mode enabled. Changes are not saved to storage.", viewModel.StatusMessage);
    }

    [Fact]
    public void Name_RequiresValue()
    {
        var viewModel = CreateEditableViewModel();

        viewModel.Name = string.Empty;

        Assert.False(viewModel.IsNameValid);
        Assert.Equal("Name is required.", viewModel.NameError);
        Assert.False(viewModel.IsFormValid);
    }

    [Fact]
    public void IpAddress_MustBeValidIpv4()
    {
        var viewModel = CreateEditableViewModel();

        viewModel.IpAddress = "999.1.1.1";

        Assert.False(viewModel.IsIpAddressValid);
        Assert.Equal("Invalid IP address.", viewModel.IpAddressError);
        Assert.False(viewModel.IsFormValid);
    }

    [Fact]
    public void HttpPort_MustBeInRange()
    {
        var viewModel = CreateEditableViewModel();

        viewModel.HttpPort = "70000";

        Assert.False(viewModel.IsHttpPortValid);
        Assert.Equal("HTTP port must be between 1 and 65535.", viewModel.HttpPortError);
        Assert.False(viewModel.IsFormValid);
    }

    [Fact]
    public void RtspPort_MustBeInRange()
    {
        var viewModel = CreateEditableViewModel();

        viewModel.RtspPort = "0";

        Assert.False(viewModel.IsRtspPortValid);
        Assert.Equal("RTSP port must be between 1 and 65535.", viewModel.RtspPortError);
        Assert.False(viewModel.IsFormValid);
    }

    [Fact]
    public void SdkPort_MustBeInRange()
    {
        var viewModel = CreateEditableViewModel();

        viewModel.SdkPort = "-1";

        Assert.False(viewModel.IsSdkPortValid);
        Assert.Equal("SDK port must be between 1 and 65535.", viewModel.SdkPortError);
        Assert.False(viewModel.IsFormValid);
    }

    [Fact]
    public void ValidInput_KeepsFormValid()
    {
        var viewModel = CreateEditableViewModel();

        viewModel.Name = "Lobby";
        viewModel.IpAddress = "10.0.0.50";
        viewModel.HttpPort = "80";
        viewModel.RtspPort = "554";
        viewModel.SdkPort = "8000";

        Assert.True(viewModel.IsFormValid);
    }

    [Fact]
    public void ApplyEditCommand_DoesNotPersistAndShowsValidationStatus()
    {
        var viewModel = CreateEditableViewModel();

        viewModel.ApplyEditCommand.Execute(null);

        Assert.Equal("Validation passed. Persistence is not implemented.", viewModel.StatusMessage);
    }

    [Fact]
    public void ApplyEditCommand_ShowsValidationFailureStatus()
    {
        var viewModel = CreateEditableViewModel();
        viewModel.IpAddress = "bad-ip";

        viewModel.ApplyEditCommand.Execute(null);

        Assert.Equal("Please fix validation errors before applying changes.", viewModel.StatusMessage);
    }

    [Fact]
    public void CloseCommand_RaisesRequestClose()
    {
        var viewModel = new CameraDetailViewModel(CreateCamera());
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

        var viewModel = new CameraDetailViewModel(camera);

        Assert.Equal(string.Empty, viewModel.Name);
        Assert.Equal(string.Empty, viewModel.Model);
        Assert.Equal(string.Empty, viewModel.RtspUrl);
        Assert.Equal("Offline", viewModel.Status);
        Assert.Equal("No", viewModel.Recording);
    }

    private static CameraDetailViewModel CreateEditableViewModel()
    {
        var viewModel = new CameraDetailViewModel(CreateCamera());
        viewModel.EditCommand.Execute(null);
        return viewModel;
    }

    private static EntityCamera CreateCamera()
    {
        return new EntityCamera
        {
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
}
