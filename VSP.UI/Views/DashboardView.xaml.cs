using System.Windows;
using System.Windows.Controls;
using VSP.Device.Drivers;
using VSP.Device.Repositories;
using VSP.Device.Services;
using VSP.UI.ViewModels;

namespace VSP.UI.Views;

public partial class DashboardView : UserControl
{
    private readonly DashboardViewModel _viewModel;

    public DashboardView()
        : this(new DashboardViewModel(
            new CameraQueryService(new CameraRepository()),
            DriverRegistry.CreateDefault()))
    {
    }

    internal DashboardView(DashboardViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += HandleLoaded;
    }

    private async void HandleLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= HandleLoaded;
        await _viewModel.LoadAsync();
    }
}
