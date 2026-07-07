using System.Windows;
using VSP.Device.Import;
using VSP.Device.Import.Execution;
using VSP.Device.Import.Preview;
using VSP.Device.Repositories;
using VSP.UI.Helpers;
using VSP.UI.ViewModels;

namespace VSP.UI.Views;

public partial class ImportWizard : Window
{
    public ImportWizard()
        : this(new ImportWizardViewModel(
            new ImportPipelineService(),
            new ImportPreviewBuilder(),
            new ImportExecutor(new CameraRepository(), new CameraImportMapper()),
            new ImportFileSelector()))
    {
    }

    internal ImportWizard(ImportWizardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += HandleCloseRequested;
        viewModel.ImportCompleted += HandleImportCompleted;
        Closed += (_, _) => viewModel.CloseRequested -= HandleCloseRequested;
        Closed += (_, _) => viewModel.ImportCompleted -= HandleImportCompleted;
    }

    private void HandleCloseRequested()
    {
        Close();
    }

    private void HandleImportCompleted(VSP.Device.Import.Execution.ImportResult result)
    {
        var summaryWindow = new ImportSummaryWindow(new ImportSummaryViewModel(result))
        {
            Owner = this
        };

        summaryWindow.ShowDialog();
    }
}
