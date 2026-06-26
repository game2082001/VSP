using System.Windows;

namespace VSP.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        MainContent.Content = new DashboardView();
    }
}