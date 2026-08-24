using System.IO;
using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.Logging;
using VSP.Core.MVVM;
using VSP.Infrastructure.Database;
using VSP.Infrastructure.Settings;
using VSP.UI.Services;
using VSP.UI.Validation;

namespace VSP.UI.ViewModels;

/// <summary>
/// Owns the Settings screen's editable state and its Save flow (Epic-016 §4). Loads the current
/// <see cref="AppSettings"/> snapshot once at construction and keeps it as the "last-saved"
/// baseline for Cancel/dirty-checking. This ViewModel never reads or parses
/// <c>recording-settings.json</c> itself; every read/write goes through
/// <see cref="AppSettingsProvider"/>. The <c>isRecordingActive</c>/<c>confirmCreateFolder</c>
/// constructor delegates are injected by the composition root (<c>MainWindowViewModel</c>) so
/// the Save flow's several branches are directly unit-testable without a live WPF
/// <c>Application</c> or a real <c>MessageBox</c> -- a deliberate step beyond
/// <c>CameraDetailWindow</c>'s event-based confirmation convention, justified because this flow
/// has several branches worth testing directly instead of one.
/// </summary>
public class SettingsViewModel : ObservableObject
{
    private readonly AppSettingsProvider _appSettingsProvider;
    private readonly Func<bool> _isRecordingActive;
    private readonly Func<string, bool> _confirmCreateFolder;
    private readonly DatabaseBackupService _databaseBackupService;
    private readonly DatabaseRestoreService _databaseRestoreService;
    private readonly Func<string?> _chooseBackupDestination;
    private readonly Func<string?> _chooseRestoreSource;
    private readonly Func<bool> _confirmRestore;
    private readonly Func<bool> _confirmConfigOnlyRestore;
    private readonly Action _showRestartRequiredAndExit;

    private AppSettings _lastSaved;

    private string _recordingPath = string.Empty;
    private string _retentionDaysText = string.Empty;
    private AppLanguage _selectedLanguage;
    private AppTheme _selectedTheme;
    private string _statusMessage = string.Empty;

    public IReadOnlyList<AppLanguage> LanguageOptions { get; } = Enum.GetValues<AppLanguage>();
    public IReadOnlyList<AppTheme> ThemeOptions { get; } = Enum.GetValues<AppTheme>();

    public string RecordingPath
    {
        get => _recordingPath;
        set => SetProperty(ref _recordingPath, value);
    }

    public string RetentionDaysText
    {
        get => _retentionDaysText;
        set => SetProperty(ref _retentionDaysText, value);
    }

    public AppLanguage SelectedLanguage
    {
        get => _selectedLanguage;
        set => SetProperty(ref _selectedLanguage, value);
    }

    public AppTheme SelectedTheme
    {
        get => _selectedTheme;
        set => SetProperty(ref _selectedTheme, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand SaveCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BackupCommand { get; }
    public ICommand RestoreCommand { get; }

    public SettingsViewModel(
        AppSettingsProvider appSettingsProvider,
        Func<bool> isRecordingActive,
        Func<string, bool> confirmCreateFolder,
        DatabaseBackupService databaseBackupService,
        DatabaseRestoreService databaseRestoreService,
        Func<string?> chooseBackupDestination,
        Func<string?> chooseRestoreSource,
        Func<bool> confirmRestore,
        Func<bool> confirmConfigOnlyRestore,
        Action showRestartRequiredAndExit)
    {
        ArgumentNullException.ThrowIfNull(appSettingsProvider);
        ArgumentNullException.ThrowIfNull(isRecordingActive);
        ArgumentNullException.ThrowIfNull(confirmCreateFolder);
        ArgumentNullException.ThrowIfNull(databaseBackupService);
        ArgumentNullException.ThrowIfNull(databaseRestoreService);
        ArgumentNullException.ThrowIfNull(chooseBackupDestination);
        ArgumentNullException.ThrowIfNull(chooseRestoreSource);
        ArgumentNullException.ThrowIfNull(confirmRestore);
        ArgumentNullException.ThrowIfNull(confirmConfigOnlyRestore);
        ArgumentNullException.ThrowIfNull(showRestartRequiredAndExit);

        _appSettingsProvider = appSettingsProvider;
        _isRecordingActive = isRecordingActive;
        _confirmCreateFolder = confirmCreateFolder;
        _databaseBackupService = databaseBackupService;
        _databaseRestoreService = databaseRestoreService;
        _chooseBackupDestination = chooseBackupDestination;
        _chooseRestoreSource = chooseRestoreSource;
        _confirmRestore = confirmRestore;
        _confirmConfigOnlyRestore = confirmConfigOnlyRestore;
        _showRestartRequiredAndExit = showRestartRequiredAndExit;

        _lastSaved = _appSettingsProvider.Load();
        ApplySnapshot(_lastSaved);

        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
        BackupCommand = new RelayCommand(Backup);
        RestoreCommand = new RelayCommand(Restore);
    }

    private void Save()
    {
        if (!int.TryParse(RetentionDaysText, out var retentionDays) || !SettingsValidator.IsValidRetentionDays(retentionDays))
        {
            StatusMessage = $"Retention Days must be a whole number between {AppSettingsLimits.MinRetentionDays} and {AppSettingsLimits.MaxRetentionDays}.";
            return;
        }

        var recordingPathChanged = RecordingPath != _lastSaved.RecordingPath;

        if (recordingPathChanged && !TryPrepareNewRecordingPath())
        {
            return;
        }

        var newSettings = new AppSettings
        {
            RecordingPath = RecordingPath,
            RetentionDays = retentionDays,
            Language = SelectedLanguage,
            Theme = SelectedTheme
        };

        try
        {
            _appSettingsProvider.Save(newSettings);
        }
        catch (Exception ex)
        {
            AppLog.Error("Could not save settings.", ex);
            StatusMessage = "Could not save settings.";
            return;
        }

        if (newSettings.Theme != _lastSaved.Theme)
        {
            ThemeService.Apply(newSettings.Theme);
        }

        _lastSaved = newSettings;
        StatusMessage = "Settings saved.";
    }

    /// <summary>Steps 3-4 of the Save flow (§4.1): only reached when RecordingPath changed from the last-saved snapshot.</summary>
    private bool TryPrepareNewRecordingPath()
    {
        if (_isRecordingActive())
        {
            StatusMessage = "Cannot change Recording Path while a recording is in progress. Stop the current recording and try again.";
            return false;
        }

        if (!SettingsValidator.IsValidRecordingPath(RecordingPath))
        {
            StatusMessage = "Recording Path is not a valid path.";
            return false;
        }

        if (!Directory.Exists(RecordingPath))
        {
            if (!_confirmCreateFolder(RecordingPath))
            {
                StatusMessage = "Save cancelled.";
                return false;
            }

            try
            {
                Directory.CreateDirectory(RecordingPath);
            }
            catch (Exception ex)
            {
                AppLog.Error($"Could not create the recording folder '{RecordingPath}'.", ex);
                StatusMessage = "Could not create the recording folder.";
                return false;
            }
        }

        if (!SettingsValidator.HasWriteAccess(RecordingPath))
        {
            AppLog.Warning($"Recording Path '{RecordingPath}' is not writable.");
            StatusMessage = "Recording Path is not writable.";
            return false;
        }

        return true;
    }

    private void Cancel()
    {
        ApplySnapshot(_lastSaved);
        StatusMessage = string.Empty;
    }

    /// <summary>
    /// Epic-017 Backup. Never blocked by an active recording -- the SQLite Backup API is safe
    /// to use against a live, in-use database. A declined destination picker is a plain user
    /// decision, not a failure, so nothing is logged or shown for it.
    /// </summary>
    private void Backup()
    {
        var destinationPath = _chooseBackupDestination();
        if (destinationPath is null)
        {
            return;
        }

        var result = _databaseBackupService.Backup(destinationPath);
        StatusMessage = result.Success
            ? "Backup saved."
            : "Backup failed. Check the log for details.";
    }

    /// <summary>
    /// Epic-017 Restore §3.4's nine steps, in order: choose file, validate (step 1), confirm the
    /// destructive action (step 2), confirm no recording is active (step 3), install (steps 4-7
    /// plus the step-9 rollback, entirely inside <see cref="DatabaseRestoreService.Install"/>),
    /// then on success show the restart-required message and terminate (step 8). Any rejection or
    /// failure along the way ends the flow with a status message and touches nothing further.
    /// </summary>
    private void Restore()
    {
        var sourcePath = _chooseRestoreSource();
        if (sourcePath is null)
        {
            return;
        }

        var validation = DatabaseRestoreResultFromPreflight(_databaseRestoreService.PreflightBackupFile(sourcePath));
        var configOnlyRestore = false;
        if (!validation.Success)
        {
            var configOnlyPreflight = _databaseRestoreService.PreflightBackupFileForConfigOnlyRestore(sourcePath);
            if (!configOnlyPreflight.Success || !configOnlyPreflight.RequiresConfigOnlyRestore)
            {
                StatusMessage = validation.FailureMessage ?? "The selected file is not a valid backup.";
                return;
            }

            if (!_confirmConfigOnlyRestore())
            {
                StatusMessage = "Restore cancelled.";
                return;
            }

            configOnlyRestore = true;
        }

        if (!_confirmRestore())
        {
            StatusMessage = "Restore cancelled.";
            return;
        }

        if (_isRecordingActive())
        {
            StatusMessage = "Cannot restore the database while a recording is in progress. Stop the current recording and try again.";
            return;
        }

        var installResult = _databaseRestoreService.Install(sourcePath, configOnlyRestore);
        if (!installResult.Success)
        {
            StatusMessage = installResult.FailureMessage ?? "Restore failed.";
            return;
        }

        _showRestartRequiredAndExit();
    }

    private static DatabaseRestoreResult DatabaseRestoreResultFromPreflight(DatabaseRestorePreflightResult preflight)
    {
        if (!preflight.Success)
        {
            return DatabaseRestoreResult.ValidationFailed(
                preflight.FailureMessage ?? "The selected file is not a valid backup.");
        }

        return DatabaseRestoreResult.Ok();
    }

    private void ApplySnapshot(AppSettings settings)
    {
        RecordingPath = settings.RecordingPath;
        RetentionDaysText = settings.RetentionDays.ToString();
        SelectedLanguage = settings.Language;
        SelectedTheme = settings.Theme;
    }
}
