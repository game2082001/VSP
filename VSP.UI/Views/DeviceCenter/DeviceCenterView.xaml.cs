using System.Windows.Controls;
using VSP.UI.ViewModels;

namespace VSP.UI.Views.DeviceCenter;

// Legacy: superseded by CameraListView (Epic-005, Camera Management Workspace).
// No longer hosted in MainWindowViewModel. Kept to minimize migration risk;
// scheduled for removal in a future Legacy Cleanup Epic.
[Obsolete("Superseded by CameraListView. Scheduled for removal in a future Legacy Cleanup Epic.")]
public partial class DeviceCenterView : UserControl
{
    public DeviceCenterView()
    {
        InitializeComponent();
#pragma warning disable CS0618 // DeviceCenterViewModel is intentionally-retained legacy code
        DataContext = new DeviceCenterViewModel();
#pragma warning restore CS0618
    }
}