using VSP.Core.Logging;
using VSP.Core.Security;
using VSP.Device.Interfaces;
using VSP.Device.Users;
using VSP.Domain.Enums;
using VSP.Tests.Logging;
using VSP.UI.ViewModels;
using Xunit;
using UserEntity = VSP.Domain.Entities.User;

namespace VSP.Tests.UI;

[Collection("AppLog")]
public sealed class Pd003UserManagementFinalValidationTests
{
    [Fact]
    public void AdminReset_ForcesNextLoginChange_ThenOperatorCanSetOwnPassword()
    {
        var repository = new FakeUserRepository();
        var admin = User("admin", Role.Admin, "admin-current-password");
        var operatorUser = User("operator", Role.Operator, "operator-old-password");
        operatorUser.MustChangePassword = false;
        repository.Add(admin);
        repository.Add(operatorUser);
        var service = new UserLifecycleService(repository);

        var reset = service.ResetPassword(operatorUser.Id, admin.Id, "operator-temporary-password");

        Assert.True(reset.Success);
        Assert.True(operatorUser.MustChangePassword);
        Assert.False(PasswordHasher.Verify("operator-old-password", operatorUser.PasswordHash, operatorUser.PasswordSalt, operatorUser.PasswordIterations));
        Assert.True(PasswordHasher.Verify("operator-temporary-password", operatorUser.PasswordHash, operatorUser.PasswordSalt, operatorUser.PasswordIterations));

        var loginWithTemporaryPassword = new LoginViewModel(repository)
        {
            Username = "OPERATOR",
            Password = "operator-temporary-password"
        };
        loginWithTemporaryPassword.LoginCommand.Execute(null);

        Assert.NotNull(loginWithTemporaryPassword.AuthenticatedUser);
        Assert.True(loginWithTemporaryPassword.AuthenticatedUser!.MustChangePassword);

        var changePassword = new ForcedPasswordChangeViewModel(operatorUser, service)
        {
            CurrentPassword = "operator-temporary-password",
            NewPassword = "operator-final-password",
            ConfirmNewPassword = "operator-final-password"
        };
        changePassword.ChangePasswordCommand.Execute(null);

        Assert.Null(changePassword.ErrorMessage);
        Assert.False(operatorUser.MustChangePassword);
        Assert.Equal("", changePassword.CurrentPassword);
        Assert.Equal("", changePassword.NewPassword);
        Assert.Equal("", changePassword.ConfirmNewPassword);

        var oldPasswordLogin = new LoginViewModel(repository)
        {
            Username = "operator",
            Password = "operator-temporary-password"
        };
        oldPasswordLogin.LoginCommand.Execute(null);

        Assert.Null(oldPasswordLogin.AuthenticatedUser);
        Assert.Equal("Invalid username or password.", oldPasswordLogin.ErrorMessage);

        var newPasswordLogin = new LoginViewModel(repository)
        {
            Username = "operator",
            Password = "operator-final-password"
        };
        newPasswordLogin.LoginCommand.Execute(null);

        Assert.NotNull(newPasswordLogin.AuthenticatedUser);
        Assert.False(newPasswordLogin.AuthenticatedUser!.MustChangePassword);
    }

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.Operator)]
    public void ChangeOwnPassword_ForAdminAndOperator_RotatesSecretAndDoesNotExposeCredentialMaterial(Role role)
    {
        var repository = new FakeUserRepository();
        var user = User($"{role.ToString().ToLowerInvariant()}-user", role, "current-password");
        user.MustChangePassword = true;
        repository.Add(user);
        var originalHash = user.PasswordHash;
        var originalSalt = user.PasswordSalt;
        var originalIterations = user.PasswordIterations;
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        var viewModel = new ForcedPasswordChangeViewModel(user, new UserLifecycleService(repository));

        Assert.Equal("", viewModel.CurrentPassword);
        Assert.Equal("", viewModel.NewPassword);
        Assert.Equal("", viewModel.ConfirmNewPassword);

        viewModel.CurrentPassword = "current-password";
        viewModel.NewPassword = "new-private-password";
        viewModel.ConfirmNewPassword = "new-private-password";
        viewModel.ChangePasswordCommand.Execute(null);

        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal("Password changed.", viewModel.StatusMessage);
        Assert.False(user.MustChangePassword);
        Assert.NotEqual(originalHash, user.PasswordHash);
        Assert.NotEqual(originalSalt, user.PasswordSalt);
        Assert.Equal(originalIterations, user.PasswordIterations);
        Assert.False(PasswordHasher.Verify("current-password", user.PasswordHash, user.PasswordSalt, user.PasswordIterations));
        Assert.True(PasswordHasher.Verify("new-private-password", user.PasswordHash, user.PasswordSalt, user.PasswordIterations));
        Assert.All(recorder.Calls, call => AssertLogMessageContainsNoSecrets(call.Message, user, "current-password", "new-private-password"));
    }

    [Fact]
    public void UserManagementAndLoginFailures_DoNotExposeAccountStateOrPasswordMaterial()
    {
        var repository = new FakeUserRepository();
        var admin = User("admin", Role.Admin, "admin-password");
        var disabled = User("disabled-operator", Role.Operator, "disabled-password");
        disabled.IsEnabled = false;
        repository.Add(admin);
        repository.Add(disabled);
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        var disabledLogin = new LoginViewModel(repository)
        {
            Username = "disabled-operator",
            Password = "disabled-password"
        };
        disabledLogin.LoginCommand.Execute(null);

        var unknownLogin = new LoginViewModel(repository)
        {
            Username = "unknown-operator",
            Password = "disabled-password"
        };
        unknownLogin.LoginCommand.Execute(null);

        Assert.Equal("Invalid username or password.", disabledLogin.ErrorMessage);
        Assert.Equal("Invalid username or password.", unknownLogin.ErrorMessage);

        var users = new UsersViewModel(repository, new UserLifecycleService(repository), admin)
        {
            NewUsername = "operator",
            TemporaryPassword = "temporary-password",
            ConfirmTemporaryPassword = "different-password"
        };
        users.CreateUserCommand.Execute(null);

        Assert.Equal("Temporary password confirmation does not match.", users.StatusMessage);
        Assert.Equal("", users.TemporaryPassword);
        Assert.Equal("", users.ConfirmTemporaryPassword);
        Assert.All(users.Users, row =>
        {
            Assert.DoesNotContain(admin.PasswordHash, row.Username);
            Assert.DoesNotContain(admin.PasswordSalt, row.Username);
            Assert.DoesNotContain(disabled.PasswordHash, row.Username);
            Assert.DoesNotContain(disabled.PasswordSalt, row.Username);
        });
        Assert.All(recorder.Calls, call => AssertLogMessageContainsNoSecrets(call.Message, disabled, "disabled-password", "temporary-password"));
    }

    [Fact]
    public void AdminLifecycleRules_PreventSelfResetSelfDisableAndLastAdminDisable()
    {
        var repository = new FakeUserRepository();
        var onlyEnabledAdmin = User("admin", Role.Admin, "admin-password");
        var disabledAdmin = User("disabled-admin", Role.Admin, "disabled-admin-password");
        disabledAdmin.IsEnabled = false;
        var operatorUser = User("operator", Role.Operator, "operator-password");
        repository.Add(onlyEnabledAdmin);
        repository.Add(disabledAdmin);
        repository.Add(operatorUser);
        var service = new UserLifecycleService(repository);

        var selfReset = service.ResetPassword(onlyEnabledAdmin.Id, onlyEnabledAdmin.Id, "temporary-password");
        var selfDisable = service.DisableUser(onlyEnabledAdmin.Id, onlyEnabledAdmin.Id);
        var lastAdminDisable = service.DisableUser(onlyEnabledAdmin.Id, operatorUser.Id);

        Assert.False(selfReset.Success);
        Assert.Equal("Use Change Password to update your own password.", selfReset.FailureMessage);
        Assert.False(selfDisable.Success);
        Assert.Equal("You cannot disable your own account.", selfDisable.FailureMessage);
        Assert.False(lastAdminDisable.Success);
        Assert.Equal("At least one enabled Admin account is required.", lastAdminDisable.FailureMessage);
        Assert.True(onlyEnabledAdmin.IsEnabled);
        Assert.True(PasswordHasher.Verify("admin-password", onlyEnabledAdmin.PasswordHash, onlyEnabledAdmin.PasswordSalt, onlyEnabledAdmin.PasswordIterations));
    }

    [Fact]
    public void CaseInsensitiveUsernameIdentity_PreventsDuplicateAndAuthenticatesCanonicalUser()
    {
        var repository = new FakeUserRepository();
        var admin = User("Admin.Display", Role.Admin, "admin-password");
        repository.Add(admin);
        var service = new UserLifecycleService(repository);

        var duplicate = service.CreateUser(" admin.display ", "temporary-password", Role.Operator);

        Assert.False(duplicate.Success);
        Assert.Equal("A user with that username already exists.", duplicate.FailureMessage);
        Assert.Single(repository.Users);

        var login = new LoginViewModel(repository)
        {
            Username = "ADMIN.DISPLAY",
            Password = "admin-password"
        };
        login.LoginCommand.Execute(null);

        Assert.NotNull(login.AuthenticatedUser);
        Assert.Same(admin, login.AuthenticatedUser);
    }
    private static void AssertLogMessageContainsNoSecrets(
        string message,
        UserEntity user,
        params string[] passwords)
    {
        Assert.DoesNotContain(user.PasswordHash, message);
        Assert.DoesNotContain(user.PasswordSalt, message);
        foreach (var password in passwords)
        {
            Assert.DoesNotContain(password, message);
        }
    }

    private static UserEntity User(string username, Role role, string password)
    {
        var (hash, salt, iterations) = PasswordHasher.Hash(password);
        return new UserEntity
        {
            Username = username,
            NormalizedUsername = UsernameIdentity.Normalize(username),
            PasswordHash = hash,
            PasswordSalt = salt,
            PasswordIterations = iterations,
            Role = role,
            IsEnabled = true
        };
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public List<UserEntity> Users { get; } = new();

        public List<UserEntity> GetAll() => Users.ToList();

        public UserEntity? GetById(Guid id) => Users.SingleOrDefault(user => user.Id == id);

        public UserEntity? GetByUsername(string username) =>
            GetByNormalizedUsername(UsernameIdentity.Normalize(username));

        public UserEntity? GetByNormalizedUsername(string normalizedUsername) =>
            Users.SingleOrDefault(user => user.NormalizedUsername == normalizedUsername);

        public void Add(UserEntity user)
        {
            user.Username = user.Username.Trim();
            user.NormalizedUsername = UsernameIdentity.Normalize(user.Username);
            if (GetByNormalizedUsername(user.NormalizedUsername) is not null)
            {
                throw new InvalidOperationException("duplicate user");
            }

            Users.Add(user);
        }

        public void Update(UserEntity user)
        {
        }
    }
}


