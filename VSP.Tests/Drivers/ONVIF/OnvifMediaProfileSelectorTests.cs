using VSP.Device.Drivers.ONVIF;
using Xunit;

namespace VSP.Tests.Drivers.ONVIF;

public class OnvifMediaProfileSelectorTests
{
    [Fact]
    public void SelectProfile_WithMultipleTokens_ReturnsFirst()
    {
        var selected = OnvifMediaProfileSelector.SelectProfile(["Profile_1", "Profile_2"]);

        Assert.Equal("Profile_1", selected);
    }

    [Fact]
    public void SelectProfile_WithSingleToken_ReturnsIt()
    {
        var selected = OnvifMediaProfileSelector.SelectProfile(["Profile_1"]);

        Assert.Equal("Profile_1", selected);
    }

    [Fact]
    public void SelectProfile_WithNoTokens_ReturnsNull()
    {
        var selected = OnvifMediaProfileSelector.SelectProfile([]);

        Assert.Null(selected);
    }

    [Fact]
    public void SelectProfile_WithNullList_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => OnvifMediaProfileSelector.SelectProfile(null!));
    }
}
