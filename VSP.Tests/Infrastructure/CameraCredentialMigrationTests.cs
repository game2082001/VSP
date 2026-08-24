using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using VSP.Domain.Entities;
using VSP.Domain.Enums;
using VSP.Infrastructure.Database;
using VSP.Infrastructure.Repositories;
using VSP.Infrastructure.Security;
using Xunit;

namespace VSP.Tests.Infrastructure;

public class CameraCredentialMigrationTests : IDisposable
{
    private const string FirstSecret = "migration-camera-secret-alpha";
    private const string SecondSecret = "migration-camera-secret-beta";
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"vsp-credential-migration-{Guid.NewGuid():N}");

    [Fact]
    public void Inspect_FreshLegacyDatabase_ReportsNotStarted()
    {
        var (sourcePath, stagingPath) = CreateLegacyDatabase();
        var migration = CreateMigration();

        Assert.Equal(CameraCredentialMigrationState.NotStarted, migration.Inspect(sourcePath, stagingPath));
    }

    [Fact]
    public void Stage_BuildsVerifiedProtectedDatabaseWithoutChangingSource()
    {
        var (sourcePath, stagingPath) = CreateLegacyDatabase();
        var sourceBefore = SHA256.HashData(File.ReadAllBytes(sourcePath));
        var migration = CreateMigration();

        var outcome = migration.Stage(sourcePath, stagingPath);

        Assert.Equal(CameraCredentialMigrationOutcome.Staged, outcome);
        Assert.Equal(sourceBefore, SHA256.HashData(File.ReadAllBytes(sourcePath)));
        Assert.Equal(CameraCredentialMigrationState.ReadyForActivation, migration.Inspect(sourcePath, stagingPath));
        AssertProtectedStaging(stagingPath);

        var stagingBytes = File.ReadAllBytes(stagingPath);
        Assert.False(ContainsSequence(stagingBytes, System.Text.Encoding.UTF8.GetBytes(FirstSecret)));
        Assert.False(ContainsSequence(stagingBytes, System.Text.Encoding.UTF8.GetBytes(SecondSecret)));
        Assert.False(File.Exists(stagingPath + "-wal"));
        Assert.False(File.Exists(stagingPath + "-journal"));
    }

    [Fact]
    public void Stage_WhenRepeated_IsIdempotentAndDoesNotRewriteStagingDatabase()
    {
        var (sourcePath, stagingPath) = CreateLegacyDatabase();
        var migration = CreateMigration();
        migration.Stage(sourcePath, stagingPath);
        var stagingBefore = SHA256.HashData(File.ReadAllBytes(stagingPath));

        var secondOutcome = migration.Stage(sourcePath, stagingPath);
        var thirdOutcome = migration.Stage(sourcePath, stagingPath);

        Assert.Equal(CameraCredentialMigrationOutcome.AlreadyStaged, secondOutcome);
        Assert.Equal(CameraCredentialMigrationOutcome.AlreadyStaged, thirdOutcome);
        Assert.Equal(stagingBefore, SHA256.HashData(File.ReadAllBytes(stagingPath)));
    }

    [Fact]
    public void Inspect_WhenStagingWasCorrupted_ReportsRecognizableCrashState()
    {
        var (sourcePath, stagingPath) = CreateLegacyDatabase();
        var migration = CreateMigration();
        migration.Stage(sourcePath, stagingPath);
        File.WriteAllText(stagingPath, "interrupted migration output");

        Assert.Equal(CameraCredentialMigrationState.InvalidStaging, migration.Inspect(sourcePath, stagingPath));
    }

    [Fact]
    public void Inspect_WhenSourceChangedAfterStaging_ReportsSourceDrift()
    {
        var (sourcePath, stagingPath) = CreateLegacyDatabase();
        var migration = CreateMigration();
        migration.Stage(sourcePath, stagingPath);

        using (var connection = Open(sourcePath))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE Camera SET Location = 'Changed after staging' WHERE Name = 'First';";
            command.ExecuteNonQuery();
        }

        Assert.Equal(CameraCredentialMigrationState.SourceChangedSinceStaging, migration.Inspect(sourcePath, stagingPath));
    }

    [Fact]
    public void Stage_WhenProtectionFails_RemovesPartialStagingAndLeavesSourceUntouched()
    {
        var (sourcePath, stagingPath) = CreateLegacyDatabase();
        var sourceBefore = File.ReadAllBytes(sourcePath);
        var migration = new CameraCredentialMigration(new ThrowingProtector(SecondSecret));

        Assert.Throws<CryptographicException>(() => migration.Stage(sourcePath, stagingPath));

        Assert.False(File.Exists(stagingPath));
        Assert.Equal(sourceBefore, File.ReadAllBytes(sourcePath));
        Assert.Equal(CameraCredentialMigrationState.NotStarted, migration.Inspect(sourcePath, stagingPath));
    }

    [Fact]
    public void DatabaseInitializer_RemainsLegacyAndIsNotConnectedToCredentialMigration()
    {
        Directory.CreateDirectory(_tempDirectory);
        var databaseService = new DatabaseService(_tempDirectory);

        var result = new DatabaseInitializer(databaseService).Initialize();

        Assert.True(result.Success);
        using var connection = databaseService.CreateConnection();
        connection.Open();
        using var version = connection.CreateCommand();
        version.CommandText = "PRAGMA user_version;";
        Assert.Equal(DatabaseSchemaVersion.LegacyPlaintextCredentials, Convert.ToInt32(version.ExecuteScalar()));

        using var columns = connection.CreateCommand();
        columns.CommandText = "PRAGMA table_info(Camera);";
        using var reader = columns.ExecuteReader();
        var names = new List<string>();
        while (reader.Read())
        {
            names.Add(reader.GetString(1));
        }

        Assert.Contains("Password", names);
        Assert.DoesNotContain("PasswordProtected", names);
    }

    [Fact]
    public void Inspect_UnknownOrMixedSchema_FailsClosed()
    {
        Directory.CreateDirectory(_tempDirectory);
        var sourcePath = Path.Combine(_tempDirectory, "unknown.db");
        var stagingPath = Path.Combine(_tempDirectory, "unknown.staging.db");
        using (var connection = Open(sourcePath))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
CREATE TABLE Camera (Id TEXT, Password TEXT, PasswordProtected BLOB);
PRAGMA user_version = 77;";
            command.ExecuteNonQuery();
        }

        Assert.Equal(CameraCredentialMigrationState.UnsupportedSource, CreateMigration().Inspect(sourcePath, stagingPath));
        Assert.Throws<InvalidOperationException>(() => CreateMigration().Stage(sourcePath, stagingPath));
    }

    private (string SourcePath, string StagingPath) CreateLegacyDatabase()
    {
        Directory.CreateDirectory(_tempDirectory);
        var databaseService = new DatabaseService(_tempDirectory);
        var initialization = new DatabaseInitializer(databaseService).Initialize();
        Assert.True(initialization.Success);

        var repository = new SQLiteCameraRepository(databaseService);
        repository.Add(CreateCamera("First", FirstSecret));
        repository.Add(CreateCamera("Second", SecondSecret));
        repository.Add(CreateCamera("No password", string.Empty));
        SqliteConnection.ClearAllPools();

        return (databaseService.GetDatabaseFilePath(), Path.Combine(_tempDirectory, "vsp.credential-migration.db"));
    }

    private static Camera CreateCamera(string name, string password)
    {
        var now = DateTime.UtcNow;
        return new Camera
        {
            Name = name,
            IpAddress = "192.0.2.10",
            Brand = CameraBrand.ONVIF,
            ConnectionType = DeviceConnectionType.ONVIF,
            Model = "Test Model",
            Location = "Test Lab",
            Username = "camera-user",
            Password = password,
            RtspUrl = "rtsp://192.0.2.10/stream",
            CreateTime = now,
            LastModifyTime = now
        };
    }

    private static CameraCredentialMigration CreateMigration() =>
        new(new DpapiCurrentUserCameraCredentialProtector());

    private static void AssertProtectedStaging(string stagingPath)
    {
        using var connection = Open(stagingPath, readOnly: true);
        using var integrity = connection.CreateCommand();
        integrity.CommandText = "PRAGMA integrity_check;";
        Assert.Equal("ok", integrity.ExecuteScalar());

        using var version = connection.CreateCommand();
        version.CommandText = "PRAGMA user_version;";
        Assert.Equal(DatabaseSchemaVersion.CurrentUserProtectedCredentials, Convert.ToInt32(version.ExecuteScalar()));

        using var schema = connection.CreateCommand();
        schema.CommandText = "SELECT COUNT(*) FROM pragma_table_info('Camera') WHERE name = 'Password';";
        Assert.Equal(0L, (long)schema.ExecuteScalar()!);

        using var credentials = connection.CreateCommand();
        credentials.CommandText = @"
SELECT Name, PasswordProtected, PasswordProtectionVersion
FROM Camera
ORDER BY Name;";
        using var reader = credentials.ExecuteReader();
        var protector = new DpapiCurrentUserCameraCredentialProtector();
        var recovered = new Dictionary<string, string>();
        while (reader.Read())
        {
            recovered[reader.GetString(0)] = reader.IsDBNull(1)
                ? string.Empty
                : protector.Unprotect((byte[])reader.GetValue(1));

            if (reader.IsDBNull(1))
            {
                Assert.True(reader.IsDBNull(2));
            }
            else
            {
                Assert.Equal(1, reader.GetInt32(2));
            }
        }

        Assert.Equal(FirstSecret, recovered["First"]);
        Assert.Equal(SecondSecret, recovered["Second"]);
        Assert.Equal(string.Empty, recovered["No password"]);
    }

    private static SqliteConnection Open(string path, bool readOnly = false)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    private static bool ContainsSequence(byte[] haystack, byte[] needle) =>
        haystack.AsSpan().IndexOf(needle) >= 0;

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // Best-effort test cleanup.
        }
    }

    private sealed class ThrowingProtector : ICameraCredentialProtector
    {
        private readonly string _rejectedSecret;
        private readonly ICameraCredentialProtector _inner = new DpapiCurrentUserCameraCredentialProtector();

        public ThrowingProtector(string rejectedSecret)
        {
            _rejectedSecret = rejectedSecret;
        }

        public byte[] Protect(string plaintext)
        {
            if (plaintext == _rejectedSecret)
            {
                throw new CryptographicException("Injected protection failure.");
            }

            return _inner.Protect(plaintext);
        }

        public string Unprotect(byte[] protectedEnvelope) => _inner.Unprotect(protectedEnvelope);
    }
}
