using VSP.Device.Import.Preview;
using VSP.Device.Interfaces;

namespace VSP.Device.Import.Execution;

public class ImportExecutor
{
    private readonly ICameraRepository _cameraRepository;
    private readonly CameraImportMapper _cameraImportMapper;

    public ImportExecutor(ICameraRepository cameraRepository, CameraImportMapper cameraImportMapper)
    {
        _cameraRepository = cameraRepository;
        _cameraImportMapper = cameraImportMapper;
    }

    public ImportResult Execute(ImportPreviewResult? previewResult)
    {
        if (previewResult is null || previewResult.Rows.Count == 0)
        {
            return new ImportResult();
        }

        var errors = new List<ImportError>();
        var importedRows = 0;
        var skippedRows = 0;
        var failedRows = 0;

        foreach (var row in previewResult.Rows)
        {
            if (!row.IsValid)
            {
                skippedRows++;
                errors.Add(new ImportError
                {
                    RowNumber = row.RowNumber,
                    Name = row.Name,
                    Message = BuildValidationMessage(row)
                });
                continue;
            }

            try
            {
                var camera = _cameraImportMapper.Map(row);
                _cameraRepository.Add(camera);
                importedRows++;
            }
            catch (Exception ex)
            {
                failedRows++;
                errors.Add(new ImportError
                {
                    RowNumber = row.RowNumber,
                    Name = row.Name,
                    Message = ex.Message
                });
            }
        }

        return new ImportResult
        {
            TotalRows = previewResult.Rows.Count,
            ImportedRows = importedRows,
            SkippedRows = skippedRows,
            FailedRows = failedRows,
            Errors = errors
        };
    }

    private static string BuildValidationMessage(ImportPreviewRow row)
    {
        if (row.Messages.Count == 0)
        {
            return "Row is invalid.";
        }

        return string.Join(" ", row.Messages.Select(message => message.Message));
    }
}
