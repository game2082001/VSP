using System.Windows;
using VSP.UI.ViewModels;

namespace VSP.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = new MainWindowViewModel();
    }
}