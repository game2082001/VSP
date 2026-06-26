using VSP.Core.MVVM;

namespace VSP.UI.ViewModels;

public class MainWindowViewModel : ObservableObject
{
    private string _title = "VSP";
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
}