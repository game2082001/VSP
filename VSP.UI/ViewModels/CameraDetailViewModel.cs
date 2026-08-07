using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.MVVM;
using VSP.Device.Drivers;
using VSP.Device.Drivers.Settings;
using VSP.Device.Interfaces;
using VSP.Domain.Entities;
using VSP.Domain.Enums;

namespace VSP.UI.ViewModels;

public enum UnsavedChangesDecision
{
    Save,
    Discard,
    Cancel
}

public enum DeleteConfirmationDecision
{
    Confirm,
    Cancel
}

public class CameraDetailViewModel : ObservableObject
{
    private static readonly DriverRegistry Drivers = DriverRegistry.CreateDefault();

    private readonly Camera _camera;
    private readonly ICameraRepository _cameraRepository;
    private readonly RelayCommand _saveCommand;
    private readonly bool _isNewMode;
    private bool _isRebuildingDriverSettings;

    private string _savedSnapshot = string.Empty;
    private string _name;
    private string _brand;
    private string _model;
    private string _location;
    private string _ipAddress;
    private string _connectionType;
    private string _statusMessage = "Read-only mode.";
    private bool _isEditMode;

    private string _nameError = string.Empty;
    private bool _isNameValid = true;
    private string _ipAddressError = string.Empty;
    private bool _isIpAddressValid = true;
    private string _lastModifyTime;
    private string _status;
    private string _recording;
    private string _createTime;

    public string Title => IsNewMode ? "Add Camera" : "Camera Detail";
    public string HeaderTitle => Title;
    public IReadOnlyList<string> BrandOptions { get; } = Enum.GetNames<CameraBrand>();
    public IReadOnlyList<string> ConnectionTypeOptions { get; } = Enum.GetNames<DeviceConnectionType>();

    public ObservableCollection<DriverSettingEditorViewModel> DriverSettings { get; } = new();

    public string LastModifyTime
    {
        get => _lastModifyTime;
        private set => SetProperty(ref _lastModifyTime, value);
    }

    public ICommand CloseCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SaveCommand => _saveCommand;
    public ICommand DeleteCommand { get; }
    public ICommand TestConnectionCommand { get; }

    public event Action? RequestClose;
    public event Action? RequestUnsavedChangesConfirmation;
    public event Action? RequestDeleteConfirmation;

    // Role defaults to Admin -- preserves every pre-Epic-018 call site/test's existing
    // full-featured behavior unchanged; the two real production call sites (CameraListView.xaml.cs)
    // always pass the authenticated user's actual role explicitly.
    public CameraDetailViewModel(Camera camera, ICameraRepository cameraRepository, Role role = Role.Admin)
        : this(camera, cameraRepository, false, role)
    {
    }

    public CameraDetailViewModel(ICameraRepository cameraRepository, Role role = Role.Admin)
        : this(CreateNewCamera(), cameraRepository, true, role)
    {
    }

    private CameraDetailViewModel(Camera camera, ICameraRepository cameraRepository, bool isNewMode, Role role)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(cameraRepository);

        _camera = camera;
        _cameraRepository = cameraRepository;
        _isNewMode = isNewMode;

        _name = camera.Name;
        _brand = camera.Brand.ToString();
        _model = camera.Model;
        _location = camera.Location;
        _ipAddress = camera.IpAddress;
        _connectionType = camera.ConnectionType.ToString();
        _status = camera.Status.ToString();
        _recording = camera.Recording ? "Yes" : "No";
        _createTime = camera.CreateTime.ToString("yyyy-MM-dd HH:mm:ss");
        _lastModifyTime = camera.LastModifyTime.ToString("yyyy-MM-dd HH:mm:ss");
        _statusMessage = isNewMode ? "Ready to add camera." : "Read-only mode.";

        // Epic-018 §4.5/§8.4: Operator gets a read-only Camera Detail -- Edit/Save/Delete are
        // Admin-only. TestConnectionCommand is deliberately left ungated: it inspects
        // connectivity without modifying any stored camera data, so it isn't "editing."
        CloseCommand = new RelayCommand(Close);
        EditCommand = new RelayCommand(EnterEditMode, () => !IsNewMode && role == Role.Admin);
        _saveCommand = new RelayCommand(Save, () => IsEditMode && role == Role.Admin);
        DeleteCommand = new RelayCommand(Delete, () => !IsNewMode && role == Role.Admin);
        TestConnectionCommand = new RelayCommand(TestConnection, () => !IsNewMode);

        IsEditMode = isNewMode;
        RebuildDriverSettings(seedFromCamera: true);
        _savedSnapshot = CreateSnapshot();
        ValidateAll();
    }

    public bool IsNewMode => _isNewMode;

    public bool WasSaved { get; private set; }

    public Guid? SavedCameraId { get; private set; }

    public bool WasDeleted { get; private set; }

    public Guid? DeletedCameraId { get; private set; }

    public bool IsDirty => !CreateSnapshot().Equals(_savedSnapshot, StringComparison.Ordinal);

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                ValidateAll();
                OnPropertyChanged(nameof(IsDirty));
            }
        }
    }

    public string Brand
    {
        get => _brand;
        set
        {
            if (SetProperty(ref _brand, value))
            {
                OnPropertyChanged(nameof(IsDirty));
            }
        }
    }

    public string Model
    {
        get => _model;
        set
        {
            if (SetProperty(ref _model, value))
            {
                OnPropertyChanged(nameof(IsDirty));
            }
        }
    }

    public string Location
    {
        get => _location;
        set
        {
            if (SetProperty(ref _location, value))
            {
                OnPropertyChanged(nameof(IsDirty));
            }
        }
    }

    public string IpAddress
    {
        get => _ipAddress;
        set
        {
            if (SetProperty(ref _ipAddress, value))
            {
                ValidateAll();
                OnPropertyChanged(nameof(IsDirty));
            }
        }
    }

    public string ConnectionType
    {
        get => _connectionType;
        set
        {
            if (SetProperty(ref _connectionType, value))
            {
                RebuildDriverSettings(seedFromCamera: false);
                OnPropertyChanged(nameof(IsDirty));
            }
        }
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        private set
        {
            if (SetProperty(ref _isEditMode, value))
            {
                _saveCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public string NameError
    {
        get => _nameError;
        private set => SetProperty(ref _nameError, value);
    }

    public bool IsNameValid
    {
        get => _isNameValid;
        private set => SetProperty(ref _isNameValid, value);
    }

    public string IpAddressError
    {
        get => _ipAddressError;
        private set => SetProperty(ref _ipAddressError, value);
    }

    public bool IsIpAddressValid
    {
        get => _isIpAddressValid;
        private set => SetProperty(ref _isIpAddressValid, value);
    }

    public bool IsFormValid =>
        IsNameValid &&
        IsIpAddressValid &&
        DriverSettings.All(setting => setting.IsValid);

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public string Recording
    {
        get => _recording;
        private set => SetProperty(ref _recording, value);
    }

    public string CreateTime
    {
        get => _createTime;
        private set => SetProperty(ref _createTime, value);
    }

    private void EnterEditMode()
    {
        IsEditMode = true;
        StatusMessage = "Edit mode enabled.";
    }

    private void TestConnection()
    {
        ValidateAll();

        if (!IsFormValid)
        {
            StatusMessage = "Please fix validation errors before testing the connection.";
            return;
        }

        var testCamera = new Camera
        {
            ConnectionType = _camera.ConnectionType
        };
        MapToCamera(testCamera);

        var driver = DriverFactory.CreateCameraDriver(testCamera.ConnectionType);
        var isSuccess = driver.TestConnection(testCamera);

        if (isSuccess)
        {
            StatusMessage = "Connection successful.";
            return;
        }

        StatusMessage = DriverFactory.IsDriverImplemented(testCamera.ConnectionType)
            ? "Connection failed."
            : "Driver not implemented.";
    }

    private void Save()
    {
        Save(closeOnSuccess: IsNewMode);
    }

    private bool Save(bool closeOnSuccess)
    {
        ValidateAll();

        if (!IsFormValid)
        {
            StatusMessage = "Please fix validation errors before saving.";
            return false;
        }

        try
        {
            MapToCamera(_camera);

            if (IsNewMode)
            {
                _cameraRepository.Add(_camera);
                SyncDisplayFieldsFromCamera();
                UpdateSavedSnapshot();
                WasSaved = true;
                SavedCameraId = _camera.Id;
                StatusMessage = "Camera added successfully.";

                if (closeOnSuccess)
                {
                    RequestClose?.Invoke();
                }

                return true;
            }

            _cameraRepository.Update(_camera);
            SyncDisplayFieldsFromCamera();
            UpdateSavedSnapshot();
            StatusMessage = "Camera saved successfully.";

            if (closeOnSuccess)
            {
                RequestClose?.Invoke();
            }

            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = IsNewMode
                ? $"Failed to add camera: {ex.Message}"
                : $"Failed to save camera: {ex.Message}";
            return false;
        }
    }

    private void ValidateAll()
    {
        ValidateName();
        ValidateIpAddress();
        OnPropertyChanged(nameof(IsFormValid));
    }

    private void ValidateName()
    {
        var isValid = !string.IsNullOrWhiteSpace(Name);
        IsNameValid = isValid;
        NameError = isValid ? string.Empty : "Name is required.";
    }

    private void ValidateIpAddress()
    {
        var isValid = IsValidIPv4(IpAddress);
        IsIpAddressValid = isValid;
        IpAddressError = isValid ? string.Empty : "Invalid IP address.";
    }

    private void Close()
    {
        if (!IsDirty)
        {
            RequestClose?.Invoke();
            return;
        }

        RequestUnsavedChangesConfirmation?.Invoke();
    }

    private void Delete()
    {
        if (IsNewMode)
        {
            return;
        }

        RequestDeleteConfirmation?.Invoke();
    }

    public void HandleDeleteConfirmationDecision(DeleteConfirmationDecision decision)
    {
        if (decision == DeleteConfirmationDecision.Cancel)
        {
            return;
        }

        try
        {
            _cameraRepository.Delete(_camera.Id);
            WasDeleted = true;
            DeletedCameraId = _camera.Id;
            StatusMessage = "Camera deleted successfully.";
            RequestClose?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to delete camera: {ex.Message}";
        }
    }

    public void HandleUnsavedChangesDecision(UnsavedChangesDecision decision)
    {
        switch (decision)
        {
            case UnsavedChangesDecision.Save:
                Save(closeOnSuccess: true);
                break;
            case UnsavedChangesDecision.Discard:
                RequestClose?.Invoke();
                break;
            case UnsavedChangesDecision.Cancel:
                break;
        }
    }

    private void UpdateSavedSnapshot()
    {
        _savedSnapshot = CreateSnapshot();
        OnPropertyChanged(nameof(IsDirty));
    }

    private string CreateSnapshot()
    {
        var settingsSnapshot = string.Join(
            ";",
            DriverSettings.Select(setting => $"{setting.Key}={setting.Value}"));

        return string.Join("|",
            NormalizeSnapshotValue(Name),
            NormalizeSnapshotValue(Brand),
            NormalizeSnapshotValue(Model),
            NormalizeSnapshotValue(Location),
            NormalizeSnapshotValue(IpAddress),
            NormalizeSnapshotValue(ConnectionType),
            settingsSnapshot);
    }

    private static string NormalizeSnapshotValue(string value)
    {
        return value.Trim();
    }

    private void MapToCamera(Camera camera)
    {
        camera.Name = Name.Trim();
        camera.Brand = Enum.TryParse<CameraBrand>(Brand, out var brand)
            ? brand
            : CameraBrand.Unknown;
        camera.Model = Model.Trim();
        camera.Location = Location.Trim();
        camera.IpAddress = IpAddress.Trim();
        camera.ConnectionType = Enum.TryParse<DeviceConnectionType>(ConnectionType, out var connectionType)
            ? connectionType
            : DeviceConnectionType.Unknown;

        foreach (var setting in DriverSettings)
        {
            ApplyCameraSettingValue(camera, setting.Key, setting.Value);
        }
    }

    /// <summary>
    /// Rebuilds <see cref="DriverSettings"/> from the selected <see cref="ConnectionType"/>'s
    /// <see cref="DriverSettingsDefinition"/> — the only place Camera Detail knows which
    /// fields exist for the current driver. Values are preserved across a rebuild where the
    /// same <see cref="DriverSettingKey"/> exists in both the old and new definition, so
    /// switching driver type mid-edit does not silently discard already-typed values.
    /// </summary>
    private void RebuildDriverSettings(bool seedFromCamera)
    {
        _isRebuildingDriverSettings = true;
        try
        {
            var previousValues = DriverSettings.ToDictionary(setting => setting.Key, setting => setting.Value);

            foreach (var setting in DriverSettings)
            {
                setting.PropertyChanged -= HandleDriverSettingPropertyChanged;
            }

            DriverSettings.Clear();

            var connectionType = Enum.TryParse<DeviceConnectionType>(ConnectionType, out var parsedConnectionType)
                ? parsedConnectionType
                : DeviceConnectionType.Unknown;
            var definitions = Drivers.GetByConnectionType(connectionType)?.SettingsDefinition?.Settings
                ?? Array.Empty<DriverSettingDefinition>();

            foreach (var definition in definitions)
            {
                var initialValue = previousValues.TryGetValue(definition.Key, out var preservedValue)
                    ? preservedValue
                    : seedFromCamera
                        ? GetCameraSettingValue(_camera, definition.Key)
                        : definition.DefaultValue?.ToString() ?? string.Empty;

                var settingViewModel = new DriverSettingEditorViewModel(definition, initialValue);
                settingViewModel.PropertyChanged += HandleDriverSettingPropertyChanged;
                DriverSettings.Add(settingViewModel);
            }
        }
        finally
        {
            _isRebuildingDriverSettings = false;
        }

        OnPropertyChanged(nameof(IsFormValid));
    }

    private void HandleDriverSettingPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isRebuildingDriverSettings)
        {
            return;
        }

        if (e.PropertyName == nameof(DriverSettingEditorViewModel.IsValid))
        {
            OnPropertyChanged(nameof(IsFormValid));
        }

        if (e.PropertyName == nameof(DriverSettingEditorViewModel.Value))
        {
            OnPropertyChanged(nameof(IsDirty));
        }
    }

    private static string GetCameraSettingValue(Camera camera, DriverSettingKey key)
    {
        return key switch
        {
            DriverSettingKey.HttpPort => camera.HttpPort.ToString(),
            DriverSettingKey.RtspPort => camera.RtspPort.ToString(),
            DriverSettingKey.SdkPort => camera.SdkPort.ToString(),
            DriverSettingKey.Username => camera.Username,
            DriverSettingKey.Password => camera.Password,
            DriverSettingKey.RtspUrl => camera.RtspUrl,
            _ => string.Empty
        };
    }

    private static void ApplyCameraSettingValue(Camera camera, DriverSettingKey key, string value)
    {
        switch (key)
        {
            case DriverSettingKey.HttpPort:
                camera.HttpPort = ParsePortOrDefault(value, camera.HttpPort);
                break;
            case DriverSettingKey.RtspPort:
                camera.RtspPort = ParsePortOrDefault(value, camera.RtspPort);
                break;
            case DriverSettingKey.SdkPort:
                camera.SdkPort = ParsePortOrDefault(value, camera.SdkPort);
                break;
            case DriverSettingKey.Username:
                camera.Username = value.Trim();
                break;
            case DriverSettingKey.Password:
                camera.Password = value;
                break;
            case DriverSettingKey.RtspUrl:
                camera.RtspUrl = value.Trim();
                break;
        }
    }

    private static int ParsePortOrDefault(string value, int fallback)
    {
        return int.TryParse(value, out var port) ? port : fallback;
    }

    private void SyncDisplayFieldsFromCamera()
    {
        Brand = _camera.Brand.ToString();
        Status = _camera.Status.ToString();
        Recording = _camera.Recording ? "Yes" : "No";
        CreateTime = _camera.CreateTime.ToString("yyyy-MM-dd HH:mm:ss");
        LastModifyTime = _camera.LastModifyTime.ToString("yyyy-MM-dd HH:mm:ss");
    }

    private static Camera CreateNewCamera()
    {
        var now = DateTime.Now;

        return new Camera
        {
            Id = Guid.NewGuid(),
            Name = string.Empty,
            IpAddress = string.Empty,
            Brand = CameraBrand.Unknown,
            ConnectionType = DeviceConnectionType.Unknown,
            Model = string.Empty,
            Location = string.Empty,
            HttpPort = 80,
            RtspPort = 554,
            SdkPort = 8000,
            Username = string.Empty,
            Password = string.Empty,
            RtspUrl = string.Empty,
            Status = CameraStatus.Offline,
            Recording = false,
            CreateTime = now,
            LastModifyTime = now
        };
    }

    private static bool IsValidIPv4(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return false;
        }

        var parts = ipAddress.Split('.');
        if (parts.Length != 4)
        {
            return false;
        }

        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var value) || value < 0 || value > 255)
            {
                return false;
            }
        }

        return true;
    }
}
