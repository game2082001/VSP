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
        EditPasswordBox.Password = _viewModel.Password;
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

    private void HandlePasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            _viewModel.Password = passwordBox.Password;
        }
    }
}
