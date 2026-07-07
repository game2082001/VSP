using System.Collections.ObjectModel;
using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.MVVM;
using ExecutionImportError = VSP.Device.Import.Execution.ImportError;
using ExecutionImportResult = VSP.Device.Import.Execution.ImportResult;

namespace VSP.UI.ViewModels;

public class ImportSummaryViewModel : ObservableObject
{
    public string Title => "Import Summary";
    public int TotalRows { get; }
    public int ImportedRows { get; }
    public int SkippedRows { get; }
    public int FailedRows { get; }
    public ObservableCollection<ExecutionImportError> Errors { get; }
    public ICommand CloseCommand { get; }

    public event Action? RequestClose;

    public ImportSummaryViewModel(ExecutionImportResult? result)
    {
        TotalRows = result?.TotalRows ?? 0;
        ImportedRows = result?.ImportedRows ?? 0;
        SkippedRows = result?.SkippedRows ?? 0;
        FailedRows = result?.FailedRows ?? 0;
        Errors = new ObservableCollection<ExecutionImportError>(result?.Errors ?? []);
        CloseCommand = new RelayCommand(Close);
    }

    private void Close()
    {
        RequestClose?.Invoke();
    }
}
