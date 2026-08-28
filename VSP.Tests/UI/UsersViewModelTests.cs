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
public class UsersViewModelTests
{
    [Fact]
    public void Construct_LoadsUserListWithoutPasswordMaterial()
    {
        var repository = new FakeUserRepository();
        var admin = User("admin", Role.Admin);
        repository.Add(admin);

        var viewModel = CreateViewModel(repository, admin);

        var row = Assert.Single(viewModel.Users);
        Assert.Equal("admin", row.Username);
        Assert.Equal(Role.Admin, row.Role);
        Assert.Equal("Enabled", row.Status);
        Assert.False(row.MustChangePassword);
        Assert.Null(viewModel.SelectedUser);
    }

    [Fact]
    public void Construct_WithOperatorCurrentUser_RejectsUserManagementAccess()
    {
        var repository = new FakeUserRepository();
        var operatorUser = User("operator", Role.Operator);
        repository.Add(operatorUser);

        Assert.Throws<UnauthorizedAccessException>(() => CreateViewModel(repository, operatorUser));
    }

    [Fact]
    public void CreateUser_WithMatchingTemporaryPassword_CreatesForcedChangeOperatorAndClearsPasswordFields()
    {
        var repository = new FakeUserRepository();
        var admin = User("admin", Role.Admin);
        repository.Add(admin);
        var viewModel = CreateViewModel(repository, admin);
        viewModel.NewUsername = "operator";
        viewModel.NewUserRole = Role.Operator;
        viewModel.TemporaryPassword = "temporary-password";
        viewModel.ConfirmTemporaryPassword = "temporary-password";

        viewModel.CreateUserCommand.Execute(null);

        var created = repository.GetByUsername("OPERATOR");
        Assert.NotNull(created);
        Assert.Equal(Role.Operator, created!.Role);
        Assert.True(created.IsEnabled);
        Assert.True(created.MustChangePassword);
        Assert.True(PasswordHasher.Verify("temporary-password", created.PasswordHash, created.PasswordSalt, created.PasswordIterations));
        Assert.Equal("", viewModel.TemporaryPassword);
        Assert.Equal("", viewModel.ConfirmTemporaryPassword);
        Assert.Equal("User created.", viewModel.StatusMessage);
    }

    [Fact]
    public void CreateUser_WithCaseInsensitiveDuplicate_ShowsActionableError()
    {
        var repository = new FakeUserRepository();
        var admin = User("Admin", Role.Admin);
        repository.Add(admin);
        var viewModel = CreateViewModel(repository, admin);
        viewModel.NewUsername = " admin ";
        viewModel.TemporaryPassword = "temporary-password";
        viewModel.ConfirmTemporaryPassword = "temporary-password";

        viewModel.CreateUserCommand.Execute(null);

        Assert.Equal("A user with that username already exists.", viewModel.StatusMessage);
        Assert.Equal("", viewModel.TemporaryPassword);
        Assert.Equal("", viewModel.ConfirmTemporaryPassword);
        Assert.Single(repository.Users);
    }

    [Fact]
    public void CreateUser_WithMismatchedTemporaryPassword_DoesNotCreateUser()
    {
        var repository = new FakeUserRepository();
        var admin = User("admin", Role.Admin);
        repository.Add(admin);
        var viewModel = CreateViewModel(repository, admin);
        viewModel.NewUsername = "operator";
        viewModel.TemporaryPassword = "temporary-password";
        viewModel.ConfirmTemporaryPassword = "different-password";

        viewModel.CreateUserCommand.Execute(null);

        Assert.Equal("Temporary password confirmation does not match.", viewModel.StatusMessage);
        Assert.Equal("", viewModel.TemporaryPassword);
        Assert.Equal("", viewModel.ConfirmTemporaryPassword);
        Assert.Single(repository.Users);
    }

    [Fact]
    public void DisableSelectedUser_WithoutExplicitSelection_PromptsForSelection()
    {
        var repository = new FakeUserRepository();
        var admin = User("admin", Role.Admin);
        var operatorUser = User("operator", Role.Operator);
        repository.Add(admin);
        repository.Add(operatorUser);
        var viewModel = CreateViewModel(repository, admin);

        viewModel.DisableSelectedUserCommand.Execute(null);

        Assert.True(operatorUser.IsEnabled);
        Assert.Equal("Select a user first.", viewModel.StatusMessage);
    }

    [Fact]
    public void DisableSelectedUser_UsesServiceRulesToRejectSelfDisable()
    {
        var repository = new FakeUserRepository();
        var admin = User("admin", Role.Admin);
        repository.Add(admin);
        var viewModel = CreateViewModel(repository, admin);
        viewModel.SelectedUser = Assert.Single(viewModel.Users);

        viewModel.DisableSelectedUserCommand.Execute(null);

        Assert.True(admin.IsEnabled);
        Assert.Equal("You cannot disable your own account.", viewModel.StatusMessage);
    }

    [Fact]
    public void DisableSelectedUser_UsesServiceRulesToRejectLastEnabledAdmin()
    {
        var repository = new FakeUserRepository();
        var admin = User("admin", Role.Admin);
        var disabledAdmin = User("disabled-admin", Role.Admin);
        disabledAdmin.IsEnabled = false;
        repository.Add(admin);
        repository.Add(disabledAdmin);
        var viewModel = CreateViewModel(repository, disabledAdmin);
        viewModel.SelectedUser = viewModel.Users.Single(user => user.Username == "admin");

        viewModel.DisableSelectedUserCommand.Execute(null);

        Assert.True(admin.IsEnabled);
        Assert.Equal("At least one enabled Admin account is required.", viewModel.StatusMessage);
    }

    [Fact]
    public void DisableAndEnableSelectedUser_RefreshesListState()
    {
        var repository = new FakeUserRepository();
        var admin = User("admin", Role.Admin);
        var operatorUser = User("operator", Role.Operator);
        repository.Add(admin);
        repository.Add(operatorUser);
        var viewModel = CreateViewModel(repository, admin);
        viewModel.SelectedUser = viewModel.Users.Single(user => user.Username == "operator");

        viewModel.DisableSelectedUserCommand.Execute(null);

        Assert.False(operatorUser.IsEnabled);
        Assert.Equal("Disabled", viewModel.Users.Single(user => user.Username == "operator").Status);

        viewModel.EnableSelectedUserCommand.Execute(null);

        Assert.True(operatorUser.IsEnabled);
        Assert.Equal("Enabled", viewModel.Users.Single(user => user.Username == "operator").Status);
    }

    [Fact]
    public void ResetSelectedUserPassword_ForOtherUserForcesChangeAndClearsPasswordFields()
    {
        var repository = new FakeUserRepository();
        var admin = User("admin", Role.Admin);
        var operatorUser = User("operator", Role.Operator);
        repository.Add(admin);
        repository.Add(operatorUser);
        var viewModel = CreateViewModel(repository, admin);
        viewModel.SelectedUser = viewModel.Users.Single(user => user.Username == "operator");
        viewModel.ResetTemporaryPassword = "new-temporary-password";
        viewModel.ConfirmResetTemporaryPassword = "new-temporary-password";

        viewModel.ResetSelectedUserPasswordCommand.Execute(null);

        Assert.True(operatorUser.MustChangePassword);
        Assert.True(PasswordHasher.Verify("new-temporary-password", operatorUser.PasswordHash, operatorUser.PasswordSalt, operatorUser.PasswordIterations));
        Assert.Equal("", viewModel.ResetTemporaryPassword);
        Assert.Equal("", viewModel.ConfirmResetTemporaryPassword);
    }

    [Fact]
    public void ResetSelectedUserPassword_RejectsSelfReset()
    {
        var repository = new FakeUserRepository();
        var admin = User("admin", Role.Admin);
        repository.Add(admin);
        var viewModel = CreateViewModel(repository, admin);
        viewModel.SelectedUser = Assert.Single(viewModel.Users);
        viewModel.ResetTemporaryPassword = "new-temporary-password";
        viewModel.ConfirmResetTemporaryPassword = "new-temporary-password";

        viewModel.ResetSelectedUserPasswordCommand.Execute(null);

        Assert.Equal("Use Change Password to update your own password.", viewModel.StatusMessage);
        Assert.Equal("", viewModel.ResetTemporaryPassword);
        Assert.Equal("", viewModel.ConfirmResetTemporaryPassword);
    }

    [Fact]
    public void ResetSelectedUserPassword_WithMismatchedConfirmation_ClearsPasswordFields()
    {
        var repository = new FakeUserRepository();
        var admin = User("admin", Role.Admin);
        var operatorUser = User("operator", Role.Operator);
        repository.Add(admin);
        repository.Add(operatorUser);
        var viewModel = CreateViewModel(repository, admin);
        viewModel.SelectedUser = viewModel.Users.Single(user => user.Username == "operator");
        viewModel.ResetTemporaryPassword = "new-temporary-password";
        viewModel.ConfirmResetTemporaryPassword = "different-password";

        viewModel.ResetSelectedUserPasswordCommand.Execute(null);

        Assert.Equal("Temporary password confirmation does not match.", viewModel.StatusMessage);
        Assert.Equal("", viewModel.ResetTemporaryPassword);
        Assert.Equal("", viewModel.ConfirmResetTemporaryPassword);
        Assert.False(PasswordHasher.Verify("new-temporary-password", operatorUser.PasswordHash, operatorUser.PasswordSalt, operatorUser.PasswordIterations));
    }

    [Fact]
    public void OperationFailure_LogAndStatusDoNotExposePasswordHashOrSalt()
    {
        var repository = new ThrowingUserRepository();
        var admin = User("admin", Role.Admin);
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        var viewModel = new UsersViewModel(repository, new UserLifecycleService(repository), admin)
        {
            NewUsername = "operator",
            TemporaryPassword = "temporary-secret",
            ConfirmTemporaryPassword = "temporary-secret"
        };

        viewModel.CreateUserCommand.Execute(null);

        Assert.Equal("The user account could not be updated.", viewModel.StatusMessage);
        Assert.Equal("", viewModel.TemporaryPassword);
        Assert.Equal("", viewModel.ConfirmTemporaryPassword);
        var call = Assert.Single(recorder.Calls);
        Assert.DoesNotContain("temporary-secret", call.Message);
        Assert.DoesNotContain("hash", call.Message);
        Assert.DoesNotContain("salt", call.Message);
    }

    [Fact]
    public void Construct_WhenUserListCannotLoad_DoesNotThrowOrExposeSecretMaterial()
    {
        var repository = new ThrowingGetAllUserRepository();
        var admin = User("admin", Role.Admin);
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        var viewModel = new UsersViewModel(repository, new UserLifecycleService(repository), admin);

        Assert.Empty(viewModel.Users);
        Assert.Null(viewModel.SelectedUser);
        Assert.Equal("The user list could not be loaded.", viewModel.StatusMessage);
        var call = Assert.Single(recorder.Calls);
        Assert.DoesNotContain("password", call.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", call.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("salt", call.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static UsersViewModel CreateViewModel(FakeUserRepository repository, UserEntity currentUser) =>
        new(repository, new UserLifecycleService(repository), currentUser);

    private static UserEntity User(string username, Role role, string password = "password") 
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

    private sealed class ThrowingUserRepository : IUserRepository
    {
        public List<UserEntity> GetAll() => new();

        public UserEntity? GetById(Guid id) => null;

        public UserEntity? GetByUsername(string username) => null;

        public UserEntity? GetByNormalizedUsername(string normalizedUsername) => null;

        public void Add(UserEntity user) => throw new InvalidOperationException("database unavailable");

        public void Update(UserEntity user)
        {
        }
    }

    private sealed class ThrowingGetAllUserRepository : IUserRepository
    {
        public List<UserEntity> GetAll() => throw new InvalidOperationException("database unavailable");

        public UserEntity? GetById(Guid id) => null;

        public UserEntity? GetByUsername(string username) => null;

        public UserEntity? GetByNormalizedUsername(string normalizedUsername) => null;

        public void Add(UserEntity user)
        {
        }

        public void Update(UserEntity user)
        {
        }
    }
}
