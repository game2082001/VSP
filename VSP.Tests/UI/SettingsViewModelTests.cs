using Microsoft.Data.Sqlite;
using VSP.Core.Logging;
using VSP.Infrastructure.Database;
using VSP.Infrastructure.Settings;
using VSP.Tests.Logging;
using VSP.UI.ViewModels;
using Xunit;

namespace VSP.Tests.UI;

/// <summary>
/// Covers the Save flow's branches (Epic-016 §4.1) and the Backup/Restore command's
/// orchestration (Epic-017 §5) via a real, temp-directory-backed <see cref="AppSettingsProvider"/>
/// / <see cref="DatabaseService"/> and fake delegates for every dialog/confirmation/exit -- no
/// live WPF <c>Application</c> needed, matching the codebase's existing no-mocking-library,
/// real-object-with-test-seam convention (see <c>AppSettingsProviderTests</c>). The Backup/Restore
/// *services'* own behavior (validation, atomic install, rollback) is covered by
/// <c>DatabaseBackupServiceTests</c>/<c>DatabaseRestoreServiceTests</c> -- these tests cover only
/// the ViewModel's sequencing: does it call the right delegate/service, in the right order, and
/// show the right status message for each outcome.
/// </summary>
[Collection("AppLog")]
public class SettingsViewModelTests : IDisposable
{
    private readonly string _configDirectory =
        Path.Combine(Path.GetTempPath(), $"vsp-settings-viewmodel-test-{Guid.NewGuid():N}");

    private readonly string _databaseDirectory =
        Path.Combine(Path.GetTempPath(), $"vsp-settings-viewmodel-db-test-{Guid.NewGuid():N}");

    private DatabaseService CreateDatabaseService()
    {
        var databaseService = new DatabaseService(_databaseDirectory);
        new DatabaseInitializer(databaseService).Initialize();
        return databaseService;
    }

    private static void WriteLegacyBackup(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE Camera
(
    Id TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    IpAddress TEXT NOT NULL,
    Brand INTEGER NOT NULL,
    ConnectionType INTEGER NOT NULL,
    Model TEXT,
    Location TEXT,
    HttpPort INTEGER,
    RtspPort INTEGER,
    SdkPort INTEGER,
    Username TEXT,
    Password TEXT,
    RtspUrl TEXT,
    Status INTEGER,
    Recording INTEGER,
    CreateTime TEXT,
    LastModifyTime TEXT
);

CREATE TABLE User
(
    Id TEXT PRIMARY KEY,
    Username TEXT NOT NULL,
    PasswordHash TEXT NOT NULL,
    PasswordSalt TEXT NOT NULL,
    PasswordIterations INTEGER NOT NULL,
    Role INTEGER NOT NULL,
    MustChangePassword INTEGER NOT NULL DEFAULT 0,
    CreateTime TEXT,
    LastModifyTime TEXT
);
CREATE UNIQUE INDEX IX_User_Username ON User (Username);";
        command.ExecuteNonQuery();
        SqliteConnection.ClearAllPools();
    }

    private SettingsViewModel CreateViewModel(
        AppSettingsProvider? provider = null,
        Func<bool>? isRecordingActive = null,
        Func<string, bool>? confirmCreateFolder = null,
        DatabaseBackupService? databaseBackupService = null,
        DatabaseRestoreService? databaseRestoreService = null,
        Func<string?>? chooseBackupDestination = null,
        Func<string?>? chooseRestoreSource = null,
        Func<bool>? confirmRestore = null,
        Action? showRestartRequiredAndExit = null)
    {
        var databaseService = CreateDatabaseService();

        return new SettingsViewModel(
            provider ?? new AppSettingsProvider(_configDirectory),
            isRecordingActive ?? (() => false),
            confirmCreateFolder ?? (_ => true),
            databaseBackupService ?? new DatabaseBackupService(databaseService),
            databaseRestoreService ?? new DatabaseRestoreService(databaseService),
            chooseBackupDestination ?? (() => null),
            chooseRestoreSource ?? (() => null),
            confirmRestore ?? (() => true),
            showRestartRequiredAndExit ?? (() => { }));
    }

    [Fact]
    public void Save_InvalidRetentionDays_AbortsWithoutPersisting()
    {
        var provider = new AppSettingsProvider(_configDirectory);
        var viewModel = CreateViewModel(provider);
        viewModel.RetentionDaysText = "not-a-number";

        viewModel.SaveCommand.Execute(null);

        Assert.False(string.IsNullOrEmpty(viewModel.StatusMessage));
        Assert.Equal(AppSettingsLimits.DefaultRetentionDays, provider.Load().RetentionDays);
    }

    [Fact]
    public void Save_RecordingPathUnchanged_PersistsOtherFields()
    {
        var provider = new AppSettingsProvider(_configDirectory);
        var viewModel = CreateViewModel(provider);
        viewModel.RetentionDaysText = "90";
        viewModel.SelectedLanguage = AppLanguage.TraditionalChinese;

        viewModel.SaveCommand.Execute(null);

        var saved = provider.Load();
        Assert.Equal("Settings saved.", viewModel.StatusMessage);
        Assert.Equal(90, saved.RetentionDays);
        Assert.Equal(AppLanguage.TraditionalChinese, saved.Language);
    }

    [Fact]
    public void Save_RecordingPathChangedWhileRecording_AbortsWithoutPersisting()
    {
        var provider = new AppSettingsProvider(_configDirectory);
        var viewModel = CreateViewModel(provider, isRecordingActive: () => true);
        var originalPath = viewModel.RecordingPath;
        viewModel.RecordingPath = Path.Combine(_configDirectory, "NewRecordings");

        viewModel.SaveCommand.Execute(null);

        Assert.Contains("recording is in progress", viewModel.StatusMessage);
        Assert.Equal(originalPath, provider.Load().RecordingPath);
    }

    [Fact]
    public void Save_RecordingPathChanged_FolderMissing_DeclinedCreate_AbortsWithoutPersisting()
    {
        var provider = new AppSettingsProvider(_configDirectory);
        var viewModel = CreateViewModel(provider, confirmCreateFolder: _ => false);
        var originalPath = viewModel.RecordingPath;
        var newPath = Path.Combine(_configDirectory, "DeclinedRecordings");
        viewModel.RecordingPath = newPath;

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("Save cancelled.", viewModel.StatusMessage);
        Assert.False(Directory.Exists(newPath));
        Assert.Equal(originalPath, provider.Load().RecordingPath);
    }

    [Fact]
    public void Save_RecordingPathChanged_FolderMissing_ConfirmedCreate_CreatesFolderAndPersists()
    {
        var provider = new AppSettingsProvider(_configDirectory);
        var viewModel = CreateViewModel(provider, confirmCreateFolder: _ => true);
        var newPath = Path.Combine(_configDirectory, "ConfirmedRecordings");
        viewModel.RecordingPath = newPath;

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("Settings saved.", viewModel.StatusMessage);
        Assert.True(Directory.Exists(newPath));
        Assert.Equal(newPath, provider.Load().RecordingPath);
    }

    [Fact]
    public void Save_RecordingPathChanged_CreateFolderFails_LogsErrorAndAbortsWithoutPersisting()
    {
        Directory.CreateDirectory(_configDirectory);
        var blockingFilePath = Path.Combine(_configDirectory, "BlockedByFile");
        File.WriteAllText(blockingFilePath, string.Empty);

        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        var provider = new AppSettingsProvider(_configDirectory);
        var originalPath = provider.Load().RecordingPath;
        var viewModel = CreateViewModel(provider, confirmCreateFolder: _ => true);
        viewModel.RecordingPath = blockingFilePath;

        viewModel.SaveCommand.Execute(null);

        Assert.Contains(recorder.Calls, c => c.Level == LogLevel.Error);
        Assert.Equal(originalPath, provider.Load().RecordingPath);
    }

    [Fact]
    public void Save_ThemeChanged_SucceedsWithoutALiveApplication()
    {
        var provider = new AppSettingsProvider(_configDirectory);
        var viewModel = CreateViewModel(provider);
        viewModel.SelectedTheme = AppTheme.Light;

        viewModel.SaveCommand.Execute(null);

        Assert.Equal("Settings saved.", viewModel.StatusMessage);
        Assert.Equal(AppTheme.Light, provider.Load().Theme);
    }

    [Fact]
    public void Cancel_RevertsUnsavedChangesToLastSavedSnapshot()
    {
        var provider = new AppSettingsProvider(_configDirectory);
        provider.Save(new AppSettings
        {
            RecordingPath = Path.Combine(_configDirectory, "Recordings"),
            RetentionDays = 45,
            Language = AppLanguage.English,
            Theme = AppTheme.Dark
        });
        var viewModel = CreateViewModel(provider);
        viewModel.RetentionDaysText = "999999";
        viewModel.SelectedTheme = AppTheme.Light;

        viewModel.CancelCommand.Execute(null);

        Assert.Equal("45", viewModel.RetentionDaysText);
        Assert.Equal(AppTheme.Dark, viewModel.SelectedTheme);
        Assert.Equal(string.Empty, viewModel.StatusMessage);
    }

    // ---------------------------------------------------------------------------------------
    // Backup (Epic-017)
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Backup_DestinationChosen_CallsBackupServiceAndShowsSuccessMessage()
    {
        var destinationPath = Path.Combine(_databaseDirectory, "chosen-backup.db");
        var viewModel = CreateViewModel(chooseBackupDestination: () => destinationPath);

        viewModel.BackupCommand.Execute(null);

        Assert.Equal("Backup saved.", viewModel.StatusMessage);
        Assert.True(File.Exists(destinationPath));
    }

    [Fact]
    public void Backup_DialogCancelled_LeavesStatusMessageUnchanged()
    {
        var viewModel = CreateViewModel(chooseBackupDestination: () => null);

        viewModel.BackupCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.StatusMessage);
    }

    [Fact]
    public void Backup_DestinationDirectoryMissing_ShowsFailureMessage()
    {
        var invalidDestination = Path.Combine(_databaseDirectory, "missing-subdirectory", "backup.db");
        var viewModel = CreateViewModel(chooseBackupDestination: () => invalidDestination);

        viewModel.BackupCommand.Execute(null);

        Assert.Equal("Backup failed. Check the log for details.", viewModel.StatusMessage);
    }

    // ---------------------------------------------------------------------------------------
    // Restore (Epic-017 §3.4's nine steps, as orchestrated by SettingsViewModel)
    // ---------------------------------------------------------------------------------------

    [Fact]
    public void Restore_DialogCancelled_LeavesStatusMessageUnchanged()
    {
        var viewModel = CreateViewModel(chooseRestoreSource: () => null);

        viewModel.RestoreCommand.Execute(null);

        Assert.Equal(string.Empty, viewModel.StatusMessage);
    }

    [Fact]
    public void Restore_InvalidSourceFile_ShowsValidationMessageWithoutAskingForConfirmation()
    {
        CreateDatabaseService();
        var invalidPath = Path.Combine(_databaseDirectory, "not-a-database.db");
        File.WriteAllText(invalidPath, "not a database");
        var confirmWasAsked = false;
        var viewModel = CreateViewModel(
            chooseRestoreSource: () => invalidPath,
            confirmRestore: () =>
            {
                confirmWasAsked = true;
                return true;
            });

        viewModel.RestoreCommand.Execute(null);

        Assert.False(confirmWasAsked);
        Assert.False(string.IsNullOrEmpty(viewModel.StatusMessage));
        Assert.NotEqual("Restore cancelled.", viewModel.StatusMessage);
    }

    [Fact]
    public void Restore_ValidSourceFile_UserDeclinesConfirmation_AbortsWithoutInstalling()
    {
        var backupPath = Path.Combine(_databaseDirectory, "valid-backup.db");
        WriteLegacyBackup(backupPath);
        var viewModel = CreateViewModel(
            chooseRestoreSource: () => backupPath,
            confirmRestore: () => false);

        viewModel.RestoreCommand.Execute(null);

        Assert.Equal("Restore cancelled.", viewModel.StatusMessage);
    }

    [Fact]
    public void Restore_ValidSourceFile_ConfirmedButRecordingActive_BlocksWithoutInstalling()
    {
        var backupPath = Path.Combine(_databaseDirectory, "valid-backup.db");
        WriteLegacyBackup(backupPath);
        var viewModel = CreateViewModel(
            isRecordingActive: () => true,
            chooseRestoreSource: () => backupPath,
            confirmRestore: () => true);

        viewModel.RestoreCommand.Execute(null);

        Assert.Contains("recording is in progress", viewModel.StatusMessage);
    }

    [Fact]
    public void Restore_ValidSourceFile_ConfirmedAndNotRecording_InstallsAndTriggersRestartCallback()
    {
        var backupPath = Path.Combine(_databaseDirectory, "valid-backup.db");
        WriteLegacyBackup(backupPath);
        var restartTriggered = false;
        var viewModel = CreateViewModel(
            chooseRestoreSource: () => backupPath,
            confirmRestore: () => true,
            showRestartRequiredAndExit: () => restartTriggered = true);

        viewModel.RestoreCommand.Execute(null);

        Assert.True(restartTriggered);
    }

    [Fact]
    public void Restore_InstallFails_ShowsFailureMessageAndDoesNotTriggerRestartCallback()
    {
        var backupPath = Path.Combine(_databaseDirectory, "valid-backup.db");
        var databaseService = CreateDatabaseService();
        WriteLegacyBackup(backupPath);
        var restartTriggered = false;
        var viewModel = CreateViewModel(
            chooseRestoreSource: () => backupPath,
            confirmRestore: () => true,
            showRestartRequiredAndExit: () => restartTriggered = true);

        SqliteConnection.ClearAllPools();
        using (new FileStream(databaseService.GetDatabaseFilePath(), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            viewModel.RestoreCommand.Execute(null);
        }

        Assert.False(restartTriggered);
        Assert.False(string.IsNullOrEmpty(viewModel.StatusMessage));
        Assert.NotEqual("Restore cancelled.", viewModel.StatusMessage);
    }

    public void Dispose()
    {
        try
        {
            SqliteConnection.ClearAllPools();

            if (Directory.Exists(_configDirectory))
            {
                Directory.Delete(_configDirectory, recursive: true);
            }

            if (Directory.Exists(_databaseDirectory))
            {
                Directory.Delete(_databaseDirectory, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
