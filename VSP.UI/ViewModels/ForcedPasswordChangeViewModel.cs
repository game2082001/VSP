using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.MVVM;
using VSP.Core.Security;
using VSP.Device.Interfaces;
using VSP.Domain.Entities;

namespace VSP.UI.ViewModels;

/// <summary>
/// Mandatory, login-triggered password change (Epic-018 §4.18/Decision 5) -- not a general
/// Change Password feature (Decision 2 explicitly rules that out). Only ever shown when the
/// just-authenticated <see cref="User.MustChangePassword"/> is true; on success the flag is
/// cleared and never set again in v1.0.
/// </summary>
public class ForcedPasswordChangeViewModel : ObservableObject
{
    // Milestone 18C password policy -- deliberately not an "enterprise" policy (no complexity
    // scoring, no character-class rules, no history): a minimum length plus the handful of
    // trivially-guessable choices the Product Owner named explicitly.
    private const int MinimumPasswordLength = 8;

    private readonly User _user;
    private readonly IUserRepository _userRepository;

    private string _currentPassword = "";
    public string CurrentPassword
    {
        get => _currentPassword;
        set => SetProperty(ref _currentPassword, value);
    }

    private string _newPassword = "";
    public string NewPassword
    {
        get => _newPassword;
        set => SetProperty(ref _newPassword, value);
    }

    private string _confirmNewPassword = "";
    public string ConfirmNewPassword
    {
        get => _confirmNewPassword;
        set => SetProperty(ref _confirmNewPassword, value);
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    public ICommand ChangePasswordCommand { get; }

    /// <summary>Raised once the new password has been persisted, so the hosting window can close itself.</summary>
    public event Action? PasswordChangeSucceeded;

    public ForcedPasswordChangeViewModel(User user, IUserRepository userRepository)
    {
        _user = user;
        _userRepository = userRepository;
        ChangePasswordCommand = new RelayCommand(ChangePassword);
    }

    private void ChangePassword()
    {
        if (!PasswordHasher.Verify(CurrentPassword, _user.PasswordHash, _user.PasswordSalt, _user.PasswordIterations))
        {
            ErrorMessage = "Current password is incorrect.";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "New password cannot be blank.";
            return;
        }

        if (NewPassword.Length < MinimumPasswordLength)
        {
            ErrorMessage = $"New password must be at least {MinimumPasswordLength} characters.";
            return;
        }

        if (string.Equals(NewPassword, _user.Username, StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "New password cannot be the same as the username.";
            return;
        }

        if (NewPassword == CurrentPassword)
        {
            ErrorMessage = "New password cannot be the same as the current password.";
            return;
        }

        if (NewPassword != ConfirmNewPassword)
        {
            ErrorMessage = "New password and confirmation do not match.";
            return;
        }

        var (hash, salt, iterations) = PasswordHasher.Hash(NewPassword);

        _user.PasswordHash = hash;
        _user.PasswordSalt = salt;
        _user.PasswordIterations = iterations;
        _user.MustChangePassword = false;

        _userRepository.Update(_user);

        ErrorMessage = null;
        PasswordChangeSucceeded?.Invoke();
    }
}
