using System.IO;
using System.Text;

namespace VSP.Device.Import;

public class CsvImportParser : IImportParser
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

    public string ParserName => "CSV";

    public bool CanParse(string fileType)
    {
        return fileType.Trim() switch
        {
            "csv" => true,
            ".csv" => true,
            "text/csv" => true,
            _ => false
        };
    }

    public IEnumerable<ImportRow> Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var content = ReadContent(stream);

        if (string.IsNullOrWhiteSpace(content))
        {
            return Array.Empty<ImportRow>();
        }

        using var reader = new StringReader(content);

        var records = ReadRecords(reader).ToList();

        if (records.Count == 0)
        {
            return Array.Empty<ImportRow>();
        }

        var headers = NormalizeHeaders(records[0]);
        var rows = new List<ImportRow>();

        for (var i = 1; i < records.Count; i++)
        {
            var record = records[i];

            if (IsEmptyRow(record))
            {
                continue;
            }

            var row = new ImportRow
            {
                RowNumber = i + 1
            };

            for (var columnIndex = 0; columnIndex < headers.Count; columnIndex++)
            {
                var value = columnIndex < record.Count ? record[columnIndex] : string.Empty;
                row.Values[headers[columnIndex]] = value;
            }

            rows.Add(row);
        }

        return rows;
    }

    private static string ReadContent(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var buffer = ReadAllBytes(stream);

        foreach (var encoding in GetEncodings())
        {
            try
            {
                var text = encoding.GetString(buffer);

                if (LooksReadable(text))
                {
                    return RemoveBom(text);
                }
            }
            catch (DecoderFallbackException)
            {
            }
        }

        return RemoveBom(Encoding.UTF8.GetString(buffer));
    }

    private static byte[] ReadAllBytes(Stream stream)
    {
        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    private static IEnumerable<Encoding> GetEncodings()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        yield return new UTF8Encoding(false, true);
        yield return Encoding.GetEncoding(950, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        yield return Encoding.Default;
    }

    private static bool LooksReadable(string text)
    {
        return !text.Contains('\uFFFD');
    }

    private static string RemoveBom(string text)
    {
        return text.TrimStart('\uFEFF');
    }

    private static IEnumerable<List<string>> ReadRecords(StringReader reader)
    {
        var record = new StringBuilder();
        var quoteCount = 0;
        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            if (record.Length > 0)
            {
                record.AppendLine();
            }

            record.Append(line);
            quoteCount += CountQuotes(line);

            if (quoteCount % 2 != 0)
            {
                continue;
            }

            yield return ParseRecord(record.ToString());
            record.Clear();
            quoteCount = 0;
        }

        if (record.Length > 0)
        {
            yield return ParseRecord(record.ToString());
        }
    }

    private static int CountQuotes(string value)
    {
        var count = 0;

        foreach (var ch in value)
        {
            if (ch == '"')
            {
                count++;
            }
        }

        return count;
    }

    private static List<string> ParseRecord(string recordText)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < recordText.Length; i++)
        {
            var ch = recordText[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < recordText.Length && recordText[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                values.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        values.Add(current.ToString().Trim());
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

    private static bool IsEmptyRow(IEnumerable<string> record)
    {
        foreach (var value in record)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
        }

        return true;
    }
}
