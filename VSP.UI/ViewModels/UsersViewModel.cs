using System.Collections.ObjectModel;
using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.Logging;
using VSP.Core.MVVM;
using VSP.Device.Interfaces;
using VSP.Device.Users;
using VSP.Domain.Entities;
using VSP.Domain.Enums;

namespace VSP.UI.ViewModels;

public sealed class UsersViewModel : ObservableObject
{
    private readonly IUserRepository _userRepository;
    private readonly UserLifecycleService _userLifecycleService;
    private readonly User _currentUser;

    private UserListItemViewModel? _selectedUser;
    private string _newUsername = string.Empty;
    private Role _newUserRole = Role.Operator;
    private string _temporaryPassword = string.Empty;
    private string _confirmTemporaryPassword = string.Empty;
    private string _resetTemporaryPassword = string.Empty;
    private string _confirmResetTemporaryPassword = string.Empty;
    private string _statusMessage = string.Empty;

    public UsersViewModel(IUserRepository userRepository, UserLifecycleService userLifecycleService, User currentUser)
    {
        ArgumentNullException.ThrowIfNull(userRepository);
        ArgumentNullException.ThrowIfNull(userLifecycleService);
        ArgumentNullException.ThrowIfNull(currentUser);
        if (currentUser.Role != Role.Admin)
        {
            throw new UnauthorizedAccessException("User Management is only available to Admin users.");
        }

        _userRepository = userRepository;
        _userLifecycleService = userLifecycleService;
        _currentUser = currentUser;

        RoleOptions = new[] { Role.Operator, Role.Admin };
        CreateUserCommand = new RelayCommand(CreateUser);
        EnableSelectedUserCommand = new RelayCommand(EnableSelectedUser);
        DisableSelectedUserCommand = new RelayCommand(DisableSelectedUser);
        ResetSelectedUserPasswordCommand = new RelayCommand(ResetSelectedUserPassword);

        TryRefreshUsers();
    }

    public ObservableCollection<UserListItemViewModel> Users { get; } = new();

    public IReadOnlyList<Role> RoleOptions { get; }

    public UserListItemViewModel? SelectedUser
    {
        get => _selectedUser;
        set => SetProperty(ref _selectedUser, value);
    }

    public string NewUsername
    {
        get => _newUsername;
        set => SetProperty(ref _newUsername, value);
    }

    public Role NewUserRole
    {
        get => _newUserRole;
        set => SetProperty(ref _newUserRole, value);
    }

    public string TemporaryPassword
    {
        get => _temporaryPassword;
        set => SetProperty(ref _temporaryPassword, value);
    }

    public string ConfirmTemporaryPassword
    {
        get => _confirmTemporaryPassword;
        set => SetProperty(ref _confirmTemporaryPassword, value);
    }

    public string ResetTemporaryPassword
    {
        get => _resetTemporaryPassword;
        set => SetProperty(ref _resetTemporaryPassword, value);
    }

    public string ConfirmResetTemporaryPassword
    {
        get => _confirmResetTemporaryPassword;
        set => SetProperty(ref _confirmResetTemporaryPassword, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public ICommand CreateUserCommand { get; }

    public ICommand EnableSelectedUserCommand { get; }

    public ICommand DisableSelectedUserCommand { get; }

    public ICommand ResetSelectedUserPasswordCommand { get; }

    private void CreateUser()
    {
        if (TemporaryPassword != ConfirmTemporaryPassword)
        {
            StatusMessage = "Temporary password confirmation does not match.";
            ClearCreatePasswords();
            return;
        }

        var success = RunLifecycleAction(
            () => _userLifecycleService.CreateUser(NewUsername, TemporaryPassword, NewUserRole),
            successMessage: "User created.");
        ClearCreatePasswords();
        if (success)
        {
            NewUsername = string.Empty;
        }
    }

    private void EnableSelectedUser()
    {
        if (!TryGetSelectedUser(out var selected))
        {
            return;
        }

        RunLifecycleAction(
            () => _userLifecycleService.EnableUser(selected.Id),
            successMessage: "User enabled.");
    }

    private void DisableSelectedUser()
    {
        if (!TryGetSelectedUser(out var selected))
        {
            return;
        }

        RunLifecycleAction(
            () => _userLifecycleService.DisableUser(selected.Id, _currentUser.Id),
            successMessage: "User disabled.");
    }

    private void ResetSelectedUserPassword()
    {
        if (!TryGetSelectedUser(out var selected))
        {
            return;
        }

        if (ResetTemporaryPassword != ConfirmResetTemporaryPassword)
        {
            StatusMessage = "Temporary password confirmation does not match.";
            ClearResetPasswords();
            return;
        }

        RunLifecycleAction(
            () => _userLifecycleService.ResetPassword(selected.Id, _currentUser.Id, ResetTemporaryPassword),
            successMessage: "Password reset. The user must change it on next login.");
        ClearResetPasswords();
    }

    private bool RunLifecycleAction(Func<UserLifecycleResult> action, string successMessage)
    {
        try
        {
            var result = action();
            if (!result.Success)
            {
                StatusMessage = result.FailureMessage ?? "The user account could not be updated.";
                return false;
            }

            if (!TryRefreshUsers())
            {
                return false;
            }

            StatusMessage = successMessage;
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("User account operation failed.", ex);
            StatusMessage = "The user account could not be updated.";
            return false;
        }
    }

    private bool TryGetSelectedUser(out UserListItemViewModel selected)
    {
        if (SelectedUser is null)
        {
            StatusMessage = "Select a user first.";
            selected = null!;
            return false;
        }

        selected = SelectedUser;
        return true;
    }

    private bool TryRefreshUsers()
    {
        try
        {
            RefreshUsers();
            return true;
        }
        catch (Exception ex)
        {
            AppLog.Error("User list refresh failed.", ex);
            Users.Clear();
            SelectedUser = null;
            StatusMessage = "The user list could not be loaded.";
            return false;
        }
    }

    private void RefreshUsers()
    {
        var selectedId = SelectedUser?.Id;
        var users = _userRepository.GetAll()
            .OrderBy(user => user.Username, StringComparer.OrdinalIgnoreCase)
            .Select(user => new UserListItemViewModel(user))
            .ToList();

        Users.Clear();
        foreach (var user in users)
        {
            Users.Add(user);
        }

        SelectedUser = selectedId is null
            ? null
            : Users.FirstOrDefault(user => user.Id == selectedId.Value);
    }

    private void ClearCreatePasswords()
    {
        TemporaryPassword = string.Empty;
        ConfirmTemporaryPassword = string.Empty;
    }

    private void ClearResetPasswords()
    {
        ResetTemporaryPassword = string.Empty;
        ConfirmResetTemporaryPassword = string.Empty;
    }
}
