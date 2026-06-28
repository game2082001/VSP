using System.Windows.Controls;
using VSP.UI.ViewModels;

namespace VSP.UI.Views.DeviceCenter;

public partial class DeviceCenterView : UserControl
{
    public DeviceCenterView()
    {
        InitializeComponent();
        DataContext = new DeviceCenterViewModel();
    }
}