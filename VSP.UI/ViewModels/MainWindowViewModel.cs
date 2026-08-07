using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using VSP.Core.Commands;
using VSP.Core.MVVM;
using VSP.Domain.Entities;
using VSP.Domain.Enums;
using VSP.Infrastructure.Database;
using VSP.Infrastructure.Settings;
using VSP.UI.Services;
using VSP.UI.Views;

namespace VSP.UI.ViewModels;

public class MainWindowViewModel : ObservableObject
{
    private readonly LiveViewCameraCoordinator _liveViewCoordinator = new();
    private readonly LiveView _liveView;
    private readonly LiveViewViewModel _liveViewViewModel;
    private readonly SessionService _sessionService;

    /// <summary>
    /// Epic-018 §4.10's session: the authenticated identity for as long as MainWindow is open.
    /// Delegates entirely to <see cref="SessionService"/> (Milestone 18D) -- this ViewModel no
    /// longer owns the value itself, only reads it and asks the service to clear it on Logout.
    /// </summary>
    public User? CurrentUser => _sessionService.CurrentUser;

    public ICommand LogoutCommand { get; }

    /// <summary>Raised by Logout so App.xaml.cs's session loop can close MainWindow and show LoginWindow again.</summary>
    public event Action? LogoutRequested;

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

    public MainWindowViewModel(SessionService sessionService)
    {
        ArgumentNullException.ThrowIfNull(sessionService);
        if (sessionService.CurrentUser is null)
        {
            throw new InvalidOperationException(
                "MainWindowViewModel requires an already-started session (SessionService.Start must be called before construction).");
        }

        _sessionService = sessionService;
        var currentUser = sessionService.CurrentUser;

        LogoutCommand = new RelayCommand(Logout);

        _liveViewViewModel = new LiveViewViewModel(Dispatcher.CurrentDispatcher, currentUser.Role);
        _liveView = new LiveView(_liveViewViewModel);

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

        // Epic-018 §4.5/§8.4: Operator gets a read-only Devices screen (needed to select a camera
        // and reach Live View, per §2.5) -- CameraListView/CameraListViewModel/CameraDetailViewModel
        // gate every modification/Discovery command internally based on the role passed here.
        Navigation.Add(new NavigationItem
        {
            Title = "Devices",
            Icon = "Server",
            View = new CameraListView(_liveViewCoordinator, currentUser.Role)
        });

        // Epic-018 §4.5 Part A / §8: Settings (and, inside it, Backup/Restore) is Admin-only.
        // Not even the underlying services are constructed for an Operator session.
        if (currentUser.Role == Role.Admin)
        {
            var databaseService = new DatabaseService();
            var settingsViewModel = new SettingsViewModel(
                new AppSettingsProvider(),
                () => _liveViewViewModel.IsRecording,
                ConfirmCreateFolder,
                new DatabaseBackupService(databaseService),
                new DatabaseRestoreService(databaseService),
                SettingsView.ChooseBackupDestination,
                SettingsView.ChooseRestoreSource,
                ConfirmRestore,
                ShowRestartRequiredAndExit);

            Navigation.Add(new NavigationItem
            {
                Title = "Settings",
                Icon = "Cog",
                View = new SettingsView(settingsViewModel)
            });
        }

        SelectedItem = Navigation[0];

        _liveViewCoordinator.CameraSelected += camera =>
        {
            _liveView.LoadCamera(camera);
            SelectedItem = liveViewNavigationItem;
        };
    }

    private void Logout()
    {
        if (!ConfirmLogout())
        {
            return;
        }

        _sessionService.End();
        OnPropertyChanged(nameof(CurrentUser));
        LogoutRequested?.Invoke();
    }

    /// <summary>
    /// Milestone 18D requirement: Logout requires confirmation. Same in-ViewModel
    /// <see cref="MessageBox"/> idiom already used by <see cref="ConfirmCreateFolder"/>/
    /// <see cref="ConfirmRestore"/> in this file, not a new pattern.
    /// </summary>
    private static bool ConfirmLogout()
    {
        return MessageBox.Show(
            "Are you sure you want to log out?",
            "Logout",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    /// <summary>
    /// The <c>confirmCreateFolder</c> delegate injected into <see cref="SettingsViewModel"/>
    /// (Epic-016 §4) -- keeps the actual <see cref="MessageBox"/> call out of the ViewModel
    /// while still letting its Save flow branch on the user's answer.
    /// </summary>
    private static bool ConfirmCreateFolder(string path)
    {
        return MessageBox.Show(
            $"The folder \"{path}\" does not exist. Create it?",
            "Create Recording Folder",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    /// <summary>
    /// Epic-017 Restore's destructive-action confirmation (§3.4 step 2) -- keeps the actual
    /// <see cref="MessageBox"/> call out of <see cref="SettingsViewModel"/>, same convention as
    /// <see cref="ConfirmCreateFolder"/>.
    /// </summary>
    private static bool ConfirmRestore()
    {
        return MessageBox.Show(
            "This will replace the current database with the selected backup. This cannot be undone from within VSP." +
            Environment.NewLine + Environment.NewLine +
            "Are you sure you want to restore the database?",
            "Restore Database",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    /// <summary>
    /// Epic-017 Restore's step 8 (§3.4/§3.5): shown only after a successful <c>Install</c>. No
    /// automatic relaunch is implemented in this Epic -- the user reopens VSP manually, matching
    /// the app's existing <see cref="Environment.Exit(int)"/>-only shutdown model (every other
    /// call site lives in <c>App.xaml.cs</c>; this is the one ViewModel-triggered exception,
    /// kept out of <see cref="SettingsViewModel"/> so the ViewModel itself never terminates the
    /// process).
    /// </summary>
    private static void ShowRestartRequiredAndExit()
    {
        MessageBox.Show(
            "The database was restored successfully." +
            Environment.NewLine + Environment.NewLine +
            "VSP must restart before the restored database can be used. Click OK to close VSP, then start it again.",
            "Restore Complete - Restart Required",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        Environment.Exit(0);
    }
}