using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using VSP.Core.Security;
using VSP.Infrastructure.Security;

namespace VSP.Infrastructure.Database;

/// <summary>
/// Builds, verifies, and atomically activates a protected database from a legacy plaintext source.
/// </summary>
internal sealed class CameraCredentialMigration
{
    internal const int ProtectionVersion = 1;
    private readonly ICameraCredentialProtector _protector;

    public CameraCredentialMigration(ICameraCredentialProtector protector)
    {
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public CameraCredentialMigrationOutcome Stage(string sourcePath, string stagingPath)
    {
        ValidatePaths(sourcePath, stagingPath);

        var state = Inspect(sourcePath, stagingPath);
        if (state == CameraCredentialMigrationState.ReadyForActivation)
        {
            return CameraCredentialMigrationOutcome.AlreadyStaged;
        }

        if (state is CameraCredentialMigrationState.SourceMissing
            or CameraCredentialMigrationState.SourceAlreadyProtected
            or CameraCredentialMigrationState.UnsupportedSource)
        {
            throw new InvalidOperationException($"Credential migration cannot start from state '{state}'.");
        }

        TryDelete(stagingPath);
        EnsureNoSourceSidecars(sourcePath);
        var sourceFingerprint = ComputeSha256(sourcePath);

        try
        {
            using (var source = OpenReadOnly(sourcePath))
            using (var staging = OpenReadWriteCreate(stagingPath))
            {
                ValidateLegacySource(source);
                InitializeStaging(staging);

                using var transaction = staging.BeginTransaction();
                CopyUsers(source, staging, transaction);
                CopyCameras(source, staging, transaction);
                WriteMigrationMetadata(staging, transaction, sourceFingerprint);
                SetSchemaVersion(staging, transaction);
                transaction.Commit();
            }

            EnsureNoSourceSidecars(sourcePath);
            if (!CryptographicOperations.FixedTimeEquals(sourceFingerprint, ComputeSha256(sourcePath)))
            {
                throw new IOException("The source database changed while credential migration was being staged.");
            }

            VerifyStagingDatabase(sourcePath, stagingPath, sourceFingerprint);
            return CameraCredentialMigrationOutcome.Staged;
        }
        catch
        {
            SqliteConnection.ClearAllPools();
            TryDelete(stagingPath);
            throw;
        }
    }

    public void StageConfigOnlyFromProtected(string sourcePath, string stagingPath)
    {
        ValidatePaths(sourcePath, stagingPath);

        TryDelete(stagingPath);
        EnsureNoSourceSidecars(sourcePath);
        var sourceFingerprint = ComputeSha256(sourcePath);

        try
        {
            using (var source = OpenReadOnly(sourcePath))
            using (var staging = OpenReadWriteCreate(stagingPath))
            {
                ValidateCurrentSchema(source);
                InitializeStaging(staging);

                using var transaction = staging.BeginTransaction();
                CopyUsers(source, staging, transaction);
                CopyProtectedCamerasAsConfigOnly(source, staging, transaction);
                WriteMigrationMetadata(staging, transaction, sourceFingerprint);
                SetSchemaVersion(staging, transaction);
                transaction.Commit();
            }

            EnsureNoSourceSidecars(sourcePath);
            if (!CryptographicOperations.FixedTimeEquals(sourceFingerprint, ComputeSha256(sourcePath)))
            {
                throw new IOException("The source database changed while config-only restore was being staged.");
            }

            VerifyConfigOnlyDatabase(sourcePath, stagingPath, sourceFingerprint);
        }
        catch
        {
            SqliteConnection.ClearAllPools();
            TryDelete(stagingPath);
            throw;
        }
    }

    public void Activate(string sourcePath, string stagingPath, string backupPath)
    {
        ValidatePaths(sourcePath, stagingPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupPath);

        SqliteConnection.ClearAllPools();
        PrepareSourceForAtomicMigration(sourcePath);
        Stage(sourcePath, stagingPath);

        var state = Inspect(sourcePath, stagingPath);
        if (state != CameraCredentialMigrationState.ReadyForActivation)
        {
            throw new InvalidOperationException($"Credential migration cannot activate from state '{state}'.");
        }

        TryDelete(backupPath);
        SqliteConnection.ClearAllPools();
        File.Replace(stagingPath, sourcePath, backupPath);

        using var activated = OpenReadOnly(sourcePath);
        ValidateCurrentSchema(activated);
        VerifyIntegrity(activated, "activated");
        DeleteRequired(backupPath);
    }

    public CameraCredentialMigrationState Inspect(string sourcePath, string stagingPath)
    {
        ValidatePaths(sourcePath, stagingPath);

        if (!File.Exists(sourcePath))
        {
            return CameraCredentialMigrationState.SourceMissing;
        }

        if (HasNonEmptySourceSidecar(sourcePath))
        {
            return CameraCredentialMigrationState.UnsupportedSource;
        }

        var sourceVersion = TryReadSchemaVersion(sourcePath);
        if (sourceVersion is DatabaseSchemaVersion.CurrentUserProtectedCredentials
            or DatabaseSchemaVersion.UserLifecycleFoundation)
        {
            return IsCurrentProtectedSchema(sourcePath)
                ? CameraCredentialMigrationState.SourceAlreadyProtected
                : CameraCredentialMigrationState.UnsupportedSource;
        }

        if (sourceVersion != DatabaseSchemaVersion.LegacyPlaintextCredentials || !IsLegacySourceSchema(sourcePath))
        {
            return CameraCredentialMigrationState.UnsupportedSource;
        }

        if (!File.Exists(stagingPath))
        {
            return CameraCredentialMigrationState.NotStarted;
        }

        try
        {
            var expectedFingerprint = ComputeSha256(sourcePath);
            var stagedFingerprint = ReadStagedSourceFingerprint(stagingPath);
            if (!CryptographicOperations.FixedTimeEquals(expectedFingerprint, stagedFingerprint))
            {
                return CameraCredentialMigrationState.SourceChangedSinceStaging;
            }

            VerifyStagingDatabase(sourcePath, stagingPath, expectedFingerprint);
            return CameraCredentialMigrationState.ReadyForActivation;
        }
        catch
        {
            return CameraCredentialMigrationState.InvalidStaging;
        }
    }

    private void CopyCameras(SqliteConnection source, SqliteConnection staging, SqliteTransaction transaction)
    {
        using var read = source.CreateCommand();
        read.CommandText = @"
SELECT Id, Name, IpAddress, Brand, ConnectionType, Model, Location,
       HttpPort, RtspPort, SdkPort, Username, Password, RtspUrl,
       Status, Recording, CreateTime, LastModifyTime
FROM Camera
ORDER BY Id;";

        using var reader = read.ExecuteReader();
        while (reader.Read())
        {
            var plaintext = reader.IsDBNull(11) ? string.Empty : reader.GetString(11);
            byte[]? protectedPassword = null;
            try
            {
                protectedPassword = plaintext.Length == 0 ? null : _protector.Protect(plaintext);

                using var insert = staging.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = @"
INSERT INTO Camera
(Id, Name, IpAddress, Brand, ConnectionType, Model, Location,
 HttpPort, RtspPort, SdkPort, Username, PasswordProtected, PasswordProtectionVersion,
 RtspUrl, Status, Recording, CreateTime, LastModifyTime)
VALUES
($Id, $Name, $IpAddress, $Brand, $ConnectionType, $Model, $Location,
 $HttpPort, $RtspPort, $SdkPort, $Username, $PasswordProtected, $PasswordProtectionVersion,
 $RtspUrl, $Status, $Recording, $CreateTime, $LastModifyTime);";

                AddValue(insert, "$Id", reader, 0);
                AddValue(insert, "$Name", reader, 1);
                AddValue(insert, "$IpAddress", reader, 2);
                AddValue(insert, "$Brand", reader, 3);
                AddValue(insert, "$ConnectionType", reader, 4);
                AddValue(insert, "$Model", reader, 5);
                AddValue(insert, "$Location", reader, 6);
                AddValue(insert, "$HttpPort", reader, 7);
                AddValue(insert, "$RtspPort", reader, 8);
                AddValue(insert, "$SdkPort", reader, 9);
                AddValue(insert, "$Username", reader, 10);
                insert.Parameters.AddWithValue("$PasswordProtected", (object?)protectedPassword ?? DBNull.Value);
                insert.Parameters.AddWithValue("$PasswordProtectionVersion", protectedPassword is null ? DBNull.Value : ProtectionVersion);
                AddValue(insert, "$RtspUrl", reader, 12);
                AddValue(insert, "$Status", reader, 13);
                AddValue(insert, "$Recording", reader, 14);
                AddValue(insert, "$CreateTime", reader, 15);
                AddValue(insert, "$LastModifyTime", reader, 16);
                insert.ExecuteNonQuery();
            }
            finally
            {
                if (protectedPassword is not null)
                {
                    CryptographicOperations.ZeroMemory(protectedPassword);
                }
            }
        }
    }

    private static void CopyUsers(SqliteConnection source, SqliteConnection staging, SqliteTransaction transaction)
    {
        var hasLifecycleColumns = HasExactColumns(source, "User", UserColumns);
        using var read = source.CreateCommand();
        read.CommandText = hasLifecycleColumns
            ? @"
SELECT Id, Username, NormalizedUsername, PasswordHash, PasswordSalt, PasswordIterations,
       Role, MustChangePassword, IsEnabled, CreateTime, LastModifyTime
FROM User
ORDER BY Id;"
            : @"
SELECT Id, Username, PasswordHash, PasswordSalt, PasswordIterations,
       Role, MustChangePassword, CreateTime, LastModifyTime
FROM User
ORDER BY Id;";

        using var reader = read.ExecuteReader();
        while (reader.Read())
        {
            using var insert = staging.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
INSERT INTO User
(Id, Username, NormalizedUsername, PasswordHash, PasswordSalt, PasswordIterations,
 Role, MustChangePassword, IsEnabled, CreateTime, LastModifyTime)
VALUES
($Id, $Username, $NormalizedUsername, $PasswordHash, $PasswordSalt, $PasswordIterations,
 $Role, $MustChangePassword, $IsEnabled, $CreateTime, $LastModifyTime);";

            if (hasLifecycleColumns)
            {
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    AddValue(insert, "$" + reader.GetName(index), reader, index);
                }
            }
            else
            {
                AddValue(insert, "$Id", reader, 0);
                AddValue(insert, "$Username", reader, 1);
                insert.Parameters.AddWithValue("$NormalizedUsername", UsernameIdentity.Normalize(reader.GetString(1)));
                AddValue(insert, "$PasswordHash", reader, 2);
                AddValue(insert, "$PasswordSalt", reader, 3);
                AddValue(insert, "$PasswordIterations", reader, 4);
                AddValue(insert, "$Role", reader, 5);
                AddValue(insert, "$MustChangePassword", reader, 6);
                insert.Parameters.AddWithValue("$IsEnabled", 1);
                AddValue(insert, "$CreateTime", reader, 7);
                AddValue(insert, "$LastModifyTime", reader, 8);
            }

            insert.ExecuteNonQuery();
        }
    }

    private static void CopyProtectedCamerasAsConfigOnly(
        SqliteConnection source,
        SqliteConnection staging,
        SqliteTransaction transaction)
    {
        using var read = source.CreateCommand();
        read.CommandText = @"
SELECT Id, Name, IpAddress, Brand, ConnectionType, Model, Location,
       HttpPort, RtspPort, SdkPort, Username, RtspUrl,
       Status, Recording, CreateTime, LastModifyTime
FROM Camera
ORDER BY Id;";

        using var reader = read.ExecuteReader();
        while (reader.Read())
        {
            using var insert = staging.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = @"
INSERT INTO Camera
(Id, Name, IpAddress, Brand, ConnectionType, Model, Location,
 HttpPort, RtspPort, SdkPort, Username, PasswordProtected, PasswordProtectionVersion,
 RtspUrl, Status, Recording, CreateTime, LastModifyTime)
VALUES
($Id, $Name, $IpAddress, $Brand, $ConnectionType, $Model, $Location,
 $HttpPort, $RtspPort, $SdkPort, $Username, NULL, NULL,
 $RtspUrl, $Status, $Recording, $CreateTime, $LastModifyTime);";

            for (var index = 0; index <= 10; index++)
            {
                AddValue(insert, "$" + reader.GetName(index), reader, index);
            }

            AddValue(insert, "$RtspUrl", reader, 11);
            AddValue(insert, "$Status", reader, 12);
            AddValue(insert, "$Recording", reader, 13);
            AddValue(insert, "$CreateTime", reader, 14);
            AddValue(insert, "$LastModifyTime", reader, 15);
            insert.ExecuteNonQuery();
        }
    }

    private void VerifyStagingDatabase(string sourcePath, string stagingPath, byte[] expectedSourceFingerprint)
    {
        using var staging = OpenReadOnly(stagingPath);
        ValidateCurrentSchema(staging);

        VerifyIntegrity(staging, "staged");

        var recordedFingerprint = ReadStagedSourceFingerprint(staging);
        if (!CryptographicOperations.FixedTimeEquals(expectedSourceFingerprint, recordedFingerprint))
        {
            throw new InvalidDataException("The staged database does not match the source database fingerprint.");
        }

        using var source = OpenReadOnly(sourcePath);
        VerifyCameraRows(source, staging);
        VerifyUserRows(source, staging);
    }

    private void VerifyCameraRows(SqliteConnection source, SqliteConnection staging)
    {
        using var sourceCommand = source.CreateCommand();
        sourceCommand.CommandText = @"
SELECT Id, Name, IpAddress, Brand, ConnectionType, Model, Location,
       HttpPort, RtspPort, SdkPort, Username, Password, RtspUrl,
       Status, Recording, CreateTime, LastModifyTime
FROM Camera
ORDER BY Id;";
        using var sourceReader = sourceCommand.ExecuteReader();

        using var stagedCommand = staging.CreateCommand();
        stagedCommand.CommandText = @"
SELECT Id, Name, IpAddress, Brand, ConnectionType, Model, Location,
       HttpPort, RtspPort, SdkPort, Username, PasswordProtected, PasswordProtectionVersion,
       RtspUrl, Status, Recording, CreateTime, LastModifyTime
FROM Camera
ORDER BY Id;";
        using var stagedReader = stagedCommand.ExecuteReader();

        while (sourceReader.Read())
        {
            if (!stagedReader.Read())
            {
                throw new InvalidDataException("The staged camera set does not match the source database.");
            }

            for (var index = 0; index <= 10; index++)
            {
                EnsureValuesMatch(sourceReader, index, stagedReader, index, "camera");
            }

            var plaintext = sourceReader.IsDBNull(11) ? string.Empty : sourceReader.GetString(11);
            if (plaintext.Length == 0)
            {
                if (!stagedReader.IsDBNull(11) || !stagedReader.IsDBNull(12))
                {
                    throw new InvalidDataException("An empty legacy credential was not represented as empty.");
                }
            }
            else
            {
                if (stagedReader.IsDBNull(11) || stagedReader.GetInt32(12) != ProtectionVersion)
                {
                    throw new InvalidDataException("A staged credential is missing protection metadata.");
                }

                var protectedEnvelope = (byte[])stagedReader.GetValue(11);
                if (!string.Equals(plaintext, _protector.Unprotect(protectedEnvelope), StringComparison.Ordinal))
                {
                    throw new CryptographicException("A staged credential failed round-trip verification.");
                }
            }

            for (var sourceIndex = 12; sourceIndex <= 16; sourceIndex++)
            {
                EnsureValuesMatch(sourceReader, sourceIndex, stagedReader, sourceIndex + 1, "camera");
            }
        }

        if (stagedReader.Read())
        {
            throw new InvalidDataException("The staged database contains an unexpected camera row.");
        }
    }

    private static void VerifyUserRows(SqliteConnection source, SqliteConnection staging)
    {
        using var sourceCommand = source.CreateCommand();
        var sourceHasLifecycleColumns = HasExactColumns(source, "User", UserColumns);
        sourceCommand.CommandText = sourceHasLifecycleColumns
            ? @"
SELECT Id, Username, NormalizedUsername, PasswordHash, PasswordSalt, PasswordIterations,
       Role, MustChangePassword, IsEnabled, CreateTime, LastModifyTime
FROM User
ORDER BY Id;"
            : @"
SELECT Id, Username, PasswordHash, PasswordSalt, PasswordIterations,
       Role, MustChangePassword, CreateTime, LastModifyTime
FROM User
ORDER BY Id;";
        using var sourceReader = sourceCommand.ExecuteReader();

        using var stagedCommand = staging.CreateCommand();
        stagedCommand.CommandText = @"
SELECT Id, Username, NormalizedUsername, PasswordHash, PasswordSalt, PasswordIterations,
       Role, MustChangePassword, IsEnabled, CreateTime, LastModifyTime
FROM User
ORDER BY Id;";
        using var stagedReader = stagedCommand.ExecuteReader();

        while (sourceReader.Read())
        {
            if (!stagedReader.Read())
            {
                throw new InvalidDataException("The staged user set does not match the source database.");
            }

            if (sourceHasLifecycleColumns)
            {
                for (var index = 0; index < sourceReader.FieldCount; index++)
                {
                    EnsureValuesMatch(sourceReader, index, stagedReader, index, "user");
                }
            }
            else
            {
                EnsureValuesMatch(sourceReader, 0, stagedReader, 0, "user");
                EnsureValuesMatch(sourceReader, 1, stagedReader, 1, "user");
                if (!string.Equals(UsernameIdentity.Normalize(sourceReader.GetString(1)), stagedReader.GetString(2), StringComparison.Ordinal))
                {
                    throw new InvalidDataException("A staged user has an unexpected normalized username.");
                }

                for (var sourceIndex = 2; sourceIndex <= 6; sourceIndex++)
                {
                    EnsureValuesMatch(sourceReader, sourceIndex, stagedReader, sourceIndex + 1, "user");
                }

                if (stagedReader.GetInt32(8) != 1)
                {
                    throw new InvalidDataException("A migrated user was not enabled.");
                }

                EnsureValuesMatch(sourceReader, 7, stagedReader, 9, "user");
                EnsureValuesMatch(sourceReader, 8, stagedReader, 10, "user");
            }
        }

        if (stagedReader.Read())
        {
            throw new InvalidDataException("The staged database contains an unexpected user row.");
        }
    }

    private static void VerifyConfigOnlyDatabase(string sourcePath, string stagingPath, byte[] expectedSourceFingerprint)
    {
        using var staging = OpenReadOnly(stagingPath);
        ValidateCurrentSchema(staging);
        VerifyIntegrity(staging, "config-only staged");

        var recordedFingerprint = ReadStagedSourceFingerprint(staging);
        if (!CryptographicOperations.FixedTimeEquals(expectedSourceFingerprint, recordedFingerprint))
        {
            throw new InvalidDataException("The config-only staged database does not match the source database fingerprint.");
        }

        using var source = OpenReadOnly(sourcePath);
        VerifyConfigOnlyCameraRows(source, staging);
        VerifyUserRows(source, staging);
    }

    private static void VerifyConfigOnlyCameraRows(SqliteConnection source, SqliteConnection staging)
    {
        using var sourceCommand = source.CreateCommand();
        sourceCommand.CommandText = @"
SELECT Id, Name, IpAddress, Brand, ConnectionType, Model, Location,
       HttpPort, RtspPort, SdkPort, Username, RtspUrl,
       Status, Recording, CreateTime, LastModifyTime
FROM Camera
ORDER BY Id;";
        using var sourceReader = sourceCommand.ExecuteReader();

        using var stagedCommand = staging.CreateCommand();
        stagedCommand.CommandText = @"
SELECT Id, Name, IpAddress, Brand, ConnectionType, Model, Location,
       HttpPort, RtspPort, SdkPort, Username, PasswordProtected, PasswordProtectionVersion,
       RtspUrl, Status, Recording, CreateTime, LastModifyTime
FROM Camera
ORDER BY Id;";
        using var stagedReader = stagedCommand.ExecuteReader();

        while (sourceReader.Read())
        {
            if (!stagedReader.Read())
            {
                throw new InvalidDataException("The config-only staged camera set does not match the source database.");
            }

            for (var index = 0; index <= 10; index++)
            {
                EnsureValuesMatch(sourceReader, index, stagedReader, index, "camera");
            }

            if (!stagedReader.IsDBNull(11) || !stagedReader.IsDBNull(12))
            {
                throw new InvalidDataException("A config-only staged credential was not cleared.");
            }

            for (var sourceIndex = 11; sourceIndex <= 15; sourceIndex++)
            {
                EnsureValuesMatch(sourceReader, sourceIndex, stagedReader, sourceIndex + 2, "camera");
            }
        }

        if (stagedReader.Read())
        {
            throw new InvalidDataException("The config-only staged database contains an unexpected camera row.");
        }
    }

    private static void EnsureValuesMatch(
        SqliteDataReader source,
        int sourceOrdinal,
        SqliteDataReader staging,
        int stagingOrdinal,
        string entityName)
    {
        var sourceIsNull = source.IsDBNull(sourceOrdinal);
        var stagingIsNull = staging.IsDBNull(stagingOrdinal);
        if (sourceIsNull != stagingIsNull
            || (!sourceIsNull && !Equals(source.GetValue(sourceOrdinal), staging.GetValue(stagingOrdinal))))
        {
            throw new InvalidDataException($"A staged {entityName} value does not match the source database.");
        }
    }

    private static void InitializeStaging(SqliteConnection staging)
    {
        using var command = staging.CreateCommand();
        command.CommandText = @"
PRAGMA journal_mode = DELETE;
PRAGMA synchronous = FULL;
PRAGMA secure_delete = ON;

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
    PasswordProtected BLOB,
    PasswordProtectionVersion INTEGER,
    RtspUrl TEXT,
    Status INTEGER,
    Recording INTEGER,
    CreateTime TEXT,
    LastModifyTime TEXT,
    CHECK ((PasswordProtected IS NULL AND PasswordProtectionVersion IS NULL)
        OR (PasswordProtected IS NOT NULL AND PasswordProtectionVersion = 1))
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
    LastModifyTime TEXT,
    IsEnabled INTEGER NOT NULL DEFAULT 1,
    NormalizedUsername TEXT NOT NULL
);
CREATE UNIQUE INDEX IX_User_Username ON User (Username);
CREATE UNIQUE INDEX IX_User_NormalizedUsername ON User (NormalizedUsername);

CREATE TABLE CredentialMigrationMetadata
(
    Id INTEGER PRIMARY KEY CHECK (Id = 1),
    SourceSha256 BLOB NOT NULL,
    ProtectionProvider TEXT NOT NULL CHECK (ProtectionProvider = 'DPAPI'),
    ProtectionScope TEXT NOT NULL CHECK (ProtectionScope = 'CurrentUser'),
    ProtectionVersion INTEGER NOT NULL CHECK (ProtectionVersion = 1)
);";
        command.ExecuteNonQuery();
    }

    private static void WriteMigrationMetadata(SqliteConnection staging, SqliteTransaction transaction, byte[] fingerprint)
    {
        using var command = staging.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
INSERT INTO CredentialMigrationMetadata
(Id, SourceSha256, ProtectionProvider, ProtectionScope, ProtectionVersion)
VALUES (1, $SourceSha256, 'DPAPI', 'CurrentUser', 1);";
        command.Parameters.AddWithValue("$SourceSha256", fingerprint);
        command.ExecuteNonQuery();
    }

    private static void SetSchemaVersion(SqliteConnection staging, SqliteTransaction transaction)
    {
        using var command = staging.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA user_version = {DatabaseSchemaVersion.UserLifecycleFoundation};";
        command.ExecuteNonQuery();
    }

    private static void PrepareSourceForAtomicMigration(string sourcePath)
    {
        EnsureNoSourceSidecars(sourcePath);

        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = sourcePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString());
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = @"
PRAGMA locking_mode = EXCLUSIVE;
PRAGMA wal_checkpoint(TRUNCATE);
PRAGMA journal_mode = DELETE;";
        command.ExecuteNonQuery();

        EnsureNoSourceSidecars(sourcePath);
    }

    private static void VerifyIntegrity(SqliteConnection connection, string databaseName)
    {
        using var integrity = connection.CreateCommand();
        integrity.CommandText = "PRAGMA integrity_check;";
        if (!string.Equals(integrity.ExecuteScalar() as string, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"The {databaseName} database failed its integrity check.");
        }
    }

    private static void ValidateLegacySource(SqliteConnection source)
    {
        if (ReadSchemaVersion(source) != DatabaseSchemaVersion.LegacyPlaintextCredentials
            || !HasExactColumns(source, "Camera", LegacyCameraColumns)
            || !HasExactColumns(source, "User", UserColumnsV1)
            || !HasExactApplicationSchema(source, LegacySchemaObjects))
        {
            throw new InvalidDataException("The source database schema is not a supported legacy VSP schema.");
        }
    }

    private static void ValidateCurrentSchema(SqliteConnection connection)
    {
        var schemaVersion = ReadSchemaVersion(connection);
        var isProtectedV1 = schemaVersion == DatabaseSchemaVersion.CurrentUserProtectedCredentials
            && HasExactColumns(connection, "Camera", ProtectedCameraColumns)
            && HasExactColumns(connection, "User", UserColumnsV1)
            && HasExactColumns(connection, "CredentialMigrationMetadata", MigrationMetadataColumns)
            && HasExactApplicationSchema(connection, ProtectedSchemaObjectsV1);
        var isUserLifecycle = schemaVersion == DatabaseSchemaVersion.UserLifecycleFoundation
            && HasExactColumns(connection, "Camera", ProtectedCameraColumns)
            && HasExactColumns(connection, "User", UserColumns)
            && HasExactColumns(connection, "CredentialMigrationMetadata", MigrationMetadataColumns)
            && HasExactApplicationSchema(connection, ProtectedSchemaObjects);
        if (!isProtectedV1 && !isUserLifecycle)
        {
            throw new InvalidDataException("The staged database schema is not a supported protected VSP schema.");
        }
    }

    private static bool IsLegacySourceSchema(string path)
    {
        try
        {
            using var connection = OpenReadOnly(path);
            ValidateLegacySource(connection);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsCurrentProtectedSchema(string path)
    {
        try
        {
            using var connection = OpenReadOnly(path);
            ValidateCurrentSchema(connection);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int TryReadSchemaVersion(string path)
    {
        try
        {
            using var connection = OpenReadOnly(path);
            return ReadSchemaVersion(connection);
        }
        catch
        {
            return -1;
        }
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

    private static byte[] ReadStagedSourceFingerprint(string stagingPath)
    {
        using var connection = OpenReadOnly(stagingPath);
        return ReadStagedSourceFingerprint(connection);
    }

    private static byte[] ReadStagedSourceFingerprint(SqliteConnection connection)
    {
        ValidateCurrentSchema(connection);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT SourceSha256 FROM CredentialMigrationMetadata WHERE Id = 1;";
        return command.ExecuteScalar() as byte[]
            ?? throw new InvalidDataException("The staged database has no source fingerprint.");
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

    private static SqliteConnection OpenReadWriteCreate(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    private static byte[] ComputeSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return SHA256.HashData(stream);
    }

    private static void AddValue(SqliteCommand command, string name, SqliteDataReader reader, int ordinal)
    {
        command.Parameters.AddWithValue(name, reader.IsDBNull(ordinal) ? DBNull.Value : reader.GetValue(ordinal));
    }

    private static void ValidatePaths(string sourcePath, string stagingPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(stagingPath);
        if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(stagingPath), StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Source and staging paths must be different.", nameof(stagingPath));
        }
    }

    private static void EnsureNoSourceSidecars(string sourcePath)
    {
        if (HasNonEmptySourceSidecar(sourcePath))
        {
            throw new InvalidOperationException(
                "The source database has an active WAL or journal sidecar and cannot be staged safely.");
        }
    }

    private static bool HasNonEmptySourceSidecar(string sourcePath)
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
            // The original exception remains authoritative; cleanup is best effort.
        }
    }

    internal static void DeleteRequired(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    internal static readonly string[] LegacyCameraColumns =
    {
        "Id", "Name", "IpAddress", "Brand", "ConnectionType", "Model", "Location",
        "HttpPort", "RtspPort", "SdkPort", "Username", "Password", "RtspUrl",
        "Status", "Recording", "CreateTime", "LastModifyTime"
    };

    internal static readonly string[] ProtectedCameraColumns =
    {
        "Id", "Name", "IpAddress", "Brand", "ConnectionType", "Model", "Location",
        "HttpPort", "RtspPort", "SdkPort", "Username", "PasswordProtected",
        "PasswordProtectionVersion", "RtspUrl", "Status", "Recording", "CreateTime", "LastModifyTime"
    };

    internal static readonly string[] UserColumnsV1 =
    {
        "Id", "Username", "PasswordHash", "PasswordSalt", "PasswordIterations",
        "Role", "MustChangePassword", "CreateTime", "LastModifyTime"
    };

    internal static readonly string[] UserColumns =
    {
        "Id", "Username", "PasswordHash", "PasswordSalt", "PasswordIterations",
        "Role", "MustChangePassword", "CreateTime", "LastModifyTime",
        "IsEnabled", "NormalizedUsername"
    };

    internal static readonly string[] MigrationMetadataColumns =
    {
        "Id", "SourceSha256", "ProtectionProvider", "ProtectionScope", "ProtectionVersion"
    };

    internal static readonly string[] LegacySchemaObjects =
    {
        "index|IX_User_Username|User",
        "table|Camera|Camera",
        "table|User|User"
    };

    internal static readonly string[] ProtectedSchemaObjects =
    {
        "index|IX_User_NormalizedUsername|User",
        "index|IX_User_Username|User",
        "table|Camera|Camera",
        "table|CredentialMigrationMetadata|CredentialMigrationMetadata",
        "table|User|User"
    };

    internal static readonly string[] ProtectedSchemaObjectsV1 =
    {
        "index|IX_User_Username|User",
        "table|Camera|Camera",
        "table|CredentialMigrationMetadata|CredentialMigrationMetadata",
        "table|User|User"
    };
}
