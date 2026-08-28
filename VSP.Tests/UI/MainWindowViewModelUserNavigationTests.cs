using VSP.Domain.Enums;
using VSP.UI.ViewModels;
using Xunit;

namespace VSP.Tests.UI;

public class MainWindowViewModelUserNavigationTests
{
    [Fact]
    public void CanManageUsers_AdminCanSeeUserManagement()
    {
        Assert.True(MainWindowViewModel.CanManageUsers(Role.Admin));
    }

    [Fact]
    public void CanManageUsers_OperatorCannotSeeUserManagement()
    {
        Assert.False(MainWindowViewModel.CanManageUsers(Role.Operator));
    }
}
