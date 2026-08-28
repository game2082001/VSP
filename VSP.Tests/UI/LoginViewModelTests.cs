using VSP.Core.Logging;
using VSP.Core.Security;
using VSP.Device.Interfaces;
using VSP.Domain.Enums;
using VSP.Tests.Logging;
using VSP.UI.ViewModels;
using Xunit;
using UserEntity = VSP.Domain.Entities.User;

namespace VSP.Tests.UI;

[Collection("AppLog")]
public class LoginViewModelTests
{
    private static UserEntity AdminUser(string password = "admin")
    {
        var (hash, salt, iterations) = PasswordHasher.Hash(password);
        return new UserEntity
        {
            Username = "admin",
            PasswordHash = hash,
            PasswordSalt = salt,
            PasswordIterations = iterations,
            Role = Role.Admin,
            MustChangePassword = true
        };
    }

    [Fact]
    public void Login_WithCorrectCredentials_SetsAuthenticatedUserAndRaisesLoginSucceeded()
    {
        var repository = new FakeUserRepository(AdminUser());
        var viewModel = new LoginViewModel(repository) { Username = "admin", Password = "admin" };
        var raised = false;
        viewModel.LoginSucceeded += () => raised = true;

        viewModel.LoginCommand.Execute(null);

        Assert.True(raised);
        Assert.NotNull(viewModel.AuthenticatedUser);
        Assert.Equal("admin", viewModel.AuthenticatedUser!.Username);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public void Login_WithUnknownUsername_FailsWithGenericMessage()
    {
        var repository = new FakeUserRepository(user: null);
        var viewModel = new LoginViewModel(repository) { Username = "nobody", Password = "whatever" };

        viewModel.LoginCommand.Execute(null);

        Assert.Null(viewModel.AuthenticatedUser);
        Assert.Equal("Invalid username or password.", viewModel.ErrorMessage);
    }

    [Fact]
    public void Login_WithWrongPassword_FailsWithIdenticalGenericMessage()
    {
        var repository = new FakeUserRepository(AdminUser());
        var viewModel = new LoginViewModel(repository) { Username = "admin", Password = "wrong-password" };

        viewModel.LoginCommand.Execute(null);

        Assert.Null(viewModel.AuthenticatedUser);
        Assert.Equal("Invalid username or password.", viewModel.ErrorMessage);
    }

    [Fact]
    public void Login_WithDisabledUser_FailsWithIdenticalGenericMessage()
    {
        var user = AdminUser();
        user.IsEnabled = false;
        var repository = new FakeUserRepository(user);
        var viewModel = new LoginViewModel(repository) { Username = "admin", Password = "admin" };

        viewModel.LoginCommand.Execute(null);

        Assert.Null(viewModel.AuthenticatedUser);
        Assert.Equal("Invalid username or password.", viewModel.ErrorMessage);
    }

    [Theory]
    [InlineData("", "admin")]
    [InlineData("admin", "")]
    [InlineData("", "")]
    [InlineData("   ", "admin")]
    public void Login_WithBlankFields_FailsWithoutHittingTheRepository(string username, string password)
    {
        var repository = new FakeUserRepository(AdminUser());
        var viewModel = new LoginViewModel(repository) { Username = username, Password = password };

        viewModel.LoginCommand.Execute(null);

        Assert.Equal(0, repository.GetByUsernameCallCount);
        Assert.Null(viewModel.AuthenticatedUser);
        Assert.Equal("Invalid username or password.", viewModel.ErrorMessage);
    }

    [Fact]
    public void Login_WithCorrectCredentials_LogsSuccessWithoutPasswordOrHashOrSalt()
    {
        // Password deliberately distinct from the username ("admin") -- otherwise the log
        // message legitimately containing the username would be indistinguishable from it
        // containing the password, defeating this assertion.
        var user = AdminUser("correct-horse-battery-staple");
        var repository = new FakeUserRepository(user);
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        var viewModel = new LoginViewModel(repository) { Username = "admin", Password = "correct-horse-battery-staple" };

        viewModel.LoginCommand.Execute(null);

        var call = Assert.Single(recorder.Calls);
        Assert.Equal(LogLevel.Info, call.Level);
        AssertMessageContainsNoCredentialMaterial(call.Message, "correct-horse-battery-staple", user);
    }

    [Fact]
    public void Login_WithWrongPassword_LogsFailureWithoutPasswordOrHashOrSalt()
    {
        var user = AdminUser();
        var repository = new FakeUserRepository(user);
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        var viewModel = new LoginViewModel(repository) { Username = "admin", Password = "totally-wrong" };

        viewModel.LoginCommand.Execute(null);

        var call = Assert.Single(recorder.Calls);
        Assert.Equal(LogLevel.Warning, call.Level);
        AssertMessageContainsNoCredentialMaterial(call.Message, "totally-wrong", user);
    }

    [Fact]
    public void Login_WithUnknownUsername_LogsFailure()
    {
        var repository = new FakeUserRepository(user: null);
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        var viewModel = new LoginViewModel(repository) { Username = "ghost", Password = "irrelevant" };

        viewModel.LoginCommand.Execute(null);

        var call = Assert.Single(recorder.Calls);
        Assert.Equal(LogLevel.Warning, call.Level);
    }

    private static void AssertMessageContainsNoCredentialMaterial(string message, string attemptedPassword, UserEntity user)
    {
        Assert.DoesNotContain(attemptedPassword, message);
        Assert.DoesNotContain(user.PasswordHash, message);
        Assert.DoesNotContain(user.PasswordSalt, message);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        private readonly UserEntity? _user;

        public int GetByUsernameCallCount { get; private set; }

        public FakeUserRepository(UserEntity? user)
        {
            _user = user;
        }

        public UserEntity? GetByUsername(string username)
        {
            GetByUsernameCallCount++;
            return _user is not null && UsernameIdentity.Normalize(_user.Username) == UsernameIdentity.Normalize(username)
                ? _user
                : null;
        }

        public List<UserEntity> GetAll() => _user is null ? new List<UserEntity>() : new List<UserEntity> { _user };

        public UserEntity? GetById(Guid id) => _user is not null && _user.Id == id ? _user : null;

        public UserEntity? GetByNormalizedUsername(string normalizedUsername) =>
            _user is not null && UsernameIdentity.Normalize(_user.Username) == normalizedUsername ? _user : null;

        public void Add(UserEntity user)
        {
        }

        public void Update(UserEntity user)
        {
        }
    }
}
