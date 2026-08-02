using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using VSP.Device.Repositories;
using VSP.Device.Services;
using VSP.UI.ViewModels;

namespace VSP.UI.Views;

public partial class PlaybackView : UserControl
{
    private readonly PlaybackViewModel _viewModel;

    public PlaybackView()
        : this(new PlaybackViewModel(new CameraQueryService(new CameraRepository()), Dispatcher.CurrentDispatcher))
    {
    }

    internal PlaybackView(PlaybackViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += HandleLoaded;
    }

    private async void HandleLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= HandleLoaded;
        await _viewModel.LoadCamerasAsync();
    }

    /// <summary>
    /// Seeks only on release (click-to-position or drag-release), never on every Value change --
    /// the slider's Value binding is OneWay from playback position, so a seek on every tick would
    /// fight the position updates already flowing the other way.
    /// </summary>
    private void SeekSlider_PreviewMouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _viewModel.Seek(TimeSpan.FromSeconds(SeekSlider.Value));
    }
}
