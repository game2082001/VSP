using VSP.Core.Logging;
using VSP.Core.Security;
using VSP.Domain.Enums;
using VSP.Infrastructure.Database;
using VSP.Infrastructure.Repositories;
using VSP.Tests.Logging;
using Xunit;
using UserEntity = VSP.Domain.Entities.User;

namespace VSP.Tests.Infrastructure;

[Collection("AppLog")]
public class SQLiteUserRepositoryTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"vsp-user-repository-test-{Guid.NewGuid():N}");

    private SQLiteUserRepository CreateRepository()
    {
        var databaseService = new DatabaseService(_tempDirectory);
        new DatabaseInitializer(databaseService).Initialize();
        return new SQLiteUserRepository(databaseService);
    }

    private static UserEntity NewUser(string username = "operator1") => new()
    {
        Username = username,
        PasswordHash = "hash",
        PasswordSalt = "salt",
        PasswordIterations = 210_000,
        Role = Role.Operator
    };

    [Fact]
    public void AddThenGetAll_RoundTripsUser()
    {
        var repository = CreateRepository();
        var user = NewUser();

        repository.Add(user);
        var all = repository.GetAll();

        // The seeded default admin (Milestone 18A / DefaultAdminSeeder) is also present.
        var stored = Assert.Single(all, u => u.Username == "operator1");
        Assert.Equal(user.Id, stored.Id);
        Assert.Equal(user.PasswordHash, stored.PasswordHash);
        Assert.Equal(user.PasswordSalt, stored.PasswordSalt);
        Assert.Equal(user.PasswordIterations, stored.PasswordIterations);
        Assert.Equal(Role.Operator, stored.Role);
        Assert.False(stored.MustChangePassword);
        Assert.True(stored.IsEnabled);
        Assert.Equal(UsernameIdentity.Normalize(user.Username), stored.NormalizedUsername);
    }

    [Fact]
    public void GetByUsername_UsesNormalizedCaseInsensitiveIdentity()
    {
        var repository = CreateRepository();
        repository.Add(NewUser("DisplayName"));

        var found = repository.GetByUsername("  displayname  ");

        Assert.NotNull(found);
        Assert.Equal("DisplayName", found!.Username);
        Assert.Equal("DISPLAYNAME", found.NormalizedUsername);
    }

    [Fact]
    public void Add_CaseOnlyDuplicateUsername_ThrowsAndLogsError()
    {
        var repository = CreateRepository();
        repository.Add(NewUser("CaseUser"));
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        var thrown = Record.Exception(() => repository.Add(NewUser("caseuser")));

        Assert.NotNull(thrown);
        var call = Assert.Single(recorder.Calls);
        Assert.Equal(LogLevel.Error, call.Level);
    }

    [Fact]
    public void GetByUsername_ReturnsMatchingUser()
    {
        var repository = CreateRepository();
        repository.Add(NewUser("findme"));

        var found = repository.GetByUsername("findme");

        Assert.NotNull(found);
        Assert.Equal("findme", found!.Username);
    }

    [Fact]
    public void GetByUsername_WhenNoMatch_ReturnsNull()
    {
        var repository = CreateRepository();

        Assert.Null(repository.GetByUsername("does-not-exist"));
    }

    [Fact]
    public void Add_DuplicateUsername_ThrowsAndLogsError()
    {
        var repository = CreateRepository();
        repository.Add(NewUser("dup"));
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        var thrown = Record.Exception(() => repository.Add(NewUser("dup")));

        Assert.NotNull(thrown);
        var call = Assert.Single(recorder.Calls);
        Assert.Equal(LogLevel.Error, call.Level);
    }

    [Fact]
    public void Update_ThenGetByUsername_ReflectsChange()
    {
        var repository = CreateRepository();
        var user = NewUser("original");
        repository.Add(user);

        user.MustChangePassword = true;
        repository.Update(user);

        var stored = repository.GetByUsername("original");
        Assert.NotNull(stored);
        Assert.True(stored!.MustChangePassword);
    }

    [Fact]
    public void Delete_RemovesUser()
    {
        var repository = CreateRepository();
        var user = NewUser("todelete");
        repository.Add(user);

        repository.Delete(user.Id);

        Assert.Null(repository.GetByUsername("todelete"));
    }

    [Fact]
    public void Add_WhenDatabaseUnavailable_LogsErrorAndRethrows()
    {
        // A file already occupies the path Add needs as a directory, so the underlying
        // CreateConnection/Directory.CreateDirectory throws before any SQL runs.
        File.WriteAllText(_tempDirectory, "not a directory");
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        var repository = new SQLiteUserRepository(new DatabaseService(_tempDirectory));

        var thrown = Record.Exception(() => repository.Add(NewUser()));
        Assert.NotNull(thrown);

        var call = Assert.Single(recorder.Calls);
        Assert.Equal(LogLevel.Error, call.Level);
        Assert.NotNull(call.Exception);
    }

    [Fact]
    public void GetAll_WhenDatabaseUnavailable_LogsErrorAndRethrows()
    {
        File.WriteAllText(_tempDirectory, "not a directory");
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        var repository = new SQLiteUserRepository(new DatabaseService(_tempDirectory));

        var thrown = Record.Exception(() => repository.GetAll());
        Assert.NotNull(thrown);

        var call = Assert.Single(recorder.Calls);
        Assert.Equal(LogLevel.Error, call.Level);
        Assert.NotNull(call.Exception);
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
