using VSP.Domain.Enums;
using VSP.UI.Services;
using Xunit;
using UserEntity = VSP.Domain.Entities.User;

namespace VSP.Tests.UI;

public class SessionServiceTests
{
    private static UserEntity NewUser(string username = "admin") => new()
    {
        Username = username,
        PasswordHash = "hash",
        PasswordSalt = "salt",
        PasswordIterations = 210_000,
        Role = Role.Admin
    };

    [Fact]
    public void CurrentUser_BeforeStart_IsNull()
    {
        var session = new SessionService();

        Assert.Null(session.CurrentUser);
    }

    [Fact]
    public void Start_SetsCurrentUser()
    {
        var session = new SessionService();
        var user = NewUser();

        session.Start(user);

        Assert.Same(user, session.CurrentUser);
    }

    [Fact]
    public void Start_WithNullUser_Throws()
    {
        var session = new SessionService();

        Assert.Throws<ArgumentNullException>(() => session.Start(null!));
    }

    [Fact]
    public void End_ClearsCurrentUser()
    {
        var session = new SessionService();
        session.Start(NewUser());

        session.End();

        Assert.Null(session.CurrentUser);
    }

    [Fact]
    public void End_ThenStart_AllowsANewSessionOnTheSameInstance()
    {
        // Not the production flow (App.xaml.cs always constructs a fresh SessionService per
        // login), but confirms End() leaves the instance in a valid, reusable state rather than
        // some poisoned "already ended" state.
        var session = new SessionService();
        var first = NewUser("admin");
        session.Start(first);
        session.End();

        var second = NewUser("operator");
        session.Start(second);

        Assert.Same(second, session.CurrentUser);
    }
}
