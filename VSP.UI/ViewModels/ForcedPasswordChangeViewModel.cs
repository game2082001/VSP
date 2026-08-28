using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.MVVM;
using VSP.Device.Interfaces;
using VSP.Device.Users;
using VSP.Domain.Entities;

namespace VSP.UI.ViewModels;

/// <summary>
/// Password change flow shared by mandatory login-triggered changes and the logged-in Change
/// Password screen. Policy and mutation rules live in <see cref="UserLifecycleService"/>.
/// </summary>
public class ForcedPasswordChangeViewModel : ObservableObject
{
    private readonly User _user;
    private readonly UserLifecycleService _userLifecycleService;

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

    private string? _statusMessage;
    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand ChangePasswordCommand { get; }

    /// <summary>Raised once the new password has been persisted, so the hosting window can close itself.</summary>
    public event Action? PasswordChangeSucceeded;

    public ForcedPasswordChangeViewModel(User user, IUserRepository userRepository)
        : this(user, new UserLifecycleService(userRepository))
    {
    }

    public ForcedPasswordChangeViewModel(User user, UserLifecycleService userLifecycleService)
    {
        _user = user ?? throw new ArgumentNullException(nameof(user));
        _userLifecycleService = userLifecycleService ?? throw new ArgumentNullException(nameof(userLifecycleService));
        ChangePasswordCommand = new RelayCommand(ChangePassword);
    }

    private void ChangePassword()
    {
        try
        {
            var result = _userLifecycleService.ChangeOwnPassword(_user, CurrentPassword, NewPassword, ConfirmNewPassword);
            if (!result.Success)
            {
                ErrorMessage = result.FailureMessage ?? "Password could not be changed.";
                StatusMessage = null;
                ClearPasswords();
                return;
            }

            ErrorMessage = null;
            StatusMessage = "Password changed.";
            ClearPasswords();
            PasswordChangeSucceeded?.Invoke();
        }
        catch (Exception)
        {
            ErrorMessage = "Password could not be changed.";
            StatusMessage = null;
            ClearPasswords();
        }
    }

    private void ClearPasswords()
    {
        CurrentPassword = "";
        NewPassword = "";
        ConfirmNewPassword = "";
    }
}
