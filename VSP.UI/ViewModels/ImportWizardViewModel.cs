using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.MVVM;
using VSP.Device.Import;
using VSP.Device.Import.Execution;
using VSP.Device.Import.Preview;
using VSP.UI.Helpers;
using ExecutionImportResult = VSP.Device.Import.Execution.ImportResult;

namespace VSP.UI.ViewModels;

public class ImportWizardViewModel : ObservableObject
{
    private readonly ImportPipelineService _importPipelineService;
    private readonly ImportPreviewBuilder _importPreviewBuilder;
    private readonly ImportExecutor _importExecutor;
    private readonly ImportFileSelector _importFileSelector;
    private ImportPreviewResult _currentPreviewResult = new();

    private string _title = "Import Wizard";
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private string _selectedFileName = "";
    public string SelectedFileName
    {
        get => _selectedFileName;
        private set => SetProperty(ref _selectedFileName, value);
    }

    private string _selectedFileType = "";
    public string SelectedFileType
    {
        get => _selectedFileType;
        private set => SetProperty(ref _selectedFileType, value);
    }

    private string _statusMessage = "Select a file to preview import data.";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private string _currentStep = "Step 1 - Select File";
    public string CurrentStep
    {
        get => _currentStep;
        private set => SetProperty(ref _currentStep, value);
    }

    private string _selectedFilePath = "";
    public string SelectedFilePath
    {
        get => _selectedFilePath;
        private set
        {
            if (SetProperty(ref _selectedFilePath, value))
            {
                RefreshRelayCommand.RaiseCanExecuteChanged();
            }
        }
    }

    private int _totalRows;
    public int TotalRows
    {
        get => _totalRows;
        private set => SetProperty(ref _totalRows, value);
    }

    private int _validRows;
    public int ValidRows
    {
        get => _validRows;
        private set => SetProperty(ref _validRows, value);
    }

    private int _invalidRows;
    public int InvalidRows
    {
        get => _invalidRows;
        private set => SetProperty(ref _invalidRows, value);
    }

    public ObservableCollection<ImportPreviewRow> PreviewRows { get; } = new();

    public ICommand BrowseFileCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand CloseCommand { get; }

    private RelayCommand RefreshRelayCommand { get; }
    private RelayCommand ImportRelayCommand { get; }

    public event Action? CloseRequested;
    public event Action<ExecutionImportResult>? ImportCompleted;

    public ImportWizardViewModel(
        ImportPipelineService importPipelineService,
        ImportPreviewBuilder importPreviewBuilder,
        ImportExecutor importExecutor,
        ImportFileSelector importFileSelector)
    {
        _importPipelineService = importPipelineService;
        _importPreviewBuilder = importPreviewBuilder;
        _importExecutor = importExecutor;
        _importFileSelector = importFileSelector;

        BrowseFileCommand = new RelayCommand(BrowseFile);
        RefreshRelayCommand = new RelayCommand(Refresh, () => !string.IsNullOrWhiteSpace(SelectedFilePath));
        RefreshCommand = RefreshRelayCommand;
        ImportRelayCommand = new RelayCommand(Import, CanImport);
        ImportCommand = ImportRelayCommand;
        CloseCommand = new RelayCommand(Close);
    }

    private void BrowseFile()
    {
        var selectedFile = _importFileSelector.SelectFile();

        if (selectedFile is null)
        {
            StatusMessage = "File selection cancelled.";
            return;
        }

        SelectedFilePath = selectedFile.FilePath;
        SelectedFileName = Path.GetFileName(selectedFile.FilePath);
        SelectedFileType = selectedFile.FileType;

        LoadPreview();
    }

    private void Refresh()
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath))
        {
            StatusMessage = "Please select a file first.";
            return;
        }

        LoadPreview();
    }

    private void LoadPreview()
    {
        try
        {
            CurrentStep = "Step 2 - Preview";
            using var stream = File.OpenRead(SelectedFilePath);
            var pipelineResult = _importPipelineService.Process(stream, SelectedFileType);
            var previewResult = _importPreviewBuilder.Build(pipelineResult);
            _currentPreviewResult = previewResult;

            PreviewRows.Clear();

            foreach (var row in previewResult.Rows)
            {
                PreviewRows.Add(row);
            }

            TotalRows = previewResult.TotalRows;
            ValidRows = previewResult.ValidRows;
            InvalidRows = previewResult.InvalidRows;
            CurrentStep = "Step 3 - Ready to Import";
            StatusMessage = previewResult.TotalRows == 0
                ? "Preview loaded. No rows found."
                : "Preview loaded successfully.";
            ImportRelayCommand.RaiseCanExecuteChanged();
        }
        catch (NotSupportedException)
        {
            ResetPreview();
            CurrentStep = "Step 1 - Select File";
            StatusMessage = "Unsupported file type.";
        }
        catch (Exception ex)
        {
            ResetPreview();
            CurrentStep = "Step 1 - Select File";
            StatusMessage = $"Import preview failed: {ex.Message}";
        }
    }

    private bool CanImport()
    {
        return _currentPreviewResult.Rows.Any(row => row.IsValid);
    }

    private void Import()
    {
        var result = _importExecutor.Execute(_currentPreviewResult);
        StatusMessage = result.TotalRows == 0
            ? "No rows available for import."
            : $"Import completed. Imported: {result.ImportedRows}, Skipped: {result.SkippedRows}, Failed: {result.FailedRows}.";
        ImportCompleted?.Invoke(result);
    }

    private void Close()
    {
        CloseRequested?.Invoke();
    }

    private void ResetPreview()
    {
        _currentPreviewResult = new ImportPreviewResult();
        PreviewRows.Clear();
        TotalRows = 0;
        ValidRows = 0;
        InvalidRows = 0;
        ImportRelayCommand.RaiseCanExecuteChanged();
    }
}
