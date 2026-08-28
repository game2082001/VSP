using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using VSP.UI.ViewModels;

namespace VSP.UI.Views;

public partial class UsersView : UserControl
{
    private readonly UsersViewModel _viewModel;
    private bool _clearingPasswordBoxes;

    public UsersView(UsersViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.PropertyChanged += HandleViewModelPropertyChanged;
    }

    private void HandleTemporaryPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_clearingPasswordBoxes)
        {
            _viewModel.TemporaryPassword = TemporaryPasswordBox.Password;
        }
    }

    private void HandleConfirmTemporaryPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_clearingPasswordBoxes)
        {
            _viewModel.ConfirmTemporaryPassword = ConfirmTemporaryPasswordBox.Password;
        }
    }

    private void HandleResetTemporaryPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_clearingPasswordBoxes)
        {
            _viewModel.ResetTemporaryPassword = ResetTemporaryPasswordBox.Password;
        }
    }

    private void HandleConfirmResetTemporaryPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_clearingPasswordBoxes)
        {
            _viewModel.ConfirmResetTemporaryPassword = ConfirmResetTemporaryPasswordBox.Password;
        }
    }

    private void HandleViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(UsersViewModel.TemporaryPassword)
            or nameof(UsersViewModel.ConfirmTemporaryPassword)
            or nameof(UsersViewModel.ResetTemporaryPassword)
            or nameof(UsersViewModel.ConfirmResetTemporaryPassword))
        {
            SyncClearedPasswordBoxes();
        }
    }

    private void SyncClearedPasswordBoxes()
    {
        _clearingPasswordBoxes = true;
        try
        {
            ClearPasswordBoxWhenViewModelValueIsEmpty(TemporaryPasswordBox, _viewModel.TemporaryPassword);
            ClearPasswordBoxWhenViewModelValueIsEmpty(ConfirmTemporaryPasswordBox, _viewModel.ConfirmTemporaryPassword);
            ClearPasswordBoxWhenViewModelValueIsEmpty(ResetTemporaryPasswordBox, _viewModel.ResetTemporaryPassword);
            ClearPasswordBoxWhenViewModelValueIsEmpty(ConfirmResetTemporaryPasswordBox, _viewModel.ConfirmResetTemporaryPassword);
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
