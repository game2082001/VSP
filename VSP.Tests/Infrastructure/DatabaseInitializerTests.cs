using VSP.Core.Logging;
using VSP.Core.Security;
using VSP.Domain.Enums;
using VSP.Infrastructure.Database;
using VSP.Infrastructure.Repositories;
using VSP.Tests.Logging;
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
        Assert.Equal(Role.Admin, admin.Role);
        Assert.True(admin.MustChangePassword);
        Assert.True(PasswordHasher.Verify("admin", admin.PasswordHash, admin.PasswordSalt, admin.PasswordIterations));
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
}
