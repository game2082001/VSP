namespace VSP.Device.Import;

public class ImportRow
{
    public int RowNumber { get; set; }
    public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
}
