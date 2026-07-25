using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.MVVM;
using VSP.Device.Interfaces;
using VSP.Domain.Entities;
using VSP.Domain.Enums;

namespace VSP.UI.ViewModels;

public class BatchEditViewModel : ObservableObject
{
    private readonly IReadOnlyList<Camera> _cameras;
    private readonly ICameraRepository _cameraRepository;

    private bool _applyBrand;
    private string _brand = nameof(CameraBrand.Unknown);
    private bool _applyLocation;
    private string _location = string.Empty;
    private bool _applyUsername;
    private string _username = string.Empty;
    private bool _applyPassword;
    private string _password = string.Empty;
    private string _statusMessage = "Select at least one field to apply.";

    public int SelectedCount => _cameras.Count;

    public IReadOnlyList<string> BrandOptions { get; } = Enum.GetNames<CameraBrand>();

    public bool ApplyBrand
    {
        get => _applyBrand;
        set => SetProperty(ref _applyBrand, value);
    }

    public string Brand
    {
        get => _brand;
        set => SetProperty(ref _brand, value);
    }

    public bool ApplyLocation
    {
        get => _applyLocation;
        set => SetProperty(ref _applyLocation, value);
    }

    public string Location
    {
        get => _location;
        set => SetProperty(ref _location, value);
    }

    public bool ApplyUsername
    {
        get => _applyUsername;
        set => SetProperty(ref _applyUsername, value);
    }

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public bool ApplyPassword
    {
        get => _applyPassword;
        set => SetProperty(ref _applyPassword, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool WasApplied { get; private set; }

    public int SuccessCount { get; private set; }

    public int FailureCount { get; private set; }

    public ICommand ApplyCommand { get; }

    public ICommand CancelCommand { get; }

    public event Action? RequestClose;

    public BatchEditViewModel(IReadOnlyList<Camera> cameras, ICameraRepository cameraRepository)
    {
        ArgumentNullException.ThrowIfNull(cameras);
        ArgumentNullException.ThrowIfNull(cameraRepository);

        if (cameras.Count == 0)
        {
            throw new ArgumentException("At least one camera must be provided.", nameof(cameras));
        }

        _cameras = cameras;
        _cameraRepository = cameraRepository;

        ApplyCommand = new RelayCommand(Apply);
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke());
    }

    private void Apply()
    {
        if (!ApplyBrand && !ApplyLocation && !ApplyUsername && !ApplyPassword)
        {
            StatusMessage = "Select at least one field to apply.";
            return;
        }

        SuccessCount = 0;
        FailureCount = 0;

        foreach (var camera in _cameras)
        {
            try
            {
                if (ApplyBrand && Enum.TryParse<CameraBrand>(Brand, out var brand))
                {
                    camera.Brand = brand;
                }

                if (ApplyLocation)
                {
                    camera.Location = Location.Trim();
                }

                if (ApplyUsername)
                {
                    camera.Username = Username.Trim();
                }

                if (ApplyPassword)
                {
                    camera.Password = Password;
                }

                camera.LastModifyTime = DateTime.Now;
                _cameraRepository.Update(camera);
                SuccessCount++;
            }
            catch
            {
                FailureCount++;
            }
        }

        WasApplied = true;
        StatusMessage = FailureCount == 0
            ? $"Updated {SuccessCount} camera(s)."
            : $"Updated {SuccessCount} camera(s), {FailureCount} failed.";

        RequestClose?.Invoke();
    }
}
