using System.IO;
using System.Windows;
using System.Windows.Threading;
using VSP.Core.Logging;
using VSP.Device.Interfaces;
using VSP.Device.Repositories;
using VSP.Domain.Entities;
using VSP.Infrastructure.Database;
using VSP.Infrastructure.Settings;
using VSP.UI.Services;
using VSP.UI.ViewModels;
using VSP.UI.Views;

namespace VSP.UI;

public partial class App : Application
{
    private FileLogger? _logger;
    private IUserRepository? _userRepository;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        InitializeLogging();

        var appSettings = new AppSettingsProvider().Load();
        ThemeService.Apply(appSettings.Theme);

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var databaseService = new DatabaseService();
        var initializer = new DatabaseInitializer(databaseService);

        var initializationResult = initializer.Initialize();
        if (!initializationResult.Success)
        {
            HandleDatabaseInitializationFailure(initializationResult.Exception);
            return;
        }

        _userRepository = new UserRepository();
        StartSession();
    }

    /// <summary>
    /// Epic-018 Milestone 18C: one full Login -> (optional Forced Password Change) -> MainWindow
    /// cycle. Recurses on Logout (MainWindowViewModel.LogoutRequested) to show a fresh
    /// LoginWindow without restarting the process (§4.9) -- each cycle constructs a brand new
    /// MainWindowViewModel/MainWindow pair, so nothing from the previous session is reused.
    /// Any exit other than Logout (closing LoginWindow/ForcedPasswordChangeWindow via the title
    /// bar X, or closing MainWindow itself) calls Shutdown() and does not recurse.
    /// </summary>
    private void StartSession()
    {
        var authenticatedUser = ShowLoginWindow(_userRepository!);
        if (authenticatedUser is null)
        {
            Shutdown();
            return;
        }

        if (authenticatedUser.MustChangePassword && !ShowForcedPasswordChangeWindow(authenticatedUser, _userRepository!))
        {
            Shutdown();
            return;
        }

        // Milestone 18D: a brand new SessionService per login -- never reused across a Logout,
        // matching MainWindowViewModel/MainWindow's own "construct fresh every cycle" convention.
        var sessionService = new SessionService();
        sessionService.Start(authenticatedUser);

        var mainWindowViewModel = new MainWindowViewModel(sessionService);
        var mainWindow = new MainWindow(mainWindowViewModel);
        var isLoggingOut = false;

        mainWindowViewModel.LogoutRequested += () =>
        {
            isLoggingOut = true;
            mainWindow.Close();
        };

        mainWindow.Closed += (_, _) =>
        {
            if (isLoggingOut)
            {
                StartSession();
            }
            else
            {
                Shutdown();
            }
        };

        MainWindow = mainWindow;
        mainWindow.Show();
    }

    /// <summary>
    /// Epic-018 §4.2/§4.3: MainWindow must never be constructed until this returns a non-null
    /// User. Blocking via ShowDialog (not Show) is what guarantees no other startup code runs
    /// concurrently with an unauthenticated session. Closing this window via the title bar X
    /// (ShowDialog returns null, not true) is indistinguishable here from a plain failed login --
    /// both return null, and the caller shuts the app down either way.
    /// </summary>
    private static User? ShowLoginWindow(IUserRepository userRepository)
    {
        var viewModel = new LoginViewModel(userRepository);
        var window = new LoginWindow(viewModel);

        return window.ShowDialog() == true ? viewModel.AuthenticatedUser : null;
    }

    /// <summary>
    /// Epic-018 §4.18 (Decision 5): a second blocking gate, only shown when the just-authenticated
    /// user has MustChangePassword set. Closing this window any way other than a successful
    /// change (including the title bar's X button) returns false here, which aborts startup via
    /// Shutdown() in the caller -- there is no path from this window into MainWindow other than
    /// completing the change.
    /// </summary>
    private static bool ShowForcedPasswordChangeWindow(User user, IUserRepository userRepository)
    {
        var viewModel = new ForcedPasswordChangeViewModel(user, userRepository);
        var window = new ForcedPasswordChangeWindow(viewModel);

        return window.ShowDialog() == true;
    }

    /// <summary>
    /// Database initialization failure per the approved Epic-015 scope. A single Error ID is
    /// generated here and used for both the log line and the dialog -- the original exception is
    /// logged Fatal together with that same ID in one call, never split across two log entries
    /// (Product Owner instruction). Terminates startup cleanly -- the app must not continue
    /// without a working database.
    /// </summary>
    private void HandleDatabaseInitializationFailure(Exception? exception)
    {
        var errorId = NewErrorId();
        AppLog.Fatal($"Startup aborted: database initialization failed. [ErrorId: {errorId}]", exception);

        var logFilePath = _logger?.GetCurrentLogFilePath() ?? "(log file unavailable)";

        MessageBox.Show(
            "VSP could not start because its database could not be initialized." +
            Environment.NewLine + Environment.NewLine +
            $"Error ID: {errorId}" + Environment.NewLine + Environment.NewLine +
            "Please send the latest log file below along with this Error ID to support:" +
            Environment.NewLine + logFilePath,
            "VSP - Startup Failed",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        Environment.Exit(1);
    }

    private void InitializeLogging()
    {
        var logsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VSP", "Logs");

        _logger = new FileLogger(logsDirectory);
        AppLog.Initialize(_logger);

        var purged = _logger.PurgeOldFiles();
        if (purged > 0)
        {
            AppLog.Info($"Purged {purged} log file(s) older than {FileLogger.DefaultRetentionDays} days.");
        }
    }

    private static string NewErrorId() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    /// <summary>Non-UI-thread exception: fatal, per the approved Epic-014 scope -- log then exit deliberately.</summary>
    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var errorId = NewErrorId();
        AppLog.Fatal($"Unhandled exception on a non-UI thread. [ErrorId: {errorId}]", e.ExceptionObject as Exception);
        Environment.Exit(1);
    }

    /// <summary>UI-thread exception: recoverable, per the approved Epic-014 scope -- log, notify, continue.</summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var errorId = NewErrorId();
        AppLog.Error($"Unhandled exception on the UI thread. [ErrorId: {errorId}]", e.Exception);

        var logFilePath = _logger?.GetCurrentLogFilePath() ?? "(log file unavailable)";

        MessageBox.Show(
            "An unexpected error occurred. VSP will continue running, but you may want to save your work and restart." +
            Environment.NewLine + Environment.NewLine +
            $"Error ID: {errorId}" + Environment.NewLine + Environment.NewLine +
            "If you contact support, please send the latest log file below along with this Error ID:" +
            Environment.NewLine + logFilePath,
            "VSP - Unexpected Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    /// <summary>Unobserved Task exception: recoverable (does not terminate the process by default) -- log and mark observed.</summary>
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var errorId = NewErrorId();
        AppLog.Error($"Unobserved task exception. [ErrorId: {errorId}]", e.Exception);
        e.SetObserved();
    }
}