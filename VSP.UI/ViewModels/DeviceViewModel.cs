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
                Username = window.Username,
                Password = window.Password,
                RtspUrl = window.RtspUrl,
                Status = CameraStatus.Offline
            };

            _deviceService.AddCamera(camera);

            LoadDevices();
        }
    }

    private void EditDevice()
    {
        MessageBox.Show("編輯設備功能下一個 Sprint 製作。", "VSP");
    }

    private void DeleteDevice()
    {
        MessageBox.Show("刪除設備功能下一個 Sprint 製作。", "VSP");
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