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
        EditPasswordBox.Password = _viewModel.Password;
    }

    private void HandleRequestClose()
    {
        Close();
    }

    private void HandlePasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox passwordBox)
        {
            _viewModel.Password = passwordBox.Password;
        }
    }
}
