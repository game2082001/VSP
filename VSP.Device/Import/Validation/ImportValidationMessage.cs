namespace VSP.Device.Import.Validation;

public class ImportValidationMessage
{
    public int RowNumber { get; init; }
    public string FieldName { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public ImportValidationSeverity Severity { get; init; }
}
