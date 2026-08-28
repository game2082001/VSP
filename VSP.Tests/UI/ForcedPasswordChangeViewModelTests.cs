using VSP.Core.Security;
using VSP.Device.Interfaces;
using VSP.Domain.Enums;
using VSP.UI.ViewModels;
using Xunit;
using UserEntity = VSP.Domain.Entities.User;

namespace VSP.Tests.UI;

public class ForcedPasswordChangeViewModelTests
{
    private static UserEntity AdminUser(string currentPassword = "admin", string username = "admin")
    {
        var (hash, salt, iterations) = PasswordHasher.Hash(currentPassword);
        return new UserEntity
        {
            Username = username,
            PasswordHash = hash,
            PasswordSalt = salt,
            PasswordIterations = iterations,
            Role = Role.Admin,
            MustChangePassword = true
        };
    }

    [Fact]
    public void ChangePassword_WithCorrectCurrentAndMatchingNewPassword_SucceedsAndClearsMustChangePassword()
    {
        var user = AdminUser();
        var repository = new FakeUserRepository();
        var viewModel = new ForcedPasswordChangeViewModel(user, repository)
        {
            CurrentPassword = "admin",
            NewPassword = "new-secure-password",
            ConfirmNewPassword = "new-secure-password"
        };
        var raised = false;
        viewModel.PasswordChangeSucceeded += () => raised = true;

        viewModel.ChangePasswordCommand.Execute(null);

        Assert.True(raised);
        Assert.Null(viewModel.ErrorMessage);
        Assert.Equal("Password changed.", viewModel.StatusMessage);
        Assert.False(user.MustChangePassword);
        Assert.Same(user, repository.LastUpdatedUser);
        Assert.Equal("", viewModel.CurrentPassword);
        Assert.Equal("", viewModel.NewPassword);
        Assert.Equal("", viewModel.ConfirmNewPassword);
    }

    [Fact]
    public void ChangePassword_OldPasswordNoLongerVerifiesAfterChange()
    {
        var user = AdminUser();
        var repository = new FakeUserRepository();
        var viewModel = new ForcedPasswordChangeViewModel(user, repository)
        {
            CurrentPassword = "admin",
            NewPassword = "new-secure-password",
            ConfirmNewPassword = "new-secure-password"
        };

        viewModel.ChangePasswordCommand.Execute(null);

        Assert.False(PasswordHasher.Verify("admin", user.PasswordHash, user.PasswordSalt, user.PasswordIterations));
    }

    [Fact]
    public void ChangePassword_NewPasswordVerifiesAfterChange()
    {
        var user = AdminUser();
        var repository = new FakeUserRepository();
        var viewModel = new ForcedPasswordChangeViewModel(user, repository)
        {
            CurrentPassword = "admin",
            NewPassword = "new-secure-password",
            ConfirmNewPassword = "new-secure-password"
        };

        viewModel.ChangePasswordCommand.Execute(null);

        Assert.True(PasswordHasher.Verify("new-secure-password", user.PasswordHash, user.PasswordSalt, user.PasswordIterations));
    }

    [Fact]
    public void ChangePassword_WithWrongCurrentPassword_IsRejectedAndDoesNotUpdate()
    {
        var user = AdminUser();
        var originalHash = user.PasswordHash;
        var repository = new FakeUserRepository();
        var viewModel = new ForcedPasswordChangeViewModel(user, repository)
        {
            CurrentPassword = "not-the-current-password",
            NewPassword = "new-secure-password",
            ConfirmNewPassword = "new-secure-password"
        };
        var raised = false;
        viewModel.PasswordChangeSucceeded += () => raised = true;

        viewModel.ChangePasswordCommand.Execute(null);

        Assert.False(raised);
        Assert.Equal("Current password is incorrect.", viewModel.ErrorMessage);
        Assert.Equal(originalHash, user.PasswordHash);
        Assert.True(user.MustChangePassword);
        Assert.Null(repository.LastUpdatedUser);
        Assert.Equal("", viewModel.CurrentPassword);
        Assert.Equal("", viewModel.NewPassword);
        Assert.Equal("", viewModel.ConfirmNewPassword);
    }

    [Fact]
    public void ChangePassword_WithMismatchedConfirmation_IsRejectedAndDoesNotUpdate()
    {
        var user = AdminUser();
        var originalHash = user.PasswordHash;
        var repository = new FakeUserRepository();
        var viewModel = new ForcedPasswordChangeViewModel(user, repository)
        {
            CurrentPassword = "admin",
            NewPassword = "new-secure-password",
            ConfirmNewPassword = "a-different-password"
        };
        var raised = false;
        viewModel.PasswordChangeSucceeded += () => raised = true;

        viewModel.ChangePasswordCommand.Execute(null);

        Assert.False(raised);
        Assert.Equal("New password and confirmation do not match.", viewModel.ErrorMessage);
        Assert.Equal(originalHash, user.PasswordHash);
        Assert.True(user.MustChangePassword);
        Assert.Null(repository.LastUpdatedUser);
    }

    [Fact]
    public void ChangePassword_WithEmptyNewPassword_IsRejectedAndDoesNotUpdate()
    {
        var user = AdminUser();
        var repository = new FakeUserRepository();
        var viewModel = new ForcedPasswordChangeViewModel(user, repository)
        {
            CurrentPassword = "admin",
            NewPassword = "",
            ConfirmNewPassword = ""
        };

        viewModel.ChangePasswordCommand.Execute(null);

        Assert.NotNull(viewModel.ErrorMessage);
        Assert.True(user.MustChangePassword);
        Assert.Null(repository.LastUpdatedUser);
    }

    // Milestone 18C password policy (§8, Additional Requirements): minimum 8 characters; reject
    // empty, whitespace-only, the username, and the current password.
    [Fact]
    public void ChangePassword_WithWhitespaceOnlyNewPassword_IsRejectedAndDoesNotUpdate()
    {
        var user = AdminUser();
        var repository = new FakeUserRepository();
        var viewModel = new ForcedPasswordChangeViewModel(user, repository)
        {
            CurrentPassword = "admin",
            NewPassword = "        ",
            ConfirmNewPassword = "        "
        };

        viewModel.ChangePasswordCommand.Execute(null);

        Assert.NotNull(viewModel.ErrorMessage);
        Assert.True(user.MustChangePassword);
        Assert.Null(repository.LastUpdatedUser);
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("1234567")]
    public void ChangePassword_WithNewPasswordShorterThanMinimumLength_IsRejectedAndDoesNotUpdate(string tooShort)
    {
        var user = AdminUser();
        var repository = new FakeUserRepository();
        var viewModel = new ForcedPasswordChangeViewModel(user, repository)
        {
            CurrentPassword = "admin",
            NewPassword = tooShort,
            ConfirmNewPassword = tooShort
        };

        viewModel.ChangePasswordCommand.Execute(null);

        Assert.NotNull(viewModel.ErrorMessage);
        Assert.True(user.MustChangePassword);
        Assert.Null(repository.LastUpdatedUser);
    }

    [Theory]
    [InlineData("long-admin-name")]
    [InlineData("LONG-ADMIN-NAME")]
    [InlineData("Long-Admin-Name")]
    public void ChangePassword_WithNewPasswordEqualToUsername_IsRejectedAndDoesNotUpdate(string usernameAsPassword)
    {
        var user = AdminUser(username: "long-admin-name");
        var repository = new FakeUserRepository();
        var viewModel = new ForcedPasswordChangeViewModel(user, repository)
        {
            CurrentPassword = "admin",
            NewPassword = usernameAsPassword,
            ConfirmNewPassword = usernameAsPassword
        };

        viewModel.ChangePasswordCommand.Execute(null);

        Assert.Equal("New password cannot be the same as the username.", viewModel.ErrorMessage);
        Assert.True(user.MustChangePassword);
        Assert.Null(repository.LastUpdatedUser);
    }

    [Fact]
    public void ChangePassword_WithNewPasswordEqualToCurrentPassword_IsRejectedAndDoesNotUpdate()
    {
        var user = AdminUser("current-password-123");
        var repository = new FakeUserRepository();
        var viewModel = new ForcedPasswordChangeViewModel(user, repository)
        {
            CurrentPassword = "current-password-123",
            NewPassword = "current-password-123",
            ConfirmNewPassword = "current-password-123"
        };

        viewModel.ChangePasswordCommand.Execute(null);

        Assert.NotNull(viewModel.ErrorMessage);
        Assert.True(user.MustChangePassword);
        Assert.Null(repository.LastUpdatedUser);
    }

    [Fact]
    public void ChangePassword_WithCompliantNewPassword_Succeeds()
    {
        var user = AdminUser();
        var repository = new FakeUserRepository();
        var viewModel = new ForcedPasswordChangeViewModel(user, repository)
        {
            CurrentPassword = "admin",
            NewPassword = "a-compliant-password",
            ConfirmNewPassword = "a-compliant-password"
        };

        viewModel.ChangePasswordCommand.Execute(null);

        Assert.Null(viewModel.ErrorMessage);
        Assert.False(user.MustChangePassword);
        Assert.Same(user, repository.LastUpdatedUser);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public UserEntity? LastUpdatedUser { get; private set; }

        public UserEntity? GetByUsername(string username) => null;

        public List<UserEntity> GetAll() => new();

        public UserEntity? GetById(Guid id) => null;

        public UserEntity? GetByNormalizedUsername(string normalizedUsername) => null;

        public void Add(UserEntity user)
        {
        }

        public void Update(UserEntity user)
        {
            LastUpdatedUser = user;
        }
    }
}
