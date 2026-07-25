using System.Windows;
using System.Windows.Controls;
using VSP.UI.ViewModels;

namespace VSP.UI.Views;

public partial class BatchEditWindow : Window
{
    private readonly BatchEditViewModel _viewModel;

    public BatchEditWindow(BatchEditViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.RequestClose += HandleRequestClose;
    }

    private void HandleApplyPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            _viewModel.Password = passwordBox.Password;
        }
    }

    private void HandleRequestClose()
    {
        DialogResult = _viewModel.WasApplied;
        Close();
    }
}
