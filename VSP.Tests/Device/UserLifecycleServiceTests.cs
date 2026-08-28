using VSP.Core.Security;
using VSP.Device.Interfaces;
using VSP.Device.Users;
using VSP.Domain.Enums;
using Xunit;
using UserEntity = VSP.Domain.Entities.User;

namespace VSP.Tests.Device;

public class UserLifecycleServiceTests
{
    [Fact]
    public void CreateUser_WhenUsernameDiffersOnlyByCase_RejectsDuplicate()
    {
        var repository = new FakeUserRepository();
        repository.Add(User("Admin", Role.Admin));
        var service = new UserLifecycleService(repository);

        var result = service.CreateUser(" admin ", "temporary-password", Role.Operator);

        Assert.False(result.Success);
        Assert.Single(repository.Users);
    }

    [Fact]
    public void CreateUser_PersistsEnabledUserWithNormalizedIdentityAndForcedPasswordChange()
    {
        var repository = new FakeUserRepository();
        var service = new UserLifecycleService(repository);

        var result = service.CreateUser(" OperatorOne ", "temporary-password", Role.Operator);

        Assert.True(result.Success);
        var user = Assert.Single(repository.Users);
        Assert.Equal("OperatorOne", user.Username);
        Assert.Equal("OPERATORONE", user.NormalizedUsername);
        Assert.Equal(Role.Operator, user.Role);
        Assert.True(user.IsEnabled);
        Assert.True(user.MustChangePassword);
        Assert.True(PasswordHasher.Verify("temporary-password", user.PasswordHash, user.PasswordSalt, user.PasswordIterations));
    }

    [Fact]
    public void DisableUser_WhenTargetIsActingUser_RejectsSelfDisable()
    {
        var repository = new FakeUserRepository();
        var admin = User("admin", Role.Admin);
        repository.Add(admin);
        var service = new UserLifecycleService(repository);

        var result = service.DisableUser(admin.Id, admin.Id);

        Assert.False(result.Success);
        Assert.True(admin.IsEnabled);
    }

    [Fact]
    public void DisableUser_WhenTargetIsLastEnabledAdmin_RejectsDisable()
    {
        var repository = new FakeUserRepository();
        var admin = User("admin", Role.Admin);
        var operatorUser = User("operator", Role.Operator);
        repository.Add(admin);
        repository.Add(operatorUser);
        var service = new UserLifecycleService(repository);

        var result = service.DisableUser(admin.Id, operatorUser.Id);

        Assert.False(result.Success);
        Assert.True(admin.IsEnabled);
    }

    [Fact]
    public void DisableUser_WhenAnotherAdminRemains_DisablesTarget()
    {
        var repository = new FakeUserRepository();
        var firstAdmin = User("first-admin", Role.Admin);
        var secondAdmin = User("second-admin", Role.Admin);
        repository.Add(firstAdmin);
        repository.Add(secondAdmin);
        var service = new UserLifecycleService(repository);

        var result = service.DisableUser(firstAdmin.Id, secondAdmin.Id);

        Assert.True(result.Success);
        Assert.False(firstAdmin.IsEnabled);
        Assert.Same(firstAdmin, repository.LastUpdated);
    }

    [Fact]
    public void ResetPassword_WhenAdminTargetsSelf_RejectsReset()
    {
        var repository = new FakeUserRepository();
        var admin = User("admin", Role.Admin);
        repository.Add(admin);
        var service = new UserLifecycleService(repository);
        var originalHash = admin.PasswordHash;

        var result = service.ResetPassword(admin.Id, admin.Id, "new-temporary-password");

        Assert.False(result.Success);
        Assert.Equal(originalHash, admin.PasswordHash);
    }

    [Fact]
    public void ResetPassword_ReplacesSecretAndForcesPasswordChange()
    {
        var repository = new FakeUserRepository();
        var admin = User("admin", Role.Admin);
        var target = User("operator", Role.Operator);
        target.MustChangePassword = false;
        repository.Add(admin);
        repository.Add(target);
        var service = new UserLifecycleService(repository);

        var result = service.ResetPassword(target.Id, admin.Id, "new-temporary-password");

        Assert.True(result.Success);
        Assert.True(target.MustChangePassword);
        Assert.True(PasswordHasher.Verify("new-temporary-password", target.PasswordHash, target.PasswordSalt, target.PasswordIterations));
        Assert.Same(target, repository.LastUpdated);
    }

    [Fact]
    public void ChangeOwnPassword_WithCorrectCurrentPassword_ReplacesSecretAndClearsForcedChange()
    {
        var repository = new FakeUserRepository();
        var user = User("operator", Role.Operator, "old-password");
        user.MustChangePassword = true;
        repository.Add(user);
        var service = new UserLifecycleService(repository);

        var result = service.ChangeOwnPassword(user.Id, "old-password", "new-password");

        Assert.True(result.Success);
        Assert.False(user.MustChangePassword);
        Assert.True(PasswordHasher.Verify("new-password", user.PasswordHash, user.PasswordSalt, user.PasswordIterations));
        Assert.Same(user, repository.LastUpdated);
    }

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

        public UserEntity? LastUpdated { get; private set; }

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
            Users.Add(user);
        }

        public void Update(UserEntity user)
        {
            LastUpdated = user;
        }
    }
}
