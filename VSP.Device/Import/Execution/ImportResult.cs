namespace VSP.Device.Import.Execution;

public class ImportResult
{
    public int TotalRows { get; init; }
    public int ImportedRows { get; init; }
    public int SkippedRows { get; init; }
    public int FailedRows { get; init; }
    public IReadOnlyList<ImportError> Errors { get; init; } = Array.Empty<ImportError>();
}
