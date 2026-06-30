namespace VSP.Device.Import;

public class ImportResult
{
    public int TotalRows { get; set; }
    public int SuccessRows { get; set; }
    public int FailedRows { get; set; }
    public int WarningRows { get; set; }
    public ImportSummary Summary { get; set; } = new();
    public IReadOnlyList<ImportRow> Rows { get; set; } = Array.Empty<ImportRow>();
}
