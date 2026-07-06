namespace VSP.Device.Import.Validation;

public class ImportValidationResult
{
    public ImportRow Row { get; init; } = new();
    public int RowNumber { get; init; }
    public bool IsValid { get; init; }
    public IReadOnlyList<ImportValidationMessage> Messages { get; init; } = Array.Empty<ImportValidationMessage>();
}
