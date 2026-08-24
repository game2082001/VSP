using Microsoft.Data.Sqlite;
using VSP.Core.Logging;
using VSP.Infrastructure.Security;

namespace VSP.Infrastructure.Database;

/// <summary>
/// Validates a user-selected backup file and, on request, installs it as the live database
/// (Epic-017 §3.4/§3.7). Never touches recording state or shows any UI -- the destructive-action
/// confirmation and the recording-active guard are <c>SettingsViewModel</c>'s job, invoked
/// between <see cref="ValidateBackupFile"/> and <see cref="Install"/>. The user-selected source
/// file is only ever read, never moved or modified.
/// </summary>
public class DatabaseRestoreService
{
    private readonly DatabaseService _databaseService;
    private readonly DatabaseRestorePreflight _preflight;

    public DatabaseRestoreService(DatabaseService databaseService)
        : this(databaseService, new DpapiCurrentUserCameraCredentialProtector())
    {
    }

    internal DatabaseRestoreService(DatabaseService databaseService, ICameraCredentialProtector protector)
    {
        _databaseService = databaseService;
        _preflight = new DatabaseRestorePreflight(protector);
    }

    /// <summary>
    /// Step 1 of §3.4, standalone: the six-point §3.7 checklist against <paramref name="sourceFilePath"/>
    /// only -- never touches the live database. Lets the caller gate a destructive-action
    /// confirmation and a recording-active check on a valid file before asking about either.
    /// </summary>
    public DatabaseRestoreResult ValidateBackupFile(string sourceFilePath)
    {
        var preflight = PreflightBackupFile(sourceFilePath);
        var result = ToValidationResult(preflight);

        if (!result.Success)
        {
            AppLog.Warning($"Rejected restore source '{sourceFilePath}': {result.FailureMessage}");
        }

        return result;
    }

    public DatabaseRestorePreflightResult PreflightBackupFile(string sourceFilePath) =>
        _preflight.Check(sourceFilePath, _databaseService.GetDatabaseFilePath());

    /// <summary>
    /// Steps 4-7 and the step-9 rollback of §3.4. Re-validates <paramref name="sourceFilePath"/>
    /// itself first (defense in depth -- this method never trusts a prior
    /// <see cref="ValidateBackupFile"/> call as sufficient authorization to touch the live file,
    /// so the active database can never be replaced by an unvalidated file no matter how a future
    /// caller sequences things), stages and atomically installs it, re-validates the newly
    /// installed file, and rolls back to the pre-restore copy on any failure from the install
    /// step onward.
    /// </summary>
    public DatabaseRestoreResult Install(string sourceFilePath)
    {
        var databaseFilePath = _databaseService.GetDatabaseFilePath();

        var preflight = PreflightBackupFile(sourceFilePath);
        var validation = ToValidationResult(preflight);
        if (!validation.Success)
        {
            AppLog.Warning($"Rejected restore source '{sourceFilePath}': {validation.FailureMessage}");
            return validation;
        }

        var databaseDirectory = _databaseService.GetDatabaseDirectory();
        Directory.CreateDirectory(databaseDirectory);

        var preRestorePath = Path.Combine(
            databaseDirectory,
            $"vsp.pre-restore.{DateTime.Now:yyyyMMdd-HHmmss}.db");
        // Fixed, not GUID-based: Restore is a synchronous, foreground, confirmation-gated UI
        // action -- exactly one can ever be in flight at a time, so uniqueness across concurrent
        // restores is not a real requirement, and a fixed name is easier to reason about if a
        // leftover temp file is ever found on disk after a crash.
        var tempInstallPath = Path.Combine(databaseDirectory, "vsp.db.restoring.tmp");

        // Release any pooled native handle against the live file before renaming it away.
        SqliteConnection.ClearAllPools();

        var originalRenamedAside = false;
        if (File.Exists(databaseFilePath))
        {
            try
            {
                File.Move(databaseFilePath, preRestorePath);
                originalRenamedAside = true;
            }
            catch (Exception ex)
            {
                // Step 4 itself failed -- the live database was never touched, nothing to roll back.
                AppLog.Error("Could not preserve the current database before installing the restored backup.", ex);
                return DatabaseRestoreResult.Failed(
                    "Restore could not start because the current database could not be preserved. The current database is unchanged.",
                    ex);
            }
        }

        try
        {
            // Step 5: copy, never move -- the validated source file itself is untouched.
            File.Copy(sourceFilePath, tempInstallPath, overwrite: true);

            // Step 6: atomic same-volume rename into the live path.
            File.Move(tempInstallPath, databaseFilePath, overwrite: true);

            // Step 7: re-validate the file now actually sitting at the live path.
            var postInstallValidation = ToValidationResult(_preflight.Check(
                databaseFilePath,
                liveDatabasePath: null,
                verifyLegacyConvertibility: false));
            if (!postInstallValidation.Success)
            {
                throw new InvalidOperationException(
                    $"The installed database failed validation: {postInstallValidation.FailureMessage}");
            }

            return DatabaseRestoreResult.Ok();
        }
        catch (Exception ex)
        {
            RollbackFailedInstall(databaseFilePath, tempInstallPath, originalRenamedAside ? preRestorePath : null);

            AppLog.Error("Database restore failed. The original database has been restored.", ex);

            return DatabaseRestoreResult.Failed(
                "Restore failed. The original database has been kept and remains usable.",
                ex);
        }
    }

    /// <summary>Step 9 of §3.4: remove a failed replacement, if present, then restore the preserved original.</summary>
    private static void RollbackFailedInstall(string databaseFilePath, string tempInstallPath, string? preRestorePath)
    {
        TryDelete(tempInstallPath);

        if (preRestorePath is null)
        {
            return;
        }

        try
        {
            if (File.Exists(preRestorePath))
            {
                File.Move(preRestorePath, databaseFilePath, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            AppLog.Fatal(
                "Restore rollback failed -- the pre-restore database could not be restored to the active path. " +
                $"The pre-restore copy remains at '{preRestorePath}'.",
                ex);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup of a temp/failed-install artifact; the rename-back that follows
            // uses overwrite: true regardless, so a cleanup failure here does not block rollback.
        }
    }

    private static DatabaseRestoreResult ToValidationResult(DatabaseRestorePreflightResult preflight)
    {
        if (!preflight.Success)
        {
            return DatabaseRestoreResult.ValidationFailed(
                preflight.FailureMessage ?? "The selected file is not a valid backup.");
        }

        if (!preflight.CanInstall)
        {
            return DatabaseRestoreResult.ValidationFailed(
                "The selected protected backup is compatible with this Windows user, but encrypted backup restore is not activated yet. Restore configuration only and re-enter camera passwords.");
        }

        return DatabaseRestoreResult.Ok();
    }

}
