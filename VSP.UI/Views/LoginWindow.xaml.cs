using System.Windows;
using VSP.UI.ViewModels;

namespace VSP.UI.Views;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.LoginSucceeded += HandleLoginSucceeded;
    }

    private void HandleLoginSucceeded()
    {
        DialogResult = true;
        Close();
    }

    // WPF's PasswordBox does not support data-binding its Password property directly (by design,
    // to avoid the plaintext password living in a bindable dependency property) -- same
    // code-behind pattern already used by CameraDetailWindow's driver-setting PasswordBoxes.
    private void HandlePasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.Password = PasswordBox.Password;
    }
}
