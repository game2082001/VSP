using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.MVVM;
using VSP.UI.Views;

namespace VSP.UI.ViewModels;

public class DeviceViewModel : ObservableObject
{
    public ObservableCollection<DeviceItem> Devices { get; }

    public ICommand AddDeviceCommand { get; }
    public ICommand EditDeviceCommand { get; }
    public ICommand DeleteDeviceCommand { get; }
    public ICommand RefreshCommand { get; }

    public DeviceViewModel()
    {
        Devices = new ObservableCollection<DeviceItem>
        {
            new DeviceItem { Name = "Cam01", IP = "192.168.1.101", Brand = "Hikvision" },
            new DeviceItem { Name = "Cam02", IP = "192.168.1.102", Brand = "Dahua" },
            new DeviceItem { Name = "Cam03", IP = "192.168.1.103", Brand = "ONVIF" }
        };

        AddDeviceCommand = new RelayCommand(AddDevice);
        EditDeviceCommand = new RelayCommand(EditDevice);
        DeleteDeviceCommand = new RelayCommand(DeleteDevice);
        RefreshCommand = new RelayCommand(Refresh);
    }

    private void AddDevice()
    {
        var window = new Views.AddDeviceWindow();

        if (window.ShowDialog() == true)
        {
            Devices.Add(new DeviceItem
            {
                Name = window.DeviceName,
                IP = window.IpAddress,
                Brand = window.Brand
            });
        }
    }

    private void EditDevice()
    {
        MessageBox.Show("編輯設備", "VSP");
    }

    private void DeleteDevice()
    {
        MessageBox.Show("刪除設備", "VSP");
    }

    private void Refresh()
    {
        MessageBox.Show("重新整理設備", "VSP");
    }
}

public class DeviceItem
{
    public string Name { get; set; } = "";
    public string IP { get; set; } = "";
    public string Brand { get; set; } = "";
}