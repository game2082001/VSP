using System.Windows;
using System.Windows.Controls;
using VSP.UI.ViewModels;

namespace VSP.UI.Views;

public partial class CameraDetailWindow : Window
{
    private readonly CameraDetailViewModel _viewModel;

    public CameraDetailWindow(CameraDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.RequestClose += HandleRequestClose;
        _viewModel.RequestUnsavedChangesConfirmation += HandleUnsavedChangesConfirmation;
        _viewModel.RequestDeleteConfirmation += HandleDeleteConfirmation;
    }

    private void HandleRequestClose()
    {
        Close();
    }

    private void HandleUnsavedChangesConfirmation()
    {
        var result = MessageBox.Show(
            "You have unsaved changes. Do you want to save before closing?",
            "Unsaved Changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        var decision = result switch
        {
            MessageBoxResult.Yes => UnsavedChangesDecision.Save,
            MessageBoxResult.No => UnsavedChangesDecision.Discard,
            _ => UnsavedChangesDecision.Cancel
        };

        _viewModel.HandleUnsavedChangesDecision(decision);
    }

    private void HandleDeleteConfirmation()
    {
        var result = MessageBox.Show(
            "Are you sure you want to delete this camera?",
            "Delete Camera",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        var decision = result == MessageBoxResult.Yes
            ? DeleteConfirmationDecision.Confirm
            : DeleteConfirmationDecision.Cancel;

        _viewModel.HandleDeleteConfirmationDecision(decision);
    }

    // Generic — works for any sensitive DriverSetting regardless of driver or key, since
    // WPF's PasswordBox does not support data-binding its Password property directly.
    // DataContext on a generated ItemsControl container is the DriverSettingEditorViewModel
    // itself, exactly like any other item template binding.

    private void HandleSettingPasswordBoxLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            passwordBox.Password = string.Empty;
        }
    }

    private void HandleSettingPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox { DataContext: DriverSettingEditorViewModel setting } passwordBox)
        {
            setting.Value = passwordBox.Password;
        }
    }
}
