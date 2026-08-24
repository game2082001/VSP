using Microsoft.Data.Sqlite;
using VSP.Infrastructure.Security;

namespace VSP.Infrastructure.Database;

internal sealed class DatabaseRestorePreflight
{
    private const int ProtectionVersion = 1;
    private readonly ICameraCredentialProtector _protector;

    public DatabaseRestorePreflight(ICameraCredentialProtector protector)
    {
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public DatabaseRestorePreflightResult Check(string sourceFilePath, string? liveDatabasePath)
    {
        if (!File.Exists(sourceFilePath))
        {
            return DatabaseRestorePreflightResult.ValidationFailed("The selected file does not exist.");
        }

        if (new FileInfo(sourceFilePath).Length == 0)
        {
            return DatabaseRestorePreflightResult.ValidationFailed("The selected file is empty.");
        }

        if (liveDatabasePath is not null &&
            string.Equals(Path.GetFullPath(sourceFilePath), Path.GetFullPath(liveDatabasePath), StringComparison.OrdinalIgnoreCase))
        {
            return DatabaseRestorePreflightResult.ValidationFailed(
                "The selected file is the active database. Choose a different backup file to restore from.");
        }

        if (HasNonEmptySidecar(sourceFilePath))
        {
            return DatabaseRestorePreflightResult.ValidationFailed(
                "The selected file has an active WAL or journal sidecar and cannot be safely restored.");
        }

        try
        {
            using var connection = OpenReadOnly(sourceFilePath);
            var integrityResult = ReadIntegrityResult(connection);
            if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseRestorePreflightResult.ValidationFailed(
                    "The selected file failed a database integrity check and cannot be used to restore.");
            }

            var schemaVersion = ReadSchemaVersion(connection);
            return schemaVersion switch
            {
                DatabaseSchemaVersion.LegacyPlaintextCredentials => CheckLegacyPlaintext(sourceFilePath, connection),
                DatabaseSchemaVersion.CurrentUserProtectedCredentials => CheckCurrentUserProtected(connection),
                _ => DatabaseRestorePreflightResult.ValidationFailed(
                    "The selected file uses an unsupported VSP database schema version.")
            };
        }
        catch (SqliteException)
        {
            return DatabaseRestorePreflightResult.ValidationFailed(
                "The selected file could not be opened as a SQLite database.");
        }
        catch (InvalidDataException ex)
        {
            return DatabaseRestorePreflightResult.ValidationFailed(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return DatabaseRestorePreflightResult.ValidationFailed(ex.Message);
        }
        catch (Exception)
        {
            return DatabaseRestorePreflightResult.ValidationFailed(
                "The selected file could not be opened as a SQLite database.");
        }
    }

    private DatabaseRestorePreflightResult CheckLegacyPlaintext(string sourceFilePath, SqliteConnection connection)
    {
        if (!HasTable(connection, "Camera"))
        {
            return DatabaseRestorePreflightResult.ValidationFailed(
                "The selected file is not a valid VSP database backup (missing the Camera table).");
        }

        if (!HasExactColumns(connection, "Camera", LegacyCameraColumns)
            || !HasExactColumns(connection, "User", UserColumns)
            || !HasExactApplicationSchema(connection, LegacySchemaObjects))
        {
            return DatabaseRestorePreflightResult.ValidationFailed(
                "The selected file is not a valid VSP database backup.");
        }

        var stagingPath = Path.Combine(
            Path.GetDirectoryName(sourceFilePath) ?? Path.GetTempPath(),
            $"vsp.restore-preflight.{Guid.NewGuid():N}.db");

        try
        {
            new CameraCredentialMigration(_protector).Stage(sourceFilePath, stagingPath);
            return DatabaseRestorePreflightResult.LegacyPlaintext();
        }
        finally
        {
            TryDelete(stagingPath);
        }
    }

    private DatabaseRestorePreflightResult CheckCurrentUserProtected(SqliteConnection connection)
    {
        if (!HasExactColumns(connection, "Camera", ProtectedCameraColumns)
            || !HasExactColumns(connection, "User", UserColumns)
            || !HasExactColumns(connection, "CredentialMigrationMetadata", MigrationMetadataColumns)
            || !HasExactApplicationSchema(connection, ProtectedSchemaObjects))
        {
            return DatabaseRestorePreflightResult.ValidationFailed(
                "The selected file is not a supported protected VSP database backup.");
        }

        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT PasswordProtected, PasswordProtectionVersion
FROM Camera
WHERE PasswordProtected IS NOT NULL OR PasswordProtectionVersion IS NOT NULL
ORDER BY Id;";
        using var reader = command.ExecuteReader();

        while (reader.Read())
        {
            if (reader.IsDBNull(0) || reader.IsDBNull(1) || reader.GetInt32(1) != ProtectionVersion)
            {
                return DatabaseRestorePreflightResult.ValidationFailed(
                    "The selected protected backup contains unsupported credential metadata.");
            }

            try
            {
                _protector.Unprotect((byte[])reader.GetValue(0));
            }
            catch
            {
                return DatabaseRestorePreflightResult.ValidationFailed(
                    "The selected protected backup contains camera credentials that cannot be decrypted by this Windows user.");
            }
        }

        return DatabaseRestorePreflightResult.CurrentUserProtectedCredentials();
    }

    private static SqliteConnection OpenReadOnly(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    private static string? ReadIntegrityResult(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        return command.ExecuteScalar() as string;
    }

    private static int ReadSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static bool HasExactColumns(SqliteConnection connection, string table, IReadOnlyList<string> expected)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info([{table.Replace("]", "]]", StringComparison.Ordinal)}]);";
        using var reader = command.ExecuteReader();
        var actual = new List<string>();
        while (reader.Read())
        {
            actual.Add(reader.GetString(1));
        }

        return actual.SequenceEqual(expected, StringComparer.Ordinal);
    }

    private static bool HasTable(SqliteConnection connection, string table)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name = $Name;";
        command.Parameters.AddWithValue("$Name", table);
        return string.Equals(command.ExecuteScalar() as string, table, StringComparison.Ordinal);
    }

    private static bool HasExactApplicationSchema(SqliteConnection connection, IReadOnlyList<string> expected)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT type || '|' || name || '|' || tbl_name
FROM sqlite_schema
WHERE type IN ('table', 'index', 'view', 'trigger')
  AND name NOT LIKE 'sqlite_%'
ORDER BY type, name, tbl_name;";
        using var reader = command.ExecuteReader();
        var actual = new List<string>();
        while (reader.Read())
        {
            actual.Add(reader.GetString(0));
        }

        return actual.SequenceEqual(expected, StringComparer.Ordinal);
    }

    private static bool HasNonEmptySidecar(string sourcePath)
    {
        return new[] { sourcePath + "-wal", sourcePath + "-journal" }
            .Any(path => File.Exists(path) && new FileInfo(path).Length > 0);
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
            // Best-effort cleanup only; the preflight verdict remains based on the source file.
        }
    }

    private static readonly string[] LegacyCameraColumns =
    {
        "Id", "Name", "IpAddress", "Brand", "ConnectionType", "Model", "Location",
        "HttpPort", "RtspPort", "SdkPort", "Username", "Password", "RtspUrl",
        "Status", "Recording", "CreateTime", "LastModifyTime"
    };

    private static readonly string[] ProtectedCameraColumns =
    {
        "Id", "Name", "IpAddress", "Brand", "ConnectionType", "Model", "Location",
        "HttpPort", "RtspPort", "SdkPort", "Username", "PasswordProtected",
        "PasswordProtectionVersion", "RtspUrl", "Status", "Recording", "CreateTime", "LastModifyTime"
    };

    private static readonly string[] UserColumns =
    {
        "Id", "Username", "PasswordHash", "PasswordSalt", "PasswordIterations",
        "Role", "MustChangePassword", "CreateTime", "LastModifyTime"
    };

    private static readonly string[] MigrationMetadataColumns =
    {
        "Id", "SourceSha256", "ProtectionProvider", "ProtectionScope", "ProtectionVersion"
    };

    private static readonly string[] LegacySchemaObjects =
    {
        "index|IX_User_Username|User",
        "table|Camera|Camera",
        "table|User|User"
    };

    private static readonly string[] ProtectedSchemaObjects =
    {
        "index|IX_User_Username|User",
        "table|Camera|Camera",
        "table|CredentialMigrationMetadata|CredentialMigrationMetadata",
        "table|User|User"
    };
}
