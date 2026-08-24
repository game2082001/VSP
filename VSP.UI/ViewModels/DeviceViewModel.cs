using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.MVVM;
using VSP.Device.Services;
using VSP.Domain.Entities;
using VSP.Domain.Enums;
using VSP.UI.Views;

namespace VSP.UI.ViewModels;

public class DeviceViewModel : ObservableObject
{
    private readonly DeviceService _deviceService = new();

    public ObservableCollection<Camera> Devices { get; } = new();

    private Camera? _selectedDevice;

    public Camera? SelectedDevice
    {
        get => _selectedDevice;
        set => SetProperty(ref _selectedDevice, value);
    }

    public ICommand AddDeviceCommand { get; }
    public ICommand EditDeviceCommand { get; }
    public ICommand DeleteDeviceCommand { get; }
    public ICommand RefreshCommand { get; }

    public DeviceViewModel()
    {
        AddDeviceCommand = new RelayCommand(AddDevice);
        EditDeviceCommand = new RelayCommand(EditDevice);
        DeleteDeviceCommand = new RelayCommand(DeleteDevice);
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
    }

    private void AddDevice()
    {
        var window = new AddDeviceWindow();

        if (window.ShowDialog() == true)
        {
            var camera = new Camera
            {
                Name = window.DeviceName,
                IpAddress = window.IpAddress,
                Brand = ParseBrand(window.Brand),
                Model = window.Model,
                Location = window.Location,
                HttpPort = window.HttpPort,
                RtspPort = window.RtspPort,
                SdkPort = window.SdkPort,
                Username = window.Username,
                RtspUrl = window.RtspUrl,
                Status = CameraStatus.Offline
            };

            _deviceService.AddCamera(camera, CameraCredentialMutation.Replace(window.Password));

            LoadDevices();
        }
    }

    private void EditDevice()
    {
        if (SelectedDevice == null)
        {
            MessageBox.Show("請先選擇要編輯的設備。", "VSP");
            return;
        }

        var window = new AddDeviceWindow(SelectedDevice);

        if (window.ShowDialog() == true)
        {
            SelectedDevice.Name = window.DeviceName;
            SelectedDevice.IpAddress = window.IpAddress;
            SelectedDevice.Brand = ParseBrand(window.Brand);
            SelectedDevice.Model = window.Model;
            SelectedDevice.Location = window.Location;
            SelectedDevice.HttpPort = window.HttpPort;
            SelectedDevice.RtspPort = window.RtspPort;
            SelectedDevice.SdkPort = window.SdkPort;
            SelectedDevice.Username = window.Username;
            SelectedDevice.RtspUrl = window.RtspUrl;

            var mutation = string.IsNullOrEmpty(window.Password)
                ? CameraCredentialMutation.Unchanged()
                : CameraCredentialMutation.Replace(window.Password);
            _deviceService.UpdateCamera(SelectedDevice, mutation);

            LoadDevices();
        }
    }

    private void DeleteDevice()
    {
        if (SelectedDevice == null)
        {
            MessageBox.Show("請先選擇要刪除的設備。", "VSP");
            return;
        }

        var result = MessageBox.Show(
            $"確定要刪除設備「{SelectedDevice.Name}」嗎？",
            "VSP",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        _deviceService.DeleteCamera(SelectedDevice.Id);

        LoadDevices();
    }

    private CameraBrand ParseBrand(string brand)
    {
        return brand switch
        {
            "Hikvision" => CameraBrand.Hikvision,
            "Dahua" => CameraBrand.Dahua,
            "ONVIF" => CameraBrand.ONVIF,
            "RTSP" => CameraBrand.RTSP,
            _ => CameraBrand.Unknown
        };
    }
}
