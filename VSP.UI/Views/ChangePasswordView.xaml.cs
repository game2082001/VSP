using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using VSP.UI.ViewModels;

namespace VSP.UI.Views;

public partial class ChangePasswordView : UserControl
{
    private readonly ForcedPasswordChangeViewModel _viewModel;
    private bool _clearingPasswordBoxes;

    public ChangePasswordView(ForcedPasswordChangeViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.PropertyChanged += HandleViewModelPropertyChanged;
    }

    private void HandleCurrentPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_clearingPasswordBoxes)
        {
            _viewModel.CurrentPassword = CurrentPasswordBox.Password;
        }
    }

    private void HandleNewPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_clearingPasswordBoxes)
        {
            _viewModel.NewPassword = NewPasswordBox.Password;
        }
    }

    private void HandleConfirmNewPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_clearingPasswordBoxes)
        {
            _viewModel.ConfirmNewPassword = ConfirmNewPasswordBox.Password;
        }
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ForcedPasswordChangeViewModel.CurrentPassword)
            or nameof(ForcedPasswordChangeViewModel.NewPassword)
            or nameof(ForcedPasswordChangeViewModel.ConfirmNewPassword))
        {
            SyncClearedPasswordBoxes();
        }
    }

    private void SyncClearedPasswordBoxes()
    {
        _clearingPasswordBoxes = true;
        try
        {
            ClearPasswordBoxWhenViewModelValueIsEmpty(CurrentPasswordBox, _viewModel.CurrentPassword);
            ClearPasswordBoxWhenViewModelValueIsEmpty(NewPasswordBox, _viewModel.NewPassword);
            ClearPasswordBoxWhenViewModelValueIsEmpty(ConfirmNewPasswordBox, _viewModel.ConfirmNewPassword);
        }
        finally
        {
            _clearingPasswordBoxes = false;
        }
    }

    private static void ClearPasswordBoxWhenViewModelValueIsEmpty(PasswordBox passwordBox, string viewModelValue)
    {
        if (viewModelValue.Length == 0 && passwordBox.Password.Length != 0)
        {
            passwordBox.Clear();
        }
    }
}
