using System.Windows;
using VSP.UI.ViewModels;

namespace VSP.UI.Views;

public partial class BatchConnectionTestWindow : Window
{
    private readonly BatchConnectionTestViewModel _viewModel;

    public BatchConnectionTestWindow(BatchConnectionTestViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        _viewModel.RequestClose += Close;
    }
}
