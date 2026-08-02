using System.IO;
using System.Windows;
using System.Windows.Threading;
using VSP.Core.Logging;
using VSP.Infrastructure.Database;

namespace VSP.UI;

public partial class App : Application
{
    private FileLogger? _logger;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        InitializeLogging();

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