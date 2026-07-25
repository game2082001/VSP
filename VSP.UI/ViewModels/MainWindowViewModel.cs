using System.Collections.ObjectModel;
using VSP.Core.MVVM;
using VSP.UI.Views;

namespace VSP.UI.ViewModels;

public class MainWindowViewModel : ObservableObject
{
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
        Navigation.Add(new NavigationItem
        {
            Title = "Dashboard",
            Icon = "Home",
            View = new DashboardView()
        });

        Navigation.Add(new NavigationItem
        {
            Title = "Live View",
            Icon = "Video",
            View = new LiveView()
        });

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
            View = new CameraListView()
        });

        Navigation.Add(new NavigationItem
        {
            Title = "Settings",
            Icon = "Cog",
            View = new SettingsView()
        });

        SelectedItem = Navigation[0];
    }
}