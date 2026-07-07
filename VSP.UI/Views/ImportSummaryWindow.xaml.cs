using System.Windows;
using VSP.UI.ViewModels;

namespace VSP.UI.Views;

public partial class ImportSummaryWindow : Window
{
    public ImportSummaryWindow(ImportSummaryViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += HandleRequestClose;
        Closed += (_, _) => viewModel.RequestClose -= HandleRequestClose;
    }

    private void HandleRequestClose()
    {
        Close();
    }
}
