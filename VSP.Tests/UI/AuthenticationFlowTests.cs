using VSP.Core.Security;
using VSP.Device.Repositories;
using VSP.Domain.Enums;
using VSP.Infrastructure.Database;
using VSP.Infrastructure.Repositories;
using VSP.Tests.Logging;
using VSP.UI.ViewModels;
using Xunit;

namespace VSP.Tests.UI;

/// <summary>
/// End-to-end (real SQLite, real DefaultAdminSeeder, real PBKDF2 hashing) coverage of Epic-018
/// Milestone 18B's login + forced password change flow, without going through any WPF Window --
/// exercises exactly the same LoginViewModel/ForcedPasswordChangeViewModel/UserRepository objects
/// App.xaml.cs wires together, just without the modal dialog shell around them.
/// </summary>
[Collection("AppLog")]
public class AuthenticationFlowTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), $"vsp-auth-flow-test-{Guid.NewGuid():N}");

    public AuthenticationFlowTests()
    {
        VSP.Core.Logging.AppLog.Initialize(new RecordingLogger());
    }

    private UserRepository CreateSeededUserRepository()
    {
        var databaseService = new DatabaseService(_tempDirectory);
        new DatabaseInitializer(databaseService).Initialize();
        return new UserRepository(new SQLiteUserRepository(databaseService));
    }

    [Fact]
    public void DefaultAdminCredentials_AuthenticateOnFreshDatabase()
    {
        var repository = CreateSeededUserRepository();
        var login = new LoginViewModel(repository) { Username = "admin", Password = "admin" };

        login.LoginCommand.Execute(null);

        Assert.NotNull(login.AuthenticatedUser);
        Assert.Equal(Role.Admin, login.AuthenticatedUser!.Role);
    }

    [Fact]
    public void DefaultAdmin_MustChangePasswordIsSetOnFirstLogin()
    {
        var repository = CreateSeededUserRepository();
        var login = new LoginViewModel(repository) { Username = "admin", Password = "admin" };

        login.LoginCommand.Execute(null);

        Assert.True(login.AuthenticatedUser!.MustChangePassword);
    }

    [Fact]
    public void FullFlow_LoginThenChangePassword_ClearsFlagAndRotatesCredentialInDatabase()
    {
        var repository = CreateSeededUserRepository();
        var login = new LoginViewModel(repository) { Username = "admin", Password = "admin" };
        login.LoginCommand.Execute(null);

        var change = new ForcedPasswordChangeViewModel(login.AuthenticatedUser!, repository)
        {
            CurrentPassword = "admin",
            NewPassword = "brand-new-password",
            ConfirmNewPassword = "brand-new-password"
        };
        change.ChangePasswordCommand.Execute(null);

        // Re-fetched independently of the in-memory objects above, so this proves the change was
        // actually persisted to the database, not just mutated in memory.
        var reloaded = repository.GetByUsername("admin")!;
        Assert.False(reloaded.MustChangePassword);
        Assert.True(PasswordHasher.Verify("brand-new-password", reloaded.PasswordHash, reloaded.PasswordSalt, reloaded.PasswordIterations));
    }

    [Fact]
    public void FullFlow_AfterPasswordChange_OldPasswordNoLongerAuthenticates()
    {
        var repository = CreateSeededUserRepository();
        var login = new LoginViewModel(repository) { Username = "admin", Password = "admin" };
        login.LoginCommand.Execute(null);

        var change = new ForcedPasswordChangeViewModel(login.AuthenticatedUser!, repository)
        {
            CurrentPassword = "admin",
            NewPassword = "brand-new-password",
            ConfirmNewPassword = "brand-new-password"
        };
        change.ChangePasswordCommand.Execute(null);

        var secondAttempt = new LoginViewModel(repository) { Username = "admin", Password = "admin" };
        secondAttempt.LoginCommand.Execute(null);

        Assert.Null(secondAttempt.AuthenticatedUser);
    }

    [Fact]
    public void FullFlow_AfterPasswordChange_NewPasswordAuthenticatesWithoutForcingAnotherChange()
    {
        var repository = CreateSeededUserRepository();
        var login = new LoginViewModel(repository) { Username = "admin", Password = "admin" };
        login.LoginCommand.Execute(null);

        var change = new ForcedPasswordChangeViewModel(login.AuthenticatedUser!, repository)
        {
            CurrentPassword = "admin",
            NewPassword = "brand-new-password",
            ConfirmNewPassword = "brand-new-password"
        };
        change.ChangePasswordCommand.Execute(null);

        var secondAttempt = new LoginViewModel(repository) { Username = "admin", Password = "brand-new-password" };
        secondAttempt.LoginCommand.Execute(null);

        Assert.NotNull(secondAttempt.AuthenticatedUser);
        Assert.False(secondAttempt.AuthenticatedUser!.MustChangePassword);
    }

    /// <summary>
    /// Milestone 18C "Login after Logout works": App.xaml.cs's session loop (not itself
    /// unit-testable -- WPF's Application/Window classes require an STA thread xunit does not
    /// provide) discards the old MainWindowViewModel/User and calls the exact same
    /// LoginViewModel + IUserRepository construction path again on Logout. This proves that path
    /// is safe to call repeatedly against the same repository/database -- nothing about a
    /// completed login leaves the repository or the credential in a state that would make a
    /// second, independent login attempt behave any differently.
    /// </summary>
    [Fact]
    public void SecondIndependentLoginAttempt_AfterFirstLoginCompletes_AlsoSucceeds()
    {
        var repository = CreateSeededUserRepository();

        var firstSession = new LoginViewModel(repository) { Username = "admin", Password = "admin" };
        firstSession.LoginCommand.Execute(null);
        Assert.NotNull(firstSession.AuthenticatedUser);

        // Simulates Logout discarding the first session's LoginViewModel/User entirely and
        // App.xaml.cs's StartSession() constructing a brand new one for the next login.
        var secondSession = new LoginViewModel(repository) { Username = "admin", Password = "admin" };
        secondSession.LoginCommand.Execute(null);

        Assert.NotNull(secondSession.AuthenticatedUser);
        Assert.Equal("admin", secondSession.AuthenticatedUser!.Username);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
