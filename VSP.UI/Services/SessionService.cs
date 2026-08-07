using VSP.Domain.Entities;

namespace VSP.UI.Services;

/// <summary>
/// Epic-018 Milestone 18D: the single owner of "who is logged in right now," for as long as
/// MainWindow is open (§4.10's session definition). Deliberately just a User holder --
/// no permission framework, no claims, no policies. Every existing role check (CameraListViewModel,
/// CameraDetailViewModel, MainWindowViewModel's own nav construction) still reads
/// <c>CurrentUser.Role</c> directly at the point of use, exactly as before this class existed;
/// this only centralizes *where that User comes from and how it's cleared*, not how it's used.
/// One instance per session -- App.xaml.cs's StartSession() constructs a brand new
/// SessionService on every login, never reuses one across a Logout.
/// </summary>
public class SessionService
{
    public User? CurrentUser { get; private set; }

    public void Start(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        CurrentUser = user;
    }

    public void End()
    {
        CurrentUser = null;
    }
}
