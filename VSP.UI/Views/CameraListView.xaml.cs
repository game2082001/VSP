using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VSP.Device.Export;
using VSP.Device.Interfaces;
using VSP.Device.Repositories;
using VSP.Device.Services;
using VSP.Domain.Entities;
using VSP.UI.Helpers;
using VSP.UI.Services;
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

    public CameraListView(LiveViewCameraCoordinator liveViewCoordinator)
        : this(new CameraListViewModel(new CameraQueryService(new CameraRepository()), liveViewCoordinator), new CameraRepository())
    {
    }

    internal CameraListView(CameraListViewModel viewModel, ICameraRepository cameraRepository)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _cameraRepository = cameraRepository;
        DataContext = _viewModel;
        _viewModel.RequestAddCamera += HandleRequestAddCamera;
        _viewModel.RequestImport += HandleRequestImport;
        _viewModel.RequestBatchEdit += HandleRequestBatchEdit;
        _viewModel.RequestBatchConnectionTest += HandleRequestBatchConnectionTest;
        _viewModel.RequestExport += HandleRequestExport;
        Loaded += HandleLoaded;
    }

    private async void HandleLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= HandleLoaded;
        await _viewModel.LoadAsync();
    }

    private async void HandleCameraRowDoubleClick(object sender, MouseButtonEventArgs e)
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

        if (detailViewModel.WasDeleted)
        {
            await _viewModel.RefreshAsync();
        }
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

    private async void HandleRequestImport()
    {
        var importWizard = new ImportWizard
        {
            Owner = Window.GetWindow(this)
        };

        importWizard.ShowDialog();

        await _viewModel.RefreshAsync();
    }

    private async void HandleRequestBatchEdit(IReadOnlyList<Camera> selectedCameras)
    {
        var batchEditViewModel = new BatchEditViewModel(selectedCameras, _cameraRepository);
        var batchEditWindow = new BatchEditWindow(batchEditViewModel)
        {
            Owner = Window.GetWindow(this)
        };

        batchEditWindow.ShowDialog();

        if (batchEditViewModel.WasApplied)
        {
            await _viewModel.RefreshAsync();
        }
    }

    private async void HandleRequestBatchConnectionTest(IReadOnlyList<Camera> selectedCameras)
    {
        var connectionTestViewModel = new BatchConnectionTestViewModel(
            selectedCameras,
            new CameraConnectionTester(),
            _cameraRepository);
        var connectionTestWindow = new BatchConnectionTestWindow(connectionTestViewModel)
        {
            Owner = Window.GetWindow(this)
        };

        connectionTestWindow.ShowDialog();

        await _viewModel.RefreshAsync();
    }

    private void HandleRequestExport(IReadOnlyList<Camera> visibleCameras)
    {
        var filePath = new ExportFileSelector().SelectFile();

        if (filePath is null)
        {
            return;
        }

        try
        {
            var csv = CameraExportWriter.Write(visibleCameras);
            System.IO.File.WriteAllText(filePath, csv, System.Text.Encoding.UTF8);
            MessageBox.Show($"Exported {visibleCameras.Count} camera(s) to {filePath}.", "VSP");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "VSP");
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
