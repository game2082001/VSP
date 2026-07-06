using VSP.Device.Import.Validation;

namespace VSP.Device.Import;

public class ImportPipelineResult
{
    public IReadOnlyList<ImportValidationResult> Results { get; init; } = Array.Empty<ImportValidationResult>();
    public int TotalRows { get; init; }
    public int ValidRows { get; init; }
    public int InvalidRows { get; init; }
}
