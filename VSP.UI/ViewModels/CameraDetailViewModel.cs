using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.MVVM;
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

public class CameraDetailViewModel : ObservableObject
{
    private readonly Camera _camera;
    private readonly ICameraRepository _cameraRepository;
    private readonly RelayCommand _saveCommand;
    private readonly bool _isNewMode;

    private string _savedSnapshot = string.Empty;
    private string _name;
    private string _brand;
    private string _model;
    private string _location;
    private string _ipAddress;
    private string _httpPort;
    private string _rtspPort;
    private string _sdkPort;
    private string _username;
    private string _password;
    private string _rtspUrl;
    private string _statusMessage = "Read-only mode.";
    private bool _isEditMode;

    private string _nameError = string.Empty;
    private bool _isNameValid = true;
    private string _ipAddressError = string.Empty;
    private bool _isIpAddressValid = true;
    private string _httpPortError = string.Empty;
    private bool _isHttpPortValid = true;
    private string _rtspPortError = string.Empty;
    private bool _isRtspPortValid = true;
    private string _sdkPortError = string.Empty;
    private bool _isSdkPortValid = true;
    private string _lastModifyTime;
    private string _status;
    private string _recording;
    private string _createTime;

    public string Title => IsNewMode ? "Add Camera" : "Camera Detail";
    public string HeaderTitle => Title;
    public IReadOnlyList<string> BrandOptions { get; } = Enum.GetNames<CameraBrand>();

    public string LastModifyTime
    {
        get => _lastModifyTime;
        private set => SetProperty(ref _lastModifyTime, value);
    }

    public ICommand CloseCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SaveCommand => _saveCommand;

    public event Action? RequestClose;
    public event Action? RequestUnsavedChangesConfirmation;

    public CameraDetailViewModel(Camera camera, ICameraRepository cameraRepository)
        : this(camera, cameraRepository, false)
    {
    }

    public CameraDetailViewModel(ICameraRepository cameraRepository)
        : this(CreateNewCamera(), cameraRepository, true)
    {
    }

    private CameraDetailViewModel(Camera camera, ICameraRepository cameraRepository, bool isNewMode)
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
        _httpPort = camera.HttpPort.ToString();
        _rtspPort = camera.RtspPort.ToString();
        _sdkPort = camera.SdkPort.ToString();
        _username = camera.Username;
        _password = camera.Password;
        _rtspUrl = camera.RtspUrl;
        _status = camera.Status.ToString();
        _recording = camera.Recording ? "Yes" : "No";
        _createTime = camera.CreateTime.ToString("yyyy-MM-dd HH:mm:ss");
        _lastModifyTime = camera.LastModifyTime.ToString("yyyy-MM-dd HH:mm:ss");
        _statusMessage = isNewMode ? "Ready to add camera." : "Read-only mode.";

        CloseCommand = new RelayCommand(Close);
        EditCommand = new RelayCommand(EnterEditMode, () => !IsNewMode);
        _saveCommand = new RelayCommand(Save, () => IsEditMode);

        IsEditMode = isNewMode;
        _savedSnapshot = CreateSnapshot();
        ValidateAll();
    }

    public bool IsNewMode => _isNewMode;

    public bool WasSaved { get; private set; }

    public Guid? SavedCameraId { get; private set; }

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

    public string HttpPort
    {
        get => _httpPort;
        set
        {
            if (SetProperty(ref _httpPort, value))
            {
                ValidateAll();
                OnPropertyChanged(nameof(IsDirty));
            }
        }
    }

    public string RtspPort
    {
        get => _rtspPort;
        set
        {
            if (SetProperty(ref _rtspPort, value))
            {
                ValidateAll();
                OnPropertyChanged(nameof(IsDirty));
            }
        }
    }

    public string SdkPort
    {
        get => _sdkPort;
        set
        {
            if (SetProperty(ref _sdkPort, value))
            {
                ValidateAll();
                OnPropertyChanged(nameof(IsDirty));
            }
        }
    }

    public string Username
    {
        get => _username;
        set
        {
            if (SetProperty(ref _username, value))
            {
                OnPropertyChanged(nameof(IsDirty));
            }
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                OnPropertyChanged(nameof(MaskedPassword));
                OnPropertyChanged(nameof(IsDirty));
            }
        }
    }

    public string MaskedPassword => MaskPassword(Password);

    public string RtspUrl
    {
        get => _rtspUrl;
        set
        {
            if (SetProperty(ref _rtspUrl, value))
            {
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

    public string HttpPortError
    {
        get => _httpPortError;
        private set => SetProperty(ref _httpPortError, value);
    }

    public bool IsHttpPortValid
    {
        get => _isHttpPortValid;
        private set => SetProperty(ref _isHttpPortValid, value);
    }

    public string RtspPortError
    {
        get => _rtspPortError;
        private set => SetProperty(ref _rtspPortError, value);
    }

    public bool IsRtspPortValid
    {
        get => _isRtspPortValid;
        private set => SetProperty(ref _isRtspPortValid, value);
    }

    public string SdkPortError
    {
        get => _sdkPortError;
        private set => SetProperty(ref _sdkPortError, value);
    }

    public bool IsSdkPortValid
    {
        get => _isSdkPortValid;
        private set => SetProperty(ref _isSdkPortValid, value);
    }

    public bool IsFormValid =>
        IsNameValid &&
        IsIpAddressValid &&
        IsHttpPortValid &&
        IsRtspPortValid &&
        IsSdkPortValid;

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
        ValidatePort(NameofPort.HttpPort);
        ValidatePort(NameofPort.RtspPort);
        ValidatePort(NameofPort.SdkPort);
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

    private void ValidatePort(NameofPort portName)
    {
        var (value, setValid, setError, message) = portName switch
        {
            NameofPort.HttpPort => (HttpPort, (Action<bool>)(v => IsHttpPortValid = v), (Action<string>)(m => HttpPortError = m), "HTTP port must be between 1 and 65535."),
            NameofPort.RtspPort => (RtspPort, (Action<bool>)(v => IsRtspPortValid = v), (Action<string>)(m => RtspPortError = m), "RTSP port must be between 1 and 65535."),
            _ => (SdkPort, (Action<bool>)(v => IsSdkPortValid = v), (Action<string>)(m => SdkPortError = m), "SDK port must be between 1 and 65535.")
        };

        var isValid = int.TryParse(value, out var port) && port >= 1 && port <= 65535;
        setValid(isValid);
        setError(isValid ? string.Empty : message);
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
        return string.Join("|",
            NormalizeSnapshotValue(Name),
            NormalizeSnapshotValue(Brand),
            NormalizeSnapshotValue(Model),
            NormalizeSnapshotValue(Location),
            NormalizeSnapshotValue(IpAddress),
            NormalizeSnapshotValue(HttpPort),
            NormalizeSnapshotValue(RtspPort),
            NormalizeSnapshotValue(SdkPort),
            NormalizeSnapshotValue(Username),
            Password,
            NormalizeSnapshotValue(RtspUrl));
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
        camera.HttpPort = int.Parse(HttpPort);
        camera.RtspPort = int.Parse(RtspPort);
        camera.SdkPort = int.Parse(SdkPort);
        camera.Username = Username.Trim();
        camera.Password = Password;
        camera.RtspUrl = RtspUrl.Trim();
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

    private static string MaskPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return string.Empty;
        }

        return new string('*', password.Length);
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

    private enum NameofPort
    {
        HttpPort,
        RtspPort,
        SdkPort
    }
}
