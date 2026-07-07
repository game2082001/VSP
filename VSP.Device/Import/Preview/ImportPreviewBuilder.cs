using VSP.Device.Import.Validation;

namespace VSP.Device.Import.Preview;

public class ImportPreviewBuilder
{
    public ImportPreviewResult Build(ImportPipelineResult? pipelineResult)
    {
        if (pipelineResult is null)
        {
            return new ImportPreviewResult();
        }

        var rows = pipelineResult.Results
            .Select(BuildRow)
            .ToList();

        return new ImportPreviewResult
        {
            Rows = rows,
            TotalRows = pipelineResult.TotalRows,
            ValidRows = pipelineResult.ValidRows,
            InvalidRows = pipelineResult.InvalidRows
        };
    }

    private static ImportPreviewRow BuildRow(ImportValidationResult result)
    {
        return new ImportPreviewRow
        {
            RowNumber = result.RowNumber,
            Name = GetValue(result, "Name"),
            Brand = GetValue(result, "Brand"),
            Model = GetValue(result, "Model"),
            IPAddress = GetValue(result, "IP Address"),
            Location = GetValue(result, "Location"),
            IsValid = result.IsValid,
            Status = GetStatus(result),
            Messages = result.Messages
        };
    }

    private static string GetValue(ImportValidationResult result, string fieldName)
    {
        return result.Row.Values.TryGetValue(fieldName, out var value)
            ? value
            : string.Empty;
    }

    private static string GetStatus(ImportValidationResult result)
    {
        return result.IsValid
            ? "Valid"
            : "Invalid";
    }
}
