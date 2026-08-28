using VSP.Domain.Entities;
using VSP.Domain.Enums;

namespace VSP.UI.ViewModels;

public sealed class UserListItemViewModel
{
    public UserListItemViewModel(User user)
    {
        Id = user.Id;
        Username = user.Username;
        Role = user.Role;
        IsEnabled = user.IsEnabled;
        MustChangePassword = user.MustChangePassword;
        CreateTime = user.CreateTime;
        LastModifyTime = user.LastModifyTime;
    }

    public Guid Id { get; }

    public string Username { get; }

    public Role Role { get; }

    public bool IsEnabled { get; }

    public string Status => IsEnabled ? "Enabled" : "Disabled";

    public bool MustChangePassword { get; }

    public DateTime CreateTime { get; }

    public DateTime LastModifyTime { get; }
}
