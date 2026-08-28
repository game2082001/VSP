using Microsoft.Data.Sqlite;
using VSP.Core.Security;

namespace VSP.Infrastructure.Database;

internal static class UserSchemaMigration
{
    public static void EnsureCurrent(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var columns = ReadUserColumns(connection);
        if (columns.Contains("IsEnabled") && columns.Contains("NormalizedUsername"))
        {
            ValidateLifecycleUsers(connection);
            EnsureIndexes(connection);
            SetCurrentVersion(connection);
            return;
        }

        if (!columns.SequenceEqual(CameraCredentialMigration.UserColumnsV1, StringComparer.Ordinal))
        {
            throw new InvalidDataException("The User table schema is not supported for lifecycle migration.");
        }

        var users = ReadLegacyUsers(connection);
        var duplicate = users
            .GroupBy(user => user.NormalizedUsername, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidDataException("The User table contains usernames that collide after normalization.");
        }

        using var transaction = connection.BeginTransaction();
        CreateLifecycleMigrationTable(connection, transaction);
        foreach (var user in users)
        {
            InsertLifecycleUser(connection, transaction, user);
        }

        ReplaceUserTable(connection, transaction);
        ValidateLifecycleUsers(connection, transaction);
        EnsureIndexes(connection, transaction);
        SetCurrentVersion(connection, transaction);
        transaction.Commit();
    }

    private static IReadOnlyList<string> ReadUserColumns(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info([User]);";
        using var reader = command.ExecuteReader();
        var columns = new List<string>();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static IReadOnlyList<LegacyUserRow> ReadLegacyUsers(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT Id, Username, PasswordHash, PasswordSalt, PasswordIterations,
       Role, MustChangePassword, CreateTime, LastModifyTime
FROM User
ORDER BY Id;";
        using var reader = command.ExecuteReader();
        var users = new List<LegacyUserRow>();
        while (reader.Read())
        {
            users.Add(new LegacyUserRow(
                Id: reader.GetString(0),
                Username: reader.GetString(1),
                NormalizedUsername: UsernameIdentity.Normalize(reader.GetString(1)),
                PasswordHash: reader.GetString(2),
                PasswordSalt: reader.GetString(3),
                PasswordIterations: reader.GetInt32(4),
                Role: reader.GetInt32(5),
                MustChangePassword: reader.GetInt32(6),
                CreateTime: reader.IsDBNull(7) ? null : reader.GetString(7),
                LastModifyTime: reader.IsDBNull(8) ? null : reader.GetString(8)));
        }

        return users;
    }

    private static void ValidateLifecycleUsers(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id, Username, NormalizedUsername FROM User ORDER BY Id;";
        using var reader = command.ExecuteReader();
        var normalizedUsernames = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read())
        {
            var username = reader.GetString(1);
            if (reader.IsDBNull(2))
            {
                throw new InvalidDataException("The User table contains an empty normalized username.");
            }

            var normalizedUsername = reader.GetString(2);
            if (normalizedUsername.Length == 0
                || !string.Equals(normalizedUsername, UsernameIdentity.Normalize(username), StringComparison.Ordinal)
                || !normalizedUsernames.Add(normalizedUsername))
            {
                throw new InvalidDataException("The User table contains invalid normalized username data.");
            }
        }
    }

    private static void CreateLifecycleMigrationTable(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
CREATE TABLE User_Pd003LifecycleMigration
(
    Id TEXT PRIMARY KEY,
    Username TEXT NOT NULL,
    PasswordHash TEXT NOT NULL,
    PasswordSalt TEXT NOT NULL,
    PasswordIterations INTEGER NOT NULL,
    Role INTEGER NOT NULL,
    MustChangePassword INTEGER NOT NULL DEFAULT 0,
    CreateTime TEXT,
    LastModifyTime TEXT,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    NormalizedUsername TEXT NOT NULL
);";
        command.ExecuteNonQuery();
    }

    private static void InsertLifecycleUser(SqliteConnection connection, SqliteTransaction transaction, LegacyUserRow user)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
INSERT INTO User_Pd003LifecycleMigration
(Id, Username, PasswordHash, PasswordSalt, PasswordIterations, Role,
 MustChangePassword, CreateTime, LastModifyTime, IsEnabled, NormalizedUsername)
VALUES
($Id, $Username, $PasswordHash, $PasswordSalt, $PasswordIterations, $Role,
 $MustChangePassword, $CreateTime, $LastModifyTime, 1, $NormalizedUsername);";
        command.Parameters.AddWithValue("$Id", user.Id);
        command.Parameters.AddWithValue("$Username", user.Username);
        command.Parameters.AddWithValue("$PasswordHash", user.PasswordHash);
        command.Parameters.AddWithValue("$PasswordSalt", user.PasswordSalt);
        command.Parameters.AddWithValue("$PasswordIterations", user.PasswordIterations);
        command.Parameters.AddWithValue("$Role", user.Role);
        command.Parameters.AddWithValue("$MustChangePassword", user.MustChangePassword);
        command.Parameters.AddWithValue("$CreateTime", user.CreateTime is null ? DBNull.Value : user.CreateTime);
        command.Parameters.AddWithValue("$LastModifyTime", user.LastModifyTime is null ? DBNull.Value : user.LastModifyTime);
        command.Parameters.AddWithValue("$NormalizedUsername", user.NormalizedUsername);
        command.ExecuteNonQuery();
    }

    private static void ReplaceUserTable(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
DROP INDEX IF EXISTS IX_User_Username;
DROP TABLE User;
ALTER TABLE User_Pd003LifecycleMigration RENAME TO User;";
        command.ExecuteNonQuery();
    }

    private static void EnsureIndexes(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
CREATE UNIQUE INDEX IF NOT EXISTS IX_User_Username ON User (Username);
CREATE UNIQUE INDEX IF NOT EXISTS IX_User_NormalizedUsername ON User (NormalizedUsername);";
        command.ExecuteNonQuery();
    }

    private static void SetCurrentVersion(SqliteConnection connection, SqliteTransaction? transaction = null)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA user_version = {DatabaseSchemaVersion.UserLifecycleFoundation};";
        command.ExecuteNonQuery();
    }

    private sealed record LegacyUserRow(
        string Id,
        string Username,
        string NormalizedUsername,
        string PasswordHash,
        string PasswordSalt,
        int PasswordIterations,
        int Role,
        int MustChangePassword,
        string? CreateTime,
        string? LastModifyTime);
}
