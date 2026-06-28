using System.Collections.ObjectModel;
using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.MVVM;
using VSP.Device.Services;
using VSP.Domain.Entities;

namespace VSP.UI.ViewModels;

public class DeviceCenterViewModel : ObservableObject
{
    private readonly DeviceService _deviceService = new();

    public ObservableCollection<Camera> Devices { get; } = new();

    private Camera? _selectedDevice;
    public Camera? SelectedDevice
    {
        get => _selectedDevice;
        set => SetProperty(ref _selectedDevice, value);
    }

    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public int DeviceCount => Devices.Count;

    public ICommand RefreshCommand { get; }

    public DeviceCenterViewModel()
    {
        RefreshCommand = new RelayCommand(LoadDevices);
        LoadDevices();
    }

    private void LoadDevices()
    {
        Devices.Clear();

        foreach (var camera in _deviceService.GetAllCameras())
        {
            Devices.Add(camera);
        }

        SelectedDevice = Devices.Count > 0 ? Devices[0] : null;
        StatusMessage = Devices.Count == 0
            ? "No cameras found."
            : $"Loaded {Devices.Count} camera(s).";

        OnPropertyChanged(nameof(DeviceCount));
    }
}
