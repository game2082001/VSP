using System.Collections.ObjectModel;
using VSP.Core.MVVM;

namespace VSP.UI.ViewModels;

public class DeviceViewModel : ObservableObject
{
    public ObservableCollection<DeviceItem> Devices { get; }

    public DeviceViewModel()
    {
        Devices = new ObservableCollection<DeviceItem>
        {
            new DeviceItem
            {
                Name = "Cam01",
                IP = "192.168.1.101",
                Brand = "Hikvision"
            },

            new DeviceItem
            {
                Name = "Cam02",
                IP = "192.168.1.102",
                Brand = "Dahua"
            },

            new DeviceItem
            {
                Name = "Cam03",
                IP = "192.168.1.103",
                Brand = "ONVIF"
            }
        };
    }
}

public class DeviceItem
{
    public string Name { get; set; } = "";

    public string IP { get; set; } = "";

    public string Brand { get; set; } = "";
}