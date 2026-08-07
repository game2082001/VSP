using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.Logging;
using VSP.Core.MVVM;
using VSP.Core.Security;
using VSP.Device.Interfaces;
using VSP.Domain.Entities;

namespace VSP.UI.ViewModels;

public class LoginViewModel : ObservableObject
{
    // Identical for "unknown username" and "wrong password" -- deliberately gives an attacker no
    // signal about which half of the credential pair was wrong (Epic-018 §4.13).
    private const string InvalidCredentialsMessage = "Invalid username or password.";

    private readonly IUserRepository _userRepository;

    private string _username = "";
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    private string _password = "";
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => SetProperty(ref _errorMessage, value);
    }

    /// <summary>Set only after a successful <see cref="LoginCommand"/> execution.</summary>
    public User? AuthenticatedUser { get; private set; }

    public ICommand LoginCommand { get; }

    /// <summary>Raised once, after <see cref="AuthenticatedUser"/> is set, so the hosting window can close itself.</summary>
    public event Action? LoginSucceeded;

    public LoginViewModel(IUserRepository userRepository)
    {
        _userRepository = userRepository;
        LoginCommand = new RelayCommand(Login);
    }

    private void Login()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = InvalidCredentialsMessage;
            return;
        }

        var user = _userRepository.GetByUsername(Username);

        if (user is null || !PasswordHasher.Verify(Password, user.PasswordHash, user.PasswordSalt, user.PasswordIterations))
        {
            AppLog.Warning($"Login failed for username '{Username}'.");
            ErrorMessage = InvalidCredentialsMessage;
            return;
        }

        AppLog.Info($"Login succeeded for username '{Username}'.");

        AuthenticatedUser = user;
        ErrorMessage = null;
        LoginSucceeded?.Invoke();
    }
}
