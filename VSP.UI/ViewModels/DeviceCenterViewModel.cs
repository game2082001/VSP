using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.MVVM;
using VSP.Device.Services;
using VSP.Domain.Entities;
using VSP.Domain.Enums;
using VSP.UI.Views;

namespace VSP.UI.ViewModels;

public class DeviceCenterViewModel : ObservableObject
{
    private const string AllOption = "All";

    private readonly DeviceService _deviceService = new();
    private readonly List<Camera> _allDevices = new();

    public ObservableCollection<Camera> Devices { get; } = new();

    public IReadOnlyList<string> BrandOptions { get; } =
        new[] { AllOption }
            .Concat(Enum.GetNames<CameraBrand>())
            .ToArray();

    public IReadOnlyList<string> ConnectionOptions { get; } =
        new[] { AllOption }
            .Concat(Enum.GetNames<DeviceConnectionType>())
            .ToArray();

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

    private string _searchKeyword = "";
    public string SearchKeyword
    {
        get => _searchKeyword;
        set
        {
            if (SetProperty(ref _searchKeyword, value))
            {
                ApplySearch();
            }
        }
    }

    private string _selectedBrand = AllOption;
    public string SelectedBrand
    {
        get => _selectedBrand;
        set
        {
            if (SetProperty(ref _selectedBrand, value))
            {
                ApplySearch();
            }
        }
    }

    private string _selectedConnection = AllOption;
    public string SelectedConnection
    {
        get => _selectedConnection;
        set
        {
            if (SetProperty(ref _selectedConnection, value))
            {
                ApplySearch();
            }
        }
    }

    public int DeviceCount => Devices.Count;

    public ICommand AddDeviceCommand { get; }
    public ICommand EditDeviceCommand { get; }
    public ICommand DeleteDeviceCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand ClearSearchCommand { get; }

    public DeviceCenterViewModel()
    {
        AddDeviceCommand = new RelayCommand(AddDevice);
        EditDeviceCommand = new RelayCommand(EditDevice);
        DeleteDeviceCommand = new RelayCommand(DeleteDevice);
        RefreshCommand = new RelayCommand(LoadDevices);
        SearchCommand = new RelayCommand(ApplySearch);
        ClearSearchCommand = new RelayCommand(ClearSearch);
        LoadDevices();
    }

    private void LoadDevices()
    {
        _allDevices.Clear();
        _allDevices.AddRange(_deviceService.GetAllCameras());
        ApplySearch();
    }

    private void ApplySearch()
    {
        var keyword = SearchKeyword.Trim();
        var currentSelectedId = SelectedDevice?.Id;

        IEnumerable<Camera> filteredDevices = _allDevices;

        if (!string.Equals(SelectedBrand, AllOption, StringComparison.OrdinalIgnoreCase))
        {
            filteredDevices = filteredDevices.Where(camera =>
                string.Equals(camera.Brand.ToString(), SelectedBrand, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.Equals(SelectedConnection, AllOption, StringComparison.OrdinalIgnoreCase))
        {
            filteredDevices = filteredDevices.Where(camera =>
                string.Equals(camera.ConnectionType.ToString(), SelectedConnection, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            filteredDevices = filteredDevices.Where(camera =>
                ContainsIgnoreCase(camera.Name, keyword)
                || ContainsIgnoreCase(camera.IpAddress, keyword)
                || ContainsIgnoreCase(camera.Brand.ToString(), keyword)
                || ContainsIgnoreCase(camera.Model, keyword));
        }

        Devices.Clear();

        foreach (var camera in filteredDevices)
        {
            Devices.Add(camera);
        }

        if (Devices.Count == 0)
        {
            SelectedDevice = null;
            StatusMessage = string.IsNullOrWhiteSpace(keyword)
                && string.Equals(SelectedBrand, AllOption, StringComparison.OrdinalIgnoreCase)
                && string.Equals(SelectedConnection, AllOption, StringComparison.OrdinalIgnoreCase)
                ? "No cameras found."
                : "No matching devices found.";
        }
        else
        {
            SelectedDevice = currentSelectedId.HasValue
                ? Devices.FirstOrDefault(x => x.Id == currentSelectedId.Value) ?? Devices[0]
                : Devices[0];

            StatusMessage = string.IsNullOrWhiteSpace(keyword)
                && string.Equals(SelectedBrand, AllOption, StringComparison.OrdinalIgnoreCase)
                && string.Equals(SelectedConnection, AllOption, StringComparison.OrdinalIgnoreCase)
                ? $"Loaded {Devices.Count} camera(s)."
                : $"Found {Devices.Count} matching device(s).";
        }

        OnPropertyChanged(nameof(DeviceCount));
    }

    private void ClearSearch()
    {
        SelectedBrand = AllOption;
        SelectedConnection = AllOption;
        SearchKeyword = string.Empty;
        ApplySearch();
    }

    private void AddDevice()
    {
        var window = new AddDeviceWindow();

        if (window.ShowDialog() != true)
        {
            return;
        }

        var camera = new Camera
        {
            Name = window.DeviceName,
            IpAddress = window.IpAddress,
            Brand = ParseBrand(window.Brand),
            ConnectionType = ParseConnectionType(window.ConnectionType),
            Model = window.Model,
            Location = window.Location,
            HttpPort = window.HttpPort,
            RtspPort = window.RtspPort,
            SdkPort = window.SdkPort,
            Username = window.Username,
            Password = window.Password,
            RtspUrl = window.RtspUrl,
            Status = CameraStatus.Offline
        };

        _deviceService.AddCamera(camera);
        LoadDevices();
        SelectedDevice = Devices.FirstOrDefault(x => x.Id == camera.Id) ?? SelectedDevice;
    }

    private void EditDevice()
    {
        if (SelectedDevice == null)
        {
            MessageBox.Show("Please select a device to edit.", "VSP");
            return;
        }

        var deviceId = SelectedDevice.Id;
        var window = new AddDeviceWindow(SelectedDevice);

        if (window.ShowDialog() != true)
        {
            return;
        }

        SelectedDevice.Name = window.DeviceName;
        SelectedDevice.IpAddress = window.IpAddress;
        SelectedDevice.Brand = ParseBrand(window.Brand);
        SelectedDevice.ConnectionType = ParseConnectionType(window.ConnectionType);
        SelectedDevice.Model = window.Model;
        SelectedDevice.Location = window.Location;
        SelectedDevice.HttpPort = window.HttpPort;
        SelectedDevice.RtspPort = window.RtspPort;
        SelectedDevice.SdkPort = window.SdkPort;
        SelectedDevice.Username = window.Username;
        SelectedDevice.Password = window.Password;
        SelectedDevice.RtspUrl = window.RtspUrl;

        _deviceService.UpdateCamera(SelectedDevice);
        LoadDevices();
        SelectedDevice = Devices.FirstOrDefault(x => x.Id == deviceId) ?? SelectedDevice;
    }

    private void DeleteDevice()
    {
        if (SelectedDevice == null)
        {
            MessageBox.Show("Please select a device.", "VSP");
            return;
        }

        var result = MessageBox.Show(
            "Are you sure you want to delete this device?",
            "Delete Device",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _deviceService.DeleteCamera(SelectedDevice.Id);
        LoadDevices();
        SelectedDevice = Devices.Count > 0 ? Devices[0] : null;
    }

    private static bool ContainsIgnoreCase(string value, string keyword)
    {
        return value.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    private static CameraBrand ParseBrand(string brand)
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

    private static DeviceConnectionType ParseConnectionType(string connectionType)
    {
        return connectionType switch
        {
            "HikvisionISAPI" => DeviceConnectionType.HikvisionISAPI,
            "HikvisionSDK" => DeviceConnectionType.HikvisionSDK,
            "DahuaNetSDK" => DeviceConnectionType.DahuaNetSDK,
            "ONVIF" => DeviceConnectionType.ONVIF,
            "RTSP" => DeviceConnectionType.RTSP,
            "AxisVAPIX" => DeviceConnectionType.AxisVAPIX,
            _ => DeviceConnectionType.Unknown
        };
    }
}
