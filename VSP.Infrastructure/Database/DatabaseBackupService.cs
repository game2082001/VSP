using Microsoft.Data.Sqlite;
using VSP.Core.Logging;

namespace VSP.Infrastructure.Database;

/// <summary>
/// Creates a database backup via the SQLite Online Backup API (Epic-017 §3.3) -- a
/// transactionally-consistent snapshot of the live database, safe to call even while a
/// short-lived connection elsewhere in the app is mid-transaction (e.g. an active recording).
/// Never moves or modifies anything other than the user-chosen destination file.
/// </summary>
public class DatabaseBackupService
{
    private readonly DatabaseService _databaseService;

    public DatabaseBackupService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public DatabaseBackupResult Backup(string destinationFilePath)
    {
        try
        {
            using var source = _databaseService.CreateConnection();
            source.Open();

            using var destination = new SqliteConnection($"Data Source={destinationFilePath}");
            destination.Open();

            source.BackupDatabase(destination);

            return DatabaseBackupResult.Ok();
        }
        catch (Exception ex)
        {
            AppLog.Error($"Failed to back up the database to '{destinationFilePath}'.", ex);
            return DatabaseBackupResult.Failed(ex);
        }
        finally
        {
            // Disposing the connections above returns their native handles to Microsoft.Data.Sqlite's
            // process-wide pool rather than necessarily closing them -- without this, the destination
            // file can still be held open when a later plain file operation (e.g. a same-session
            // Restore reading this exact file as its source) tries to touch it.
            SqliteConnection.ClearAllPools();
        }
    }
}
