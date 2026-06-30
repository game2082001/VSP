using System.Windows.Input;
using VSP.Core.Commands;
using VSP.Core.MVVM;

namespace VSP.UI.ViewModels;

public class ImportWizardViewModel : ObservableObject
{
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
        set => SetProperty(ref _selectedFileName, value);
    }

    private string _selectedFileType = "";
    public string SelectedFileType
    {
        get => _selectedFileType;
        set => SetProperty(ref _selectedFileType, value);
    }

    private string _statusMessage = "Import framework ready.";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private int _currentStep = 1;
    public int CurrentStep
    {
        get => _currentStep;
        set => SetProperty(ref _currentStep, value);
    }

    public ICommand BrowseFileCommand { get; }
    public ICommand NextStepCommand { get; }
    public ICommand PreviousStepCommand { get; }
    public ICommand CloseCommand { get; }

    public ImportWizardViewModel()
    {
        BrowseFileCommand = new RelayCommand(BrowseFile);
        NextStepCommand = new RelayCommand(MoveNext);
        PreviousStepCommand = new RelayCommand(MovePrevious);
        CloseCommand = new RelayCommand(Close);
    }

    private void BrowseFile()
    {
        StatusMessage = "File selection is not implemented in Task-111A.";
    }

    private void MoveNext()
    {
        if (CurrentStep < 3)
        {
            CurrentStep++;
        }

        StatusMessage = "Wizard step changed.";
    }

    private void MovePrevious()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
        }

        StatusMessage = "Wizard step changed.";
    }

    private void Close()
    {
        StatusMessage = "Close action is not wired in Task-111A.";
    }
}
