using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.MVVM;
using VSP.Domain.Entities;

namespace VSP.UI.ViewModels;

public class CameraDetailViewModel : ObservableObject
{
    private readonly RelayCommand _applyEditCommand;

    private string _name;
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

    public string Title => "Camera Detail";
    public string Brand { get; }
    public string Status { get; }
    public string Recording { get; }
    public string CreateTime { get; }
    public string LastModifyTime { get; }
    public ICommand CloseCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand ApplyEditCommand => _applyEditCommand;

    public event Action? RequestClose;

    public CameraDetailViewModel(Camera camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        _name = camera.Name;
        _model = camera.Model;
        _location = camera.Location;
        _ipAddress = camera.IpAddress;
        _httpPort = camera.HttpPort.ToString();
        _rtspPort = camera.RtspPort.ToString();
        _sdkPort = camera.SdkPort.ToString();
        _username = camera.Username;
        _password = camera.Password;
        _rtspUrl = camera.RtspUrl;

        Brand = camera.Brand.ToString();
        Status = camera.Status.ToString();
        Recording = camera.Recording ? "Yes" : "No";
        CreateTime = camera.CreateTime.ToString("yyyy-MM-dd HH:mm:ss");
        LastModifyTime = camera.LastModifyTime.ToString("yyyy-MM-dd HH:mm:ss");

        CloseCommand = new RelayCommand(Close);
        EditCommand = new RelayCommand(EnterEditMode);
        _applyEditCommand = new RelayCommand(ApplyEdit, () => IsEditMode);

        ValidateAll();
    }

    public string Name
    {
        get => _name;
        set
        {
            if (SetProperty(ref _name, value))
            {
                ValidateAll();
            }
        }
    }

    public string Model
    {
        get => _model;
        set => SetProperty(ref _model, value);
    }

    public string Location
    {
        get => _location;
        set => SetProperty(ref _location, value);
    }

    public string IpAddress
    {
        get => _ipAddress;
        set
        {
            if (SetProperty(ref _ipAddress, value))
            {
                ValidateAll();
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
            }
        }
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                OnPropertyChanged(nameof(MaskedPassword));
            }
        }
    }

    public string MaskedPassword => MaskPassword(Password);

    public string RtspUrl
    {
        get => _rtspUrl;
        set => SetProperty(ref _rtspUrl, value);
    }

    public bool IsEditMode
    {
        get => _isEditMode;
        private set
        {
            if (SetProperty(ref _isEditMode, value))
            {
                _applyEditCommand.RaiseCanExecuteChanged();
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

    private void EnterEditMode()
    {
        IsEditMode = true;
        StatusMessage = "Edit mode enabled. Changes are not saved to storage.";
    }

    private void ApplyEdit()
    {
        ValidateAll();
        StatusMessage = IsFormValid
            ? "Validation passed. Persistence is not implemented."
            : "Please fix validation errors before applying changes.";
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
        RequestClose?.Invoke();
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
