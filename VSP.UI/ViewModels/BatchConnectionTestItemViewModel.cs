namespace VSP.UI.ViewModels;

public class BatchConnectionTestItemViewModel
{
    public BatchConnectionTestItemViewModel(string name, string ipAddress, bool isSuccess)
    {
        Name = name;
        IpAddress = ipAddress;
        IsSuccess = isSuccess;
    }

    public string Name { get; }

    public string IpAddress { get; }

    public bool IsSuccess { get; }

    public string ResultText => IsSuccess ? "Success" : "Failed";
}
