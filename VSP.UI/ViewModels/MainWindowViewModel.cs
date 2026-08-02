using System.Collections.ObjectModel;
using System.Windows.Threading;
using VSP.Core.MVVM;
using VSP.UI.Services;
using VSP.UI.Views;

namespace VSP.UI.ViewModels;

public class MainWindowViewModel : ObservableObject
{
    private readonly LiveViewCameraCoordinator _liveViewCoordinator = new();
    private readonly LiveView _liveView;

    public ObservableCollection<NavigationItem> Navigation { get; } = new();

    private NavigationItem? _selectedItem;

    public NavigationItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value))
            {
                CurrentView = value?.View;
            }
        }
    }

    private object? _currentView;

    public object? CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    public MainWindowViewModel()
    {
        _liveView = new LiveView(new LiveViewViewModel(Dispatcher.CurrentDispatcher));

        Navigation.Add(new NavigationItem
        {
            Title = "Dashboard",
            Icon = "Home",
            View = new DashboardView()
        });

        var liveViewNavigationItem = new NavigationItem
        {
            Title = "Live View",
            Icon = "Video",
            View = _liveView
        };
        Navigation.Add(liveViewNavigationItem);

        Navigation.Add(new NavigationItem
        {
            Title = "Playback",
            Icon = "PlayCircle",
            View = new PlaybackView()
        });

        Navigation.Add(new NavigationItem
        {
            Title = "Devices",
            Icon = "Server",
            View = new CameraListView(_liveViewCoordinator)
        });

        Navigation.Add(new NavigationItem
        {
            Title = "Settings",
            Icon = "Cog",
            View = new SettingsView()
        });

        SelectedItem = Navigation[0];

        _liveViewCoordinator.CameraSelected += camera =>
        {
            _liveView.LoadCamera(camera);
            SelectedItem = liveViewNavigationItem;
        };
    }
}