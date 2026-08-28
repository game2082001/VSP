using VSP.Core.Logging;
using VSP.Core.Security;
using VSP.Device.Interfaces;
using VSP.Domain.Entities;
using VSP.Domain.Enums;

namespace VSP.Device.Users;

public sealed class UserLifecycleService
{
    private readonly IUserRepository _userRepository;

    public UserLifecycleService(IUserRepository userRepository)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    public UserLifecycleResult CreateUser(string username, string temporaryPassword, Role role)
    {
        if (!IsSupportedRole(role))
        {
            return UserLifecycleResult.Failed("The selected role is not supported.");
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            return UserLifecycleResult.Failed("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(temporaryPassword))
        {
            return UserLifecycleResult.Failed("Temporary password is required.");
        }

        var normalizedUsername = UsernameIdentity.Normalize(username);
        if (_userRepository.GetByNormalizedUsername(normalizedUsername) is not null)
        {
            return UserLifecycleResult.Failed("A user with that username already exists.");
        }

        var (hash, salt, iterations) = PasswordHasher.Hash(temporaryPassword);
        _userRepository.Add(new User
        {
            Username = username.Trim(),
            NormalizedUsername = normalizedUsername,
            PasswordHash = hash,
            PasswordSalt = salt,
            PasswordIterations = iterations,
            Role = role,
            IsEnabled = true,
            MustChangePassword = true
        });

        AppLog.Info($"User account created: role={role}.");
        return UserLifecycleResult.Ok();
    }

    public UserLifecycleResult DisableUser(Guid targetUserId, Guid actingUserId)
    {
        var target = _userRepository.GetById(targetUserId);
        if (target is null)
        {
            return UserLifecycleResult.Failed("User account was not found.");
        }

        if (target.Id == actingUserId)
        {
            return UserLifecycleResult.Failed("You cannot disable your own account.");
        }

        if (target.Role == Role.Admin && target.IsEnabled && CountEnabledAdminsExcept(target.Id) == 0)
        {
            return UserLifecycleResult.Failed("At least one enabled Admin account is required.");
        }

        target.IsEnabled = false;
        _userRepository.Update(target);
        AppLog.Info($"User account disabled: userId={target.Id:N}.");
        return UserLifecycleResult.Ok();
    }

    public UserLifecycleResult EnableUser(Guid targetUserId)
    {
        var target = _userRepository.GetById(targetUserId);
        if (target is null)
        {
            return UserLifecycleResult.Failed("User account was not found.");
        }

        target.IsEnabled = true;
        _userRepository.Update(target);
        AppLog.Info($"User account enabled: userId={target.Id:N}.");
        return UserLifecycleResult.Ok();
    }

    public UserLifecycleResult ResetPassword(Guid targetUserId, Guid actingUserId, string temporaryPassword)
    {
        var target = _userRepository.GetById(targetUserId);
        if (target is null)
        {
            return UserLifecycleResult.Failed("User account was not found.");
        }

        if (target.Id == actingUserId)
        {
            return UserLifecycleResult.Failed("Use Change Password to update your own password.");
        }

        if (string.IsNullOrWhiteSpace(temporaryPassword))
        {
            return UserLifecycleResult.Failed("Temporary password is required.");
        }

        var (hash, salt, iterations) = PasswordHasher.Hash(temporaryPassword);
        target.PasswordHash = hash;
        target.PasswordSalt = salt;
        target.PasswordIterations = iterations;
        target.MustChangePassword = true;
        _userRepository.Update(target);

        AppLog.Info($"User password reset: userId={target.Id:N}.");
        return UserLifecycleResult.Ok();
    }

    public UserLifecycleResult ChangeOwnPassword(
        Guid actingUserId,
        string currentPassword,
        string newPassword,
        string confirmNewPassword)
    {
        var user = _userRepository.GetById(actingUserId);
        if (user is null || !user.IsEnabled)
        {
            return UserLifecycleResult.Failed("Password could not be changed.");
        }

        return ChangeOwnPassword(user, currentPassword, newPassword, confirmNewPassword);
    }

    public UserLifecycleResult ChangeOwnPassword(
        User user,
        string currentPassword,
        string newPassword,
        string confirmNewPassword)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (!user.IsEnabled)
        {
            return UserLifecycleResult.Failed("Password could not be changed.");
        }

        if (!PasswordHasher.Verify(currentPassword, user.PasswordHash, user.PasswordSalt, user.PasswordIterations))
        {
            return UserLifecycleResult.Failed("Current password is incorrect.");
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return UserLifecycleResult.Failed("New password cannot be blank.");
        }

        if (newPassword.Length < PasswordPolicy.MinimumLength)
        {
            return UserLifecycleResult.Failed($"New password must be at least {PasswordPolicy.MinimumLength} characters.");
        }

        if (string.Equals(newPassword, user.Username, StringComparison.OrdinalIgnoreCase))
        {
            return UserLifecycleResult.Failed("New password cannot be the same as the username.");
        }

        if (newPassword == currentPassword)
        {
            return UserLifecycleResult.Failed("New password cannot be the same as the current password.");
        }

        if (newPassword != confirmNewPassword)
        {
            return UserLifecycleResult.Failed("New password and confirmation do not match.");
        }

        var (hash, salt, iterations) = PasswordHasher.Hash(newPassword);
        user.PasswordHash = hash;
        user.PasswordSalt = salt;
        user.PasswordIterations = iterations;
        user.MustChangePassword = false;
        _userRepository.Update(user);

        AppLog.Info($"Own password changed: userId={user.Id:N}.");
        return UserLifecycleResult.Ok();
    }

    private int CountEnabledAdminsExcept(Guid excludedUserId) =>
        _userRepository.GetAll().Count(user => user.Id != excludedUserId && user.Role == Role.Admin && user.IsEnabled);

    private static bool IsSupportedRole(Role role) => role is Role.Admin or Role.Operator;

    private static class PasswordPolicy
    {
        public const int MinimumLength = 8;
    }
}
