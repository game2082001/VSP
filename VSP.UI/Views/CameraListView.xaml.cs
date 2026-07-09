using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VSP.Device.Interfaces;
using VSP.Device.Repositories;
using VSP.Device.Services;
using VSP.UI.ViewModels;

namespace VSP.UI.Views;

public partial class CameraListView : UserControl
{
    private readonly CameraListViewModel _viewModel;
    private readonly ICameraRepository _cameraRepository;

    public CameraListView()
        : this(new CameraListViewModel(new CameraQueryService(new CameraRepository())), new CameraRepository())
    {
    }

    internal CameraListView(CameraListViewModel viewModel, ICameraRepository cameraRepository)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _cameraRepository = cameraRepository;
        DataContext = _viewModel;
        _viewModel.RequestAddCamera += HandleRequestAddCamera;
        Loaded += HandleLoaded;
    }

    private async void HandleLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= HandleLoaded;
        await _viewModel.LoadAsync();
    }

    private void HandleCameraRowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedCamera?.SourceCamera is null)
        {
            return;
        }

        if (!IsDataGridRowDoubleClick(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var detailViewModel = new CameraDetailViewModel(_viewModel.SelectedCamera.SourceCamera, _cameraRepository);
        var detailWindow = new CameraDetailWindow(detailViewModel)
        {
            Owner = Window.GetWindow(this)
        };

        detailWindow.ShowDialog();
    }

    private async void HandleRequestAddCamera()
    {
        var detailViewModel = new CameraDetailViewModel(_cameraRepository);
        var detailWindow = new CameraDetailWindow(detailViewModel)
        {
            Owner = Window.GetWindow(this)
        };

        detailWindow.ShowDialog();

        if (detailViewModel.WasSaved)
        {
            await _viewModel.RefreshAsync(detailViewModel.SavedCameraId);
        }
    }

    private static bool IsDataGridRowDoubleClick(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is DataGridRow)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
