using System.Windows.Controls;

namespace VSP.UI.ViewModels;

public class NavigationItem
{
    public string Title { get; set; } = "";
    public string Icon { get; set; } = "";
    public UserControl View { get; set; } = null!;
}