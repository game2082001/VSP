using VSP.Device.Drivers.Settings;
using VSP.Device.Interfaces;
using VSP.Domain.Enums;
using VSP.Tests.Drivers.RTSP;
using VSP.UI.ViewModels;
using EntityCamera = VSP.Domain.Entities.Camera;
using Xunit;

namespace VSP.Tests.Camera;

public class CameraDetailViewModelTests
{
    // Epic-018 Milestone 18C: with an Operator role, Edit/Save/Delete all report
    // CanExecute() == false -- Operator gets a read-only Camera Detail (§4.5/§8.4).
    // TestConnectionCommand is deliberately unaffected (inspects connectivity, does not modify
    // stored camera data).
    [Fact]
    public void OperatorRole_EditSaveDeleteCommands_AllReportCannotExecute()
    {
        var viewModel = new CameraDetailViewModel(CreateCamera(), new FakeCameraRepository(), Role.Operator);

        Assert.False(viewModel.EditCommand.CanExecute(null));
        Assert.False(viewModel.SaveCommand.CanExecute(null));
        Assert.False(viewModel.DeleteCommand.CanExecute(null));
        Assert.True(viewModel.TestConnectionCommand.CanExecute(null));
    }

    [Fact]
    public void OperatorRole_EditCommandExecuted_DoesNotEnterEditMode()
    {
        // Defense in depth: even if something invoked Execute() directly bypassing the disabled
        // UI button, EnterEditMode's own CanExecute-guarded command should never have been wired
        // to actually change state for Operator. RelayCommand.Execute does not itself consult
        // CanExecute, so this documents that Save stays blocked even if Edit were forced open.
        var viewModel = new CameraDetailViewModel(CreateCamera(), new FakeCameraRepository(), Role.Operator);

        viewModel.EditCommand.Execute(null);

        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void AdminRole_EditSaveDeleteCommands_UnaffectedRegressionGuard()
    {
        var viewModel = new CameraDetailViewModel(CreateCamera(), new FakeCameraRepository(), Role.Admin);

        Assert.True(viewModel.EditCommand.CanExecute(null));
        Assert.True(viewModel.DeleteCommand.CanExecute(null));

        viewModel.EditCommand.Execute(null);

        Assert.True(viewModel.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void Constructor_MapsCameraFields()
    {
        var viewModel = new CameraDetailViewModel(CreateCamera(), new FakeCameraRepository());

        Assert.Equal("North Gate", viewModel.Name);
        Assert.Equal("Hikvision", viewModel.Brand);
        Assert.Equal("DS-2CD2143G2", viewModel.Model);
        Assert.Equal("192.168.10.15", viewModel.IpAddress);
        Assert.Equal("HikvisionISAPI", viewModel.ConnectionType);
        Assert.Equal("80", GetSetting(viewModel, DriverSettingKey.HttpPort).Value);
        Assert.Equal("8000", GetSetting(viewModel, DriverSettingKey.SdkPort).Value);
        Assert.Equal("admin", GetSetting(viewModel, DriverSettingKey.Username).Value);
        Assert.Equal("", GetSetting(viewModel, DriverSettingKey.Password).DisplayValue);
        Assert.Equal("Online", viewModel.Status);
        Assert.Equal("Yes", viewModel.Recording);
        Assert.Equal("Entrance", viewModel.Location);
        Assert.Equal("2026-07-01 08:30:00", viewModel.CreateTime);
        Assert.Equal("2026-07-02 09:45:00", viewModel.LastModifyTime);
        Assert.False(viewModel.IsEditMode);
        Assert.False(viewModel.IsNewMode);
    }

    [Fact]
    public void Constructor_DoesNotIncludeSettingsForKeysNotInTheDriverDefinition()
    {
        var viewModel = new CameraDetailViewModel(CreateCamera(), new FakeCameraRepository());

        Assert.DoesNotContain(viewModel.DriverSettings, setting => setting.Key == DriverSettingKey.RtspPort);
        Assert.DoesNotContain(viewModel.DriverSettings, setting => setting.Key == DriverSettingKey.RtspUrl);
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
        Assert.Equal("Unknown", viewModel.ConnectionType);
        Assert.Equal("Offline", viewModel.Status);
        Assert.Equal("No", viewModel.Recording);
        Assert.Equal("Ready to add camera.", viewModel.StatusMessage);
        Assert.False(viewModel.IsDirty);
        Assert.False(viewModel.DeleteCommand.CanExecute(null));
    }

    [Fact]
    public void NewMode_WithUnknownConnectionType_HasNoDriverSettings()
    {
        var viewModel = new CameraDetailViewModel(new FakeCameraRepository());

        Assert.Empty(viewModel.DriverSettings);
    }

    [Fact]
    public void SelectingConnectionType_PopulatesDriverSettingsFromDefinition()
    {
        var viewModel = new CameraDetailViewModel(new FakeCameraRepository());

        viewModel.ConnectionType = nameof(DeviceConnectionType.RTSP);

        Assert.Equal("554", GetSetting(viewModel, DriverSettingKey.RtspPort).Value);
        Assert.Equal(string.Empty, GetSetting(viewModel, DriverSettingKey.RtspUrl).Value);
        Assert.DoesNotContain(viewModel.DriverSettings, setting => setting.Key == DriverSettingKey.HttpPort);
    }

    [Fact]
    public void ChangingConnectionType_PreservesValuesForSharedKeys()
    {
        var viewModel = new CameraDetailViewModel(CreateCamera(), new FakeCameraRepository());
        viewModel.EditCommand.Execute(null);
        GetSetting(viewModel, DriverSettingKey.Username).Value = "edited-user";

        viewModel.ConnectionType = nameof(DeviceConnectionType.DahuaNetSDK);

        Assert.Equal("edited-user", GetSetting(viewModel, DriverSettingKey.Username).Value);
    }

    [Fact]
    public void ChangingConnectionType_DropsSettingsNotInTheNewDefinition()
    {
        var camera = CreateCamera();
        camera.ConnectionType = DeviceConnectionType.RTSP;
        var viewModel = new CameraDetailViewModel(camera, new FakeCameraRepository());
        viewModel.EditCommand.Execute(null);

        viewModel.ConnectionType = nameof(DeviceConnectionType.ONVIF);

        Assert.DoesNotContain(viewModel.DriverSettings, setting => setting.Key == DriverSettingKey.RtspUrl);
        Assert.DoesNotContain(viewModel.DriverSettings, setting => setting.Key == DriverSettingKey.RtspPort);
    }

    [Fact]
    public void IsDirty_ChangesInEditMode()
    {
        var viewModel = CreateEditableViewModel(new FakeCameraRepository());

        viewModel.Name = "Lobby";

        Assert.True(viewModel.IsDirty);
    }

    [Fact]
    public void IsDirty_ChangesInNewMode()
    {
        var viewModel = new CameraDetailViewModel(new FakeCameraRepository());

        viewModel.Name = "Lobby";

        Assert.True(viewModel.IsDirty);
    }

    [Fact]
    public void IsDirty_ChangesWhenDriverSettingValueChanges()
    {
        var viewModel = CreateEditableViewModel(new FakeCameraRepository());

        GetSetting(viewModel, DriverSettingKey.Username).Value = "changed";

        Assert.True(viewModel.IsDirty);
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

        GetSetting(viewModel, DriverSettingKey.HttpPort).Value = "70000";

        Assert.False(GetSetting(viewModel, DriverSettingKey.HttpPort).IsValid);
        Assert.Equal("HTTP Port must be between 1 and 65535.", GetSetting(viewModel, DriverSettingKey.HttpPort).ErrorMessage);
        Assert.False(viewModel.IsFormValid);
    }

    [Fact]
    public void SdkPort_MustBeInRange()
    {
        var viewModel = CreateEditableViewModel(new FakeCameraRepository());

        GetSetting(viewModel, DriverSettingKey.SdkPort).Value = "-1";

        Assert.False(GetSetting(viewModel, DriverSettingKey.SdkPort).IsValid);
        Assert.Equal("SDK Port must be between 1 and 65535.", GetSetting(viewModel, DriverSettingKey.SdkPort).ErrorMessage);
        Assert.False(viewModel.IsFormValid);
    }

    [Fact]
    public void RtspPort_MustBeInRange()
    {
        var camera = CreateCamera();
        camera.ConnectionType = DeviceConnectionType.RTSP;
        var viewModel = new CameraDetailViewModel(camera, new FakeCameraRepository());
        viewModel.EditCommand.Execute(null);

        GetSetting(viewModel, DriverSettingKey.RtspPort).Value = "0";

        Assert.False(GetSetting(viewModel, DriverSettingKey.RtspPort).IsValid);
        Assert.Equal("RTSP Port must be between 1 and 65535.", GetSetting(viewModel, DriverSettingKey.RtspPort).ErrorMessage);
        Assert.False(viewModel.IsFormValid);
    }

    [Fact]
    public void RtspUrl_RequiresValue()
    {
        var camera = CreateCamera();
        camera.ConnectionType = DeviceConnectionType.RTSP;
        var viewModel = new CameraDetailViewModel(camera, new FakeCameraRepository());
        viewModel.EditCommand.Execute(null);

        GetSetting(viewModel, DriverSettingKey.RtspUrl).Value = string.Empty;

        Assert.False(GetSetting(viewModel, DriverSettingKey.RtspUrl).IsValid);
        Assert.False(viewModel.IsFormValid);
    }

    [Fact]
    public void ValidInput_KeepsFormValid()
    {
        var viewModel = CreateEditableViewModel(new FakeCameraRepository());

        viewModel.Name = "Lobby";
        viewModel.IpAddress = "10.0.0.50";
        GetSetting(viewModel, DriverSettingKey.HttpPort).Value = "80";
        GetSetting(viewModel, DriverSettingKey.SdkPort).Value = "8000";

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
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public void SaveCommand_PersistsEditedDriverSettingOntoCamera()
    {
        var repository = new FakeCameraRepository();
        var viewModel = CreateEditableViewModel(repository);

        GetSetting(viewModel, DriverSettingKey.Username).Value = "new-user";
        viewModel.SaveCommand.Execute(null);

        Assert.Equal("new-user", repository.LastUpdatedCamera!.Username);
    }

    [Fact]
    public void SaveCommand_DoesNotOverwriteCameraFieldsOutsideTheActiveDriverDefinition()
    {
        var repository = new FakeCameraRepository();
        var camera = CreateCamera();
        camera.RtspPort = 9999;
        var viewModel = new CameraDetailViewModel(camera, repository);
        viewModel.EditCommand.Execute(null);

        viewModel.SaveCommand.Execute(null);

        Assert.Equal(9999, repository.LastUpdatedCamera!.RtspPort);
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
        Assert.False(viewModel.IsDirty);
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
    public void DeleteCommand_RequestsConfirmation()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository());
        var confirmationRequested = false;

        viewModel.RequestDeleteConfirmation += () => confirmationRequested = true;

        viewModel.DeleteCommand.Execute(null);

        Assert.True(confirmationRequested);
    }

    [Fact]
    public void DeleteCommand_WithUnsavedChanges_StillRequestsDeleteConfirmation()
    {
        var viewModel = CreateEditableViewModel(new FakeCameraRepository());
        var deleteConfirmationRequested = false;
        var unsavedConfirmationRequested = false;

        viewModel.Name = "Lobby";
        viewModel.RequestDeleteConfirmation += () => deleteConfirmationRequested = true;
        viewModel.RequestUnsavedChangesConfirmation += () => unsavedConfirmationRequested = true;

        viewModel.DeleteCommand.Execute(null);

        Assert.True(deleteConfirmationRequested);
        Assert.False(unsavedConfirmationRequested);
    }

    [Fact]
    public void HandleDeleteConfirmationDecision_Confirm_DeletesAndCloses()
    {
        var repository = new FakeCameraRepository();
        var viewModel = CreateViewModel(repository);
        var closeRequested = false;

        viewModel.RequestClose += () => closeRequested = true;

        viewModel.HandleDeleteConfirmationDecision(DeleteConfirmationDecision.Confirm);

        Assert.True(closeRequested);
        Assert.True(viewModel.WasDeleted);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), viewModel.DeletedCameraId);
        Assert.Equal(1, repository.DeleteCallCount);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), repository.LastDeletedId);
        Assert.Equal("Camera deleted successfully.", viewModel.StatusMessage);
    }

    [Fact]
    public void HandleDeleteConfirmationDecision_Cancel_DoesNotDelete()
    {
        var repository = new FakeCameraRepository();
        var viewModel = CreateViewModel(repository);
        var closeRequested = false;

        viewModel.RequestClose += () => closeRequested = true;

        viewModel.HandleDeleteConfirmationDecision(DeleteConfirmationDecision.Cancel);

        Assert.False(closeRequested);
        Assert.False(viewModel.WasDeleted);
        Assert.Null(viewModel.DeletedCameraId);
        Assert.Equal(0, repository.DeleteCallCount);
    }

    [Fact]
    public void HandleDeleteConfirmationDecision_DeleteFails_KeepsWindowOpenAndValues()
    {
        var repository = new ThrowingCameraRepository();
        var viewModel = CreateEditableViewModel(repository);
        var closeRequested = false;

        viewModel.Name = "Edited Name";
        viewModel.RequestClose += () => closeRequested = true;

        viewModel.HandleDeleteConfirmationDecision(DeleteConfirmationDecision.Confirm);

        Assert.False(closeRequested);
        Assert.False(viewModel.WasDeleted);
        Assert.Null(viewModel.DeletedCameraId);
        Assert.Equal("Edited Name", viewModel.Name);
        Assert.Equal("Failed to delete camera: Repository failure.", viewModel.StatusMessage);
    }

    [Fact]
    public void CloseCommand_WithoutChanges_RaisesRequestClose()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository());
        var wasRaised = false;

        viewModel.RequestClose += () => wasRaised = true;

        viewModel.CloseCommand.Execute(null);

        Assert.True(wasRaised);
    }

    [Fact]
    public void CloseCommand_WithChanges_RequestsConfirmation()
    {
        var viewModel = CreateEditableViewModel(new FakeCameraRepository());
        var confirmationRequested = false;
        var closeRequested = false;

        viewModel.Name = "Lobby";
        viewModel.RequestUnsavedChangesConfirmation += () => confirmationRequested = true;
        viewModel.RequestClose += () => closeRequested = true;

        viewModel.CloseCommand.Execute(null);

        Assert.True(confirmationRequested);
        Assert.False(closeRequested);
    }

    [Fact]
    public void HandleUnsavedChangesDecision_Save_ClosesAfterSuccessfulSave()
    {
        var repository = new FakeCameraRepository();
        var viewModel = CreateEditableViewModel(repository);
        var closeRequested = false;

        viewModel.Name = "Lobby";
        viewModel.RequestClose += () => closeRequested = true;

        viewModel.HandleUnsavedChangesDecision(UnsavedChangesDecision.Save);

        Assert.True(closeRequested);
        Assert.Equal(1, repository.UpdateCallCount);
        Assert.False(viewModel.IsDirty);
    }

    [Fact]
    public void HandleUnsavedChangesDecision_Save_DoesNotCloseWhenSaveFails()
    {
        var repository = new ThrowingCameraRepository();
        var viewModel = CreateEditableViewModel(repository);
        var closeRequested = false;

        viewModel.Name = "Lobby";
        viewModel.RequestClose += () => closeRequested = true;

        viewModel.HandleUnsavedChangesDecision(UnsavedChangesDecision.Save);

        Assert.False(closeRequested);
        Assert.True(viewModel.IsDirty);
        Assert.Equal("Failed to save camera: Repository failure.", viewModel.StatusMessage);
    }

    [Fact]
    public void HandleUnsavedChangesDecision_Discard_ClosesWithoutSaving()
    {
        var repository = new FakeCameraRepository();
        var viewModel = CreateEditableViewModel(repository);
        var closeRequested = false;

        viewModel.Name = "Lobby";
        viewModel.IpAddress = "bad-ip";
        viewModel.RequestClose += () => closeRequested = true;

        viewModel.HandleUnsavedChangesDecision(UnsavedChangesDecision.Discard);

        Assert.True(closeRequested);
        Assert.Equal(0, repository.UpdateCallCount);
    }

    [Fact]
    public void HandleUnsavedChangesDecision_Cancel_StaysOpenAndKeepsChanges()
    {
        var viewModel = CreateEditableViewModel(new FakeCameraRepository());
        var closeRequested = false;

        viewModel.Name = "Lobby";
        viewModel.RequestClose += () => closeRequested = true;

        viewModel.HandleUnsavedChangesDecision(UnsavedChangesDecision.Cancel);

        Assert.False(closeRequested);
        Assert.True(viewModel.IsDirty);
        Assert.Equal("Lobby", viewModel.Name);
    }

    [Fact]
    public void Constructor_HandlesEmptyStringsWithoutThrowing()
    {
        var camera = new EntityCamera
        {
            ConnectionType = DeviceConnectionType.RTSP,
            CreateTime = new DateTime(2026, 7, 1, 8, 30, 0),
            LastModifyTime = new DateTime(2026, 7, 2, 9, 45, 0)
        };

        var viewModel = new CameraDetailViewModel(camera, new FakeCameraRepository());

        Assert.Equal(string.Empty, viewModel.Name);
        Assert.Equal(string.Empty, viewModel.Model);
        Assert.Equal(string.Empty, GetSetting(viewModel, DriverSettingKey.RtspUrl).Value);
        Assert.Equal("Offline", viewModel.Status);
        Assert.Equal("No", viewModel.Recording);
    }

    [Fact]
    public void TestConnectionCommand_NotAvailableInNewMode()
    {
        var viewModel = new CameraDetailViewModel(new FakeCameraRepository());

        Assert.False(viewModel.TestConnectionCommand.CanExecute(null));
    }

    [Fact]
    public void TestConnectionCommand_ValidationBlocksTest()
    {
        var viewModel = CreateEditableViewModel(new FakeCameraRepository());
        GetSetting(viewModel, DriverSettingKey.HttpPort).Value = "not-a-port";

        viewModel.TestConnectionCommand.Execute(null);

        Assert.Equal("Please fix validation errors before testing the connection.", viewModel.StatusMessage);
    }

    [Fact]
    public void TestConnectionCommand_UnimplementedDriver_ReportsNotImplemented()
    {
        var camera = CreateCamera();
        camera.ConnectionType = DeviceConnectionType.HikvisionISAPI;
        var viewModel = new CameraDetailViewModel(camera, new FakeCameraRepository());

        viewModel.TestConnectionCommand.Execute(null);

        Assert.Equal("Driver not implemented.", viewModel.StatusMessage);
    }

    [Fact]
    public void TestConnectionCommand_RtspFailure_ReportsConnectionFailed()
    {
        var camera = CreateCamera();
        camera.ConnectionType = DeviceConnectionType.RTSP;
        var viewModel = new CameraDetailViewModel(camera, new FakeCameraRepository());
        viewModel.EditCommand.Execute(null);
        GetSetting(viewModel, DriverSettingKey.RtspUrl).Value = "http://192.168.10.15/wrong-scheme";

        viewModel.TestConnectionCommand.Execute(null);

        Assert.Equal("Connection failed.", viewModel.StatusMessage);
    }

    [Fact]
    public void TestConnectionCommand_RtspSuccess_ReportsConnectionSuccessful()
    {
        using var server = new LoopbackRtspTestServer("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n");
        var camera = CreateCamera();
        camera.ConnectionType = DeviceConnectionType.RTSP;
        var viewModel = new CameraDetailViewModel(camera, new FakeCameraRepository());
        viewModel.EditCommand.Execute(null);
        GetSetting(viewModel, DriverSettingKey.RtspUrl).Value = $"rtsp://127.0.0.1:{server.Port}/stream";

        viewModel.TestConnectionCommand.Execute(null);

        Assert.Equal("Connection successful.", viewModel.StatusMessage);
    }

    [Fact]
    public void TestConnectionCommand_UsesCurrentFormValues_NotStaleSavedRtspUrl()
    {
        using var server = new LoopbackRtspTestServer("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n");
        var camera = CreateCamera();
        camera.ConnectionType = DeviceConnectionType.RTSP;
        camera.RtspUrl = "http://192.168.10.15/wrong-scheme";
        var viewModel = new CameraDetailViewModel(camera, new FakeCameraRepository());

        viewModel.TestConnectionCommand.Execute(null);
        var resultBeforeEdit = viewModel.StatusMessage;

        viewModel.EditCommand.Execute(null);
        GetSetting(viewModel, DriverSettingKey.RtspUrl).Value = $"rtsp://127.0.0.1:{server.Port}/stream";
        viewModel.TestConnectionCommand.Execute(null);

        Assert.Equal("Connection failed.", resultBeforeEdit);
        Assert.Equal("Connection successful.", viewModel.StatusMessage);
    }

    // A: legacy record, RtspPort never touched this session -> the explicit port already
    // embedded in RtspUrl is preserved, and RtspPort is synced up to match it on Save.
    [Fact]
    public void SaveCommand_RtspPortNotEdited_PreservesLegacyUrlPortAndSyncsRtspPort()
    {
        var repository = new FakeCameraRepository();
        var camera = CreateCamera();
        camera.ConnectionType = DeviceConnectionType.RTSP;
        camera.RtspUrl = "rtsp://host:8554/stream";
        camera.RtspPort = 554;
        var viewModel = new CameraDetailViewModel(camera, repository);
        viewModel.EditCommand.Execute(null);

        viewModel.SaveCommand.Execute(null);

        Assert.Equal(8554, repository.LastUpdatedCamera!.RtspPort);
        Assert.Equal("rtsp://host:8554/stream", repository.LastUpdatedCamera.RtspUrl);
    }

    // B: user explicitly re-enters the RTSP Port field with the value it already displays
    // (554 -> 554). The setter must mark explicit intent purely from being invoked, independent
    // of SetProperty's equality check -- so this must NOT collapse to "not edited" and defer to
    // the legacy URL's :8554.
    [Fact]
    public void SaveCommand_RtspPortExplicitlyEditedToSameValue_BecomesAuthoritativeAndNormalizesUrl()
    {
        var repository = new FakeCameraRepository();
        var camera = CreateCamera();
        camera.ConnectionType = DeviceConnectionType.RTSP;
        camera.RtspUrl = "rtsp://host:8554/stream";
        camera.RtspPort = 554;
        var viewModel = new CameraDetailViewModel(camera, repository);
        viewModel.EditCommand.Execute(null);

        var rtspPortSetting = GetSetting(viewModel, DriverSettingKey.RtspPort);
        rtspPortSetting.Value = "554";

        Assert.True(rtspPortSetting.WasExplicitlyEdited);

        viewModel.SaveCommand.Execute(null);

        Assert.Equal(554, repository.LastUpdatedCamera!.RtspPort);
        Assert.Equal("rtsp://host:554/stream", repository.LastUpdatedCamera.RtspUrl);
    }

    // C: user explicitly edits RTSP Port to a non-default value -> authoritative, URL normalized.
    [Fact]
    public void SaveCommand_RtspPortExplicitlyEditedToNonDefault_BecomesAuthoritativeAndNormalizesUrl()
    {
        var repository = new FakeCameraRepository();
        var camera = CreateCamera();
        camera.ConnectionType = DeviceConnectionType.RTSP;
        camera.RtspUrl = "rtsp://host:554/stream";
        camera.RtspPort = 554;
        var viewModel = new CameraDetailViewModel(camera, repository);
        viewModel.EditCommand.Execute(null);

        GetSetting(viewModel, DriverSettingKey.RtspPort).Value = "8554";
        viewModel.SaveCommand.Execute(null);

        Assert.Equal(8554, repository.LastUpdatedCamera!.RtspPort);
        Assert.Equal("rtsp://host:8554/stream", repository.LastUpdatedCamera.RtspUrl);
    }

    // D: new camera, RtspPort set once -> authoritative without requiring the same port to also
    // be typed into the RTSP URL.
    [Fact]
    public void SaveCommand_NewCamera_ExplicitRtspPort_DoesNotRequireEnteringPortTwice()
    {
        var repository = new FakeCameraRepository();
        var viewModel = new CameraDetailViewModel(repository);
        viewModel.Name = "Lobby";
        viewModel.IpAddress = "10.0.0.50";
        viewModel.ConnectionType = nameof(DeviceConnectionType.RTSP);

        GetSetting(viewModel, DriverSettingKey.RtspUrl).Value = "rtsp://192.168.1.50/stream";
        GetSetting(viewModel, DriverSettingKey.RtspPort).Value = "8554";

        viewModel.SaveCommand.Execute(null);

        Assert.Equal(8554, repository.LastAddedCamera!.RtspPort);
        Assert.Equal("rtsp://192.168.1.50:8554/stream", repository.LastAddedCamera.RtspUrl);
    }

    // E: programmatic initialization/reseeding must never mark explicit edit intent.
    [Fact]
    public void DriverSettingEditorViewModel_Construction_DoesNotMarkExplicitlyEdited()
    {
        var definition = new DriverSettingDefinition(DriverSettingKey.RtspPort, "RTSP Port", valueKind: DriverSettingValueKind.Port);

        var setting = new DriverSettingEditorViewModel(definition, "554");

        Assert.False(setting.WasExplicitlyEdited);
    }

    [Fact]
    public void SelectingConnectionType_SeededRtspPort_IsNotMarkedExplicitlyEdited()
    {
        var viewModel = new CameraDetailViewModel(new FakeCameraRepository());

        viewModel.ConnectionType = nameof(DeviceConnectionType.RTSP);

        Assert.False(GetSetting(viewModel, DriverSettingKey.RtspPort).WasExplicitlyEdited);
    }

    [Fact]
    public void ChangingConnectionType_RebuiltSetting_PreservesValueAndExplicitEditFlag()
    {
        // Credential mutation depends on edit intent. A connection-type rebuild must preserve
        // both the typed value and the fact that the user typed it.
        var camera = CreateCamera();
        camera.ConnectionType = DeviceConnectionType.RTSP;
        var viewModel = new CameraDetailViewModel(camera, new FakeCameraRepository());
        viewModel.EditCommand.Execute(null);
        GetSetting(viewModel, DriverSettingKey.Username).Value = "edited-user";

        viewModel.ConnectionType = nameof(DeviceConnectionType.ONVIF);
        viewModel.ConnectionType = nameof(DeviceConnectionType.RTSP);

        Assert.Equal("edited-user", GetSetting(viewModel, DriverSettingKey.Username).Value);
        Assert.True(GetSetting(viewModel, DriverSettingKey.Username).WasExplicitlyEdited);
    }

    private static DriverSettingEditorViewModel GetSetting(CameraDetailViewModel viewModel, DriverSettingKey key)
    {
        return viewModel.DriverSettings.Single(setting => setting.Key == key);
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
            ConnectionType = DeviceConnectionType.HikvisionISAPI,
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
        public int DeleteCallCount { get; private set; }
        public int UpdateCallCount { get; private set; }
        public EntityCamera? LastAddedCamera { get; private set; }
        public Guid? LastDeletedId { get; private set; }
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
            DeleteCallCount++;
            LastDeletedId = id;
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
            throw new InvalidOperationException("Repository failure.");
        }
    }
}
