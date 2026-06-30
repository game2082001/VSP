using ClosedXML.Excel;
using System.IO;

namespace VSP.Device.Import;

public class ExcelImportParser : IImportParser
{
    private static readonly string[] RequiredHeaders =
    {
        "Name",
        "Brand",
        "Model",
        "IP Address",
        "HTTP Port",
        "RTSP Port",
        "SDK Port",
        "Username",
        "Password",
        "Connection Type",
        "RTSP URL",
        "Location"
    };

    public string ParserName => "Excel";

    public bool CanParse(string fileType)
    {
        return fileType.Trim() switch
        {
            "xlsx" => true,
            ".xlsx" => true,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => true,
            _ => false
        };
    }

    public IEnumerable<ImportRow> Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet is null)
        {
            return Array.Empty<ImportRow>();
        }

        var usedRange = worksheet.RangeUsed();

        if (usedRange is null)
        {
            return Array.Empty<ImportRow>();
        }

        var headerRow = usedRange.FirstRowUsed();
        var headers = NormalizeHeaders(ReadRowValues(headerRow));

        if (headers.Count == 0)
        {
            return Array.Empty<ImportRow>();
        }

        var rows = new List<ImportRow>();

        foreach (var row in usedRange.RowsUsed().Skip(1))
        {
            var values = ReadRowValues(row, headers.Count);

            if (IsEmptyRow(values))
            {
                continue;
            }

            var importRow = new ImportRow
            {
                RowNumber = row.RowNumber()
            };

            for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
            {
                var value = columnIndex < values.Count ? values[columnIndex] : string.Empty;
                importRow.Values[headers[columnIndex]] = value;
            }

            rows.Add(importRow);
        }

        return rows;
    }

    private static List<string> ReadRowValues(IXLRangeRow row, int expectedColumnCount = -1)
    {
        var lastCellNumber = expectedColumnCount > 0
            ? expectedColumnCount
            : row.LastCellUsed()?.Address.ColumnNumber ?? 0;

        var values = new List<string>(lastCellNumber);

        for (var columnNumber = 1; columnNumber <= lastCellNumber; columnNumber++)
        {
            values.Add(row.Cell(columnNumber).GetValue<string>().Trim());
        }

        return values;
    }

    private static List<string> NormalizeHeaders(IReadOnlyList<string> headers)
    {
        var normalized = new List<string>(headers.Count);

        foreach (var header in headers)
        {
            var trimmedHeader = header.Trim();
            var matchedHeader = RequiredHeaders.FirstOrDefault(x =>
                string.Equals(x, trimmedHeader, StringComparison.OrdinalIgnoreCase));

            normalized.Add(matchedHeader ?? trimmedHeader);
        }

        return normalized;
    }

    private static bool IsEmptyRow(IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
        }

        return true;
    }
}
