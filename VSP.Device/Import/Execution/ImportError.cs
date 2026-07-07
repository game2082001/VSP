namespace VSP.Device.Import.Execution;

public class ImportError
{
    public int RowNumber { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
