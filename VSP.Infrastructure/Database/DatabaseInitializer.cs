using Microsoft.Data.Sqlite;
using VSP.Infrastructure.Security;
using VSP.Infrastructure.SQLite;

namespace VSP.Infrastructure.Database;

/// <summary>
/// Deliberately does not log its own failure (unlike the other five Epic-015 components) --
/// the caller generates the single Error ID for a startup failure and logs the original
/// exception together with that ID in one line, so the ID and the exception are never split
/// across two separate log entries. See VSP.UI/App.xaml.cs's HandleDatabaseInitializationFailure.
/// </summary>
public class DatabaseInitializer
{
    private readonly DatabaseService _databaseService;

    public DatabaseInitializer(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public DatabaseInitializationResult Initialize()
    {
        try
        {
            ActivateCredentialSchemaIfRequired();

            using var connection = _databaseService.CreateConnection();

            connection.Open();

            CameraTable.Create(connection);
            UserTable.Create(connection);
            EnsureProtectedMetadata(connection);
            DefaultAdminSeeder.SeedIfEmpty(connection);

            return DatabaseInitializationResult.Ok();
        }
        catch (Exception ex)
        {
            return DatabaseInitializationResult.Failed(ex);
        }
    }

    private void ActivateCredentialSchemaIfRequired()
    {
        var databasePath = _databaseService.GetDatabaseFilePath();
        var stagingPath = Path.Combine(_databaseService.GetDatabaseDirectory(), "vsp.db.pd001d-staging");
        var legacyBackupPath = Path.Combine(_databaseService.GetDatabaseDirectory(), "vsp.db.pd001d-legacy");
        var migration = new CameraCredentialMigration(new DpapiCurrentUserCameraCredentialProtector());
        var state = migration.Inspect(databasePath, stagingPath);

        if (state == CameraCredentialMigrationState.SourceMissing
            || state == CameraCredentialMigrationState.SourceAlreadyProtected)
        {
            return;
        }

        if (state is CameraCredentialMigrationState.NotStarted
            or CameraCredentialMigrationState.InvalidStaging
            or CameraCredentialMigrationState.SourceChangedSinceStaging
            or CameraCredentialMigrationState.ReadyForActivation)
        {
            migration.Activate(databasePath, stagingPath, legacyBackupPath);
            return;
        }

        throw new InvalidDataException($"The database credential schema is not supported for startup migration: {state}.");
    }

    private static void EnsureProtectedMetadata(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $@"
CREATE TABLE IF NOT EXISTS CredentialMigrationMetadata
(
    Id INTEGER PRIMARY KEY CHECK (Id = 1),
    SourceSha256 BLOB NOT NULL,
    ProtectionProvider TEXT NOT NULL CHECK (ProtectionProvider = 'DPAPI'),
    ProtectionScope TEXT NOT NULL CHECK (ProtectionScope = 'CurrentUser'),
    ProtectionVersion INTEGER NOT NULL CHECK (ProtectionVersion = 1)
);
INSERT OR IGNORE INTO CredentialMigrationMetadata
(Id, SourceSha256, ProtectionProvider, ProtectionScope, ProtectionVersion)
VALUES (1, zeroblob(32), 'DPAPI', 'CurrentUser', 1);
PRAGMA user_version = {DatabaseSchemaVersion.CurrentUserProtectedCredentials};";
        command.ExecuteNonQuery();
    }
}
