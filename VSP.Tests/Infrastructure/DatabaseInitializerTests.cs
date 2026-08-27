using VSP.Core.Logging;
using VSP.Core.Security;
using VSP.Domain.Enums;
using VSP.Infrastructure.Database;
using VSP.Infrastructure.Repositories;
using VSP.Tests.Logging;
using Microsoft.Data.Sqlite;
using Xunit;

namespace VSP.Tests.Infrastructure;

[Collection("AppLog")]
public class DatabaseInitializerTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"vsp-db-initializer-test-{Guid.NewGuid():N}");

    [Fact]
    public void Initialize_WithWritableDirectory_ReturnsSuccessResult()
    {
        var initializer = new DatabaseInitializer(new DatabaseService(_tempDirectory));

        var result = initializer.Initialize();

        Assert.True(result.Success);
        Assert.Null(result.Exception);
        Assert.True(File.Exists(Path.Combine(_tempDirectory, "vsp.db")));
    }

    [Fact]
    public void Initialize_WhenDirectoryCannotBeCreated_ReturnsFailedResultWithOriginalException()
    {
        // A file already occupies the path Initialize needs as a directory, so
        // Directory.CreateDirectory (inside DatabaseService.CreateConnection) throws.
        File.WriteAllText(_tempDirectory, "not a directory");

        var initializer = new DatabaseInitializer(new DatabaseService(_tempDirectory));

        var result = initializer.Initialize();

        Assert.False(result.Success);
        Assert.NotNull(result.Exception);
    }

    [Fact]
    public void Initialize_OnFreshDatabase_SeedsExactlyOneDefaultAdmin()
    {
        var databaseService = new DatabaseService(_tempDirectory);
        var initializer = new DatabaseInitializer(databaseService);

        initializer.Initialize();

        var users = new SQLiteUserRepository(databaseService).GetAll();
        var admin = Assert.Single(users);
        Assert.Equal("admin", admin.Username);
        Assert.Equal("ADMIN", admin.NormalizedUsername);
        Assert.Equal(Role.Admin, admin.Role);
        Assert.True(admin.MustChangePassword);
        Assert.True(admin.IsEnabled);
        Assert.True(PasswordHasher.Verify("admin", admin.PasswordHash, admin.PasswordSalt, admin.PasswordIterations));
    }

    [Fact]
    public void Initialize_MigratesExistingUsersToLifecycleSchemaWithoutChangingSecrets()
    {
        var databaseService = new DatabaseService(_tempDirectory);
        var databasePath = databaseService.GetDatabaseFilePath();
        Directory.CreateDirectory(_tempDirectory);
        var (hash, salt, iterations) = PasswordHasher.Hash("existing-password");
        CreateLegacyProtectedUserSchema(databasePath, "OriginalAdmin", hash, salt, iterations);

        var result = new DatabaseInitializer(databaseService).Initialize();

        Assert.True(result.Success);
        var admin = new SQLiteUserRepository(databaseService).GetByUsername("originaladmin");
        Assert.NotNull(admin);
        Assert.Equal("OriginalAdmin", admin!.Username);
        Assert.Equal("ORIGINALADMIN", admin.NormalizedUsername);
        Assert.Equal(hash, admin.PasswordHash);
        Assert.Equal(salt, admin.PasswordSalt);
        Assert.Equal(iterations, admin.PasswordIterations);
        Assert.Equal(Role.Admin, admin.Role);
        Assert.True(admin.MustChangePassword);
        Assert.True(admin.IsEnabled);
        using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
        connection.Open();
        using var columns = connection.CreateCommand();
        columns.CommandText = "SELECT COUNT(*) FROM pragma_table_info('User') WHERE name IN ('NormalizedUsername', 'IsEnabled') AND [notnull] = 1;";
        Assert.Equal(2L, (long)columns.ExecuteScalar()!);
    }

    [Fact]
    public void Initialize_WhenExistingUsersNormalizeToSameIdentity_FailsClosed()
    {
        var databaseService = new DatabaseService(_tempDirectory);
        var databasePath = databaseService.GetDatabaseFilePath();
        Directory.CreateDirectory(_tempDirectory);
        CreateLegacyProtectedUserSchema(databasePath, "Admin", "hash-one", "salt-one", 210_000);
        InsertLegacyUser(databasePath, " admin ", "hash-two", "salt-two", 210_000);

        var result = new DatabaseInitializer(databaseService).Initialize();

        Assert.False(result.Success);
        Assert.NotNull(result.Exception);
        using var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly");
        connection.Open();
        using var columns = connection.CreateCommand();
        columns.CommandText = "SELECT COUNT(*) FROM pragma_table_info('User') WHERE name IN ('NormalizedUsername', 'IsEnabled');";
        Assert.Equal(0L, (long)columns.ExecuteScalar()!);
    }

    [Fact]
    public void Initialize_CalledTwice_DoesNotReSeedDefaultAdmin()
    {
        var databaseService = new DatabaseService(_tempDirectory);
        var initializer = new DatabaseInitializer(databaseService);

        initializer.Initialize();
        initializer.Initialize();

        var users = new SQLiteUserRepository(databaseService).GetAll();
        Assert.Single(users);
    }

    [Fact]
    public void Initialize_WhenDirectoryCannotBeCreated_DoesNotLogItself()
    {
        // Per Product Owner instruction: the Error ID and the exception must be logged together,
        // once, by the caller (App.xaml.cs) -- not split across a separate log line here.
        File.WriteAllText(_tempDirectory, "not a directory");
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        var initializer = new DatabaseInitializer(new DatabaseService(_tempDirectory));
        initializer.Initialize();

        Assert.Empty(recorder.Calls);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            else if (File.Exists(_tempDirectory))
            {
                File.Delete(_tempDirectory);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    private static void CreateLegacyProtectedUserSchema(
        string databasePath,
        string username,
        string hash,
        string salt,
        int iterations)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $@"
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
    LastModifyTime TEXT
);
CREATE UNIQUE INDEX IX_User_Username ON User (Username);

CREATE TABLE CredentialMigrationMetadata
(
    Id INTEGER PRIMARY KEY CHECK (Id = 1),
    SourceSha256 BLOB NOT NULL,
    ProtectionProvider TEXT NOT NULL CHECK (ProtectionProvider = 'DPAPI'),
    ProtectionScope TEXT NOT NULL CHECK (ProtectionScope = 'CurrentUser'),
    ProtectionVersion INTEGER NOT NULL CHECK (ProtectionVersion = 1)
);
INSERT INTO CredentialMigrationMetadata
(Id, SourceSha256, ProtectionProvider, ProtectionScope, ProtectionVersion)
VALUES (1, zeroblob(32), 'DPAPI', 'CurrentUser', 1);
PRAGMA user_version = {DatabaseSchemaVersion.CurrentUserProtectedCredentials};";
        command.ExecuteNonQuery();
        InsertLegacyUser(databasePath, username, hash, salt, iterations);
    }

    private static void InsertLegacyUser(string databasePath, string username, string hash, string salt, int iterations)
    {
        using var connection = new SqliteConnection($"Data Source={databasePath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
INSERT INTO User
(Id, Username, PasswordHash, PasswordSalt, PasswordIterations, Role, MustChangePassword, CreateTime, LastModifyTime)
VALUES ($Id, $Username, $Hash, $Salt, $Iterations, $Role, 1, $CreateTime, $LastModifyTime);";
        command.Parameters.AddWithValue("$Id", Guid.NewGuid().ToString());
        command.Parameters.AddWithValue("$Username", username);
        command.Parameters.AddWithValue("$Hash", hash);
        command.Parameters.AddWithValue("$Salt", salt);
        command.Parameters.AddWithValue("$Iterations", iterations);
        command.Parameters.AddWithValue("$Role", (int)Role.Admin);
        command.Parameters.AddWithValue("$CreateTime", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$LastModifyTime", DateTime.UtcNow.ToString("O"));
        command.ExecuteNonQuery();
    }
}
