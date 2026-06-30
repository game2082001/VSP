using System.IO;
using System.Linq;

namespace VSP.Device.Import;

public class ImportService
{
    private readonly IReadOnlyList<IImportParser> _parsers;

    public ImportService(IEnumerable<IImportParser>? parsers = null)
    {
        _parsers = parsers is null
            ? Array.Empty<IImportParser>()
            : parsers.ToList();
    }

    public IReadOnlyList<IImportParser> Parsers => _parsers;

    public bool CanImport(string fileType)
    {
        return _parsers.Any(parser => parser.CanParse(fileType));
    }

    public ImportResult PrepareImport(ImportContext context, Stream stream)
    {
        var parser = _parsers.FirstOrDefault(x => x.CanParse(context.FileType));

        if (parser is null)
        {
            return new ImportResult
            {
                Summary = new ImportSummary
                {
                    Title = "Import Framework",
                    Description = "No parser is registered for the selected file type."
                }
            };
        }

        var rows = parser.Parse(stream).ToList();

        return new ImportResult
        {
            TotalRows = rows.Count,
            Rows = rows,
            Summary = new ImportSummary
            {
                Title = "Import Framework",
                Description = $"Prepared {rows.Count} row(s) for downstream import steps."
            }
        };
    }
}
