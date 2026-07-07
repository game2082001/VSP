using VSP.Device.Import.Validation;

namespace VSP.Device.Import.Preview;

public class ImportPreviewRow
{
    public int RowNumber { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Brand { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public string IPAddress { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public bool IsValid { get; init; }
    public string Status { get; init; } = string.Empty;
    public IReadOnlyList<ImportValidationMessage> Messages { get; init; } = Array.Empty<ImportValidationMessage>();
}
