using System.Windows;
using System.Windows.Controls;
using VSP.Device.Repositories;
using VSP.Device.Services;
using VSP.UI.ViewModels;

namespace VSP.UI.Views;

public partial class CameraListView : UserControl
{
    private readonly CameraListViewModel _viewModel;

    public CameraListView()
        : this(new CameraListViewModel(new CameraQueryService(new CameraRepository())))
    {
    }

    internal CameraListView(CameraListViewModel viewModel)
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
