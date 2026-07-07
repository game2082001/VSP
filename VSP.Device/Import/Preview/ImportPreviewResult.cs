namespace VSP.Device.Import.Preview;

public class ImportPreviewResult
{
    public IReadOnlyList<ImportPreviewRow> Rows { get; init; } = Array.Empty<ImportPreviewRow>();
    public int TotalRows { get; init; }
    public int ValidRows { get; init; }
    public int InvalidRows { get; init; }
}
