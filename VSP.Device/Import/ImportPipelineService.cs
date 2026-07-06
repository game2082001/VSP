using System.IO;
using VSP.Device.Import.Validation;

namespace VSP.Device.Import;

public class ImportPipelineService
{
    private readonly ImportParserSelector _parserSelector;
    private readonly ImportValidationEngine _validationEngine;
    private readonly DuplicateChecker _duplicateChecker;

    public ImportPipelineService(
        IEnumerable<IImportParser>? parsers = null,
        ImportValidationEngine? validationEngine = null,
        DuplicateChecker? duplicateChecker = null)
    {
        var parserList = parsers?.ToList() ?? [new CsvImportParser(), new ExcelImportParser()];

        _parserSelector = new ImportParserSelector(parserList);
        _validationEngine = validationEngine ?? new ImportValidationEngine();
        _duplicateChecker = duplicateChecker ?? new DuplicateChecker();
    }

    public ImportPipelineResult Process(Stream stream, string fileType)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileType);

        var parser = _parserSelector.Select(fileType);
        var parsedRows = parser.Parse(stream).ToList();
        var validatedResults = _validationEngine.Validate(parsedRows);
        var finalResults = _duplicateChecker.Check(validatedResults);

        var validRows = finalResults.Count(result => result.IsValid);
        var invalidRows = finalResults.Count - validRows;

        return new ImportPipelineResult
        {
            Results = finalResults,
            TotalRows = parsedRows.Count,
            ValidRows = validRows,
            InvalidRows = invalidRows
        };
    }

    private sealed class ImportParserSelector
    {
        private readonly IReadOnlyList<IImportParser> _parsers;

        public ImportParserSelector(IReadOnlyList<IImportParser> parsers)
        {
            _parsers = parsers;
        }

        public IImportParser Select(string fileType)
        {
            var parser = _parsers.FirstOrDefault(candidate => candidate.CanParse(fileType));

            return parser ?? throw new NotSupportedException($"Unsupported import file type: {fileType}");
        }
    }
}
