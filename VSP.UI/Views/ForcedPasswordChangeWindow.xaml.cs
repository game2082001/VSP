using System.Windows;
using VSP.UI.ViewModels;

namespace VSP.UI.Views;

public partial class ForcedPasswordChangeWindow : Window
{
    private readonly ForcedPasswordChangeViewModel _viewModel;

    public ForcedPasswordChangeWindow(ForcedPasswordChangeViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.PasswordChangeSucceeded += HandlePasswordChangeSucceeded;
    }

    private void HandlePasswordChangeSucceeded()
    {
        DialogResult = true;
        Close();
    }

    // Same PasswordBox code-behind pattern as LoginWindow/CameraDetailWindow -- WPF's PasswordBox
    // does not support data-binding its Password property directly.
    private void HandleCurrentPasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.CurrentPassword = CurrentPasswordBox.Password;
    }

    private void HandleNewPasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.NewPassword = NewPasswordBox.Password;
    }

    private void HandleConfirmNewPasswordChanged(object sender, RoutedEventArgs e)
    {
        _viewModel.ConfirmNewPassword = ConfirmNewPasswordBox.Password;
    }
}
