using System.Collections.ObjectModel;
using VSP.Core.MVVM;
using VSP.UI.Views;

namespace VSP.UI.ViewModels;

public class MainWindowViewModel : ObservableObject
{
    public ObservableCollection<NavigationItem> Navigation { get; }

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
        Navigation = new ObservableCollection<NavigationItem>
        {
            new()
            {
                Title="Dashboard",
                View=new DashboardView()
            },

            new()
            {
                Title="Live View",
                View=new LiveView()
            },

            new()
            {
                Title="Playback",
                View=new PlaybackView()
            },

            new()
            {
                Title="Devices",
                View=new DeviceView()
            },

            new()
            {
                Title="Settings",
                View=new SettingsView()
            }
        };

        SelectedItem = Navigation[0];
    }
}