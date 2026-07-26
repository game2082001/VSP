using System.Windows.Controls;
using VSP.Device.Discovery.Workspace;
using VSP.Device.Repositories;
using VSP.UI.ViewModels;

namespace VSP.UI.Views;

public partial class CameraDiscoveryView : UserControl
{
    public CameraDiscoveryView()
        : this(new CameraDiscoveryViewModel(
            CameraDiscoveryOrchestratorFactory.CreateDefault(new CameraRepository())))
    {
    }

    internal CameraDiscoveryView(CameraDiscoveryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
