using System.Net;

namespace VSP.Device.Import.Validation;

public class ImportValidationEngine
{
    public ImportValidationResult Validate(ImportRow? row)
    {
        if (row is null)
        {
            var nullMessages = new[]
            {
                CreateMessage(0, "Row", "Required", "Import row is required.", ImportValidationSeverity.Error)
            };

            return new ImportValidationResult
            {
                Row = new ImportRow(),
                RowNumber = 0,
                IsValid = false,
                Messages = nullMessages
            };
        }

        var messages = new List<ImportValidationMessage>();

        ValidateRequired(row, messages, "Name");
        ValidateRequired(row, messages, "Brand");
        ValidateRequired(row, messages, "IP Address");
        ValidateRequired(row, messages, "Username");
        ValidateRequired(row, messages, "Connection Type");

        ValidateIpAddress(row, messages);
        ValidatePort(row, messages, "HTTP Port");
        ValidatePort(row, messages, "RTSP Port");
        ValidatePort(row, messages, "SDK Port");
        ValidateRtspUrl(row, messages);

        return new ImportValidationResult
        {
            Row = row,
            RowNumber = row.RowNumber,
            IsValid = messages.All(x => x.Severity != ImportValidationSeverity.Error),
            Messages = messages
        };
    }

    public IReadOnlyList<ImportValidationResult> Validate(IEnumerable<ImportRow>? rows)
    {
        if (rows is null)
        {
            return new[] { Validate((ImportRow?)null) };
        }

        return rows.Select(Validate).ToList();
    }

    private static void ValidateRequired(ImportRow row, List<ImportValidationMessage> messages, string fieldName)
    {
        if (!TryGetValue(row, fieldName, out var value) || string.IsNullOrWhiteSpace(value))
        {
            messages.Add(CreateMessage(
                row.RowNumber,
                fieldName,
                "Required",
                $"{fieldName} is required.",
                ImportValidationSeverity.Error));
        }
    }

    private static void ValidateIpAddress(ImportRow row, List<ImportValidationMessage> messages)
    {
        if (!TryGetValue(row, "IP Address", out var ipAddress) || string.IsNullOrWhiteSpace(ipAddress))
        {
            return;
        }

        if (!IPAddress.TryParse(ipAddress, out var parsedAddress) || parsedAddress.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            messages.Add(CreateMessage(
                row.RowNumber,
                "IP Address",
                "InvalidIpAddress",
                "IP Address must be a valid IPv4 address.",
                ImportValidationSeverity.Error));
        }
    }

    private static void ValidatePort(ImportRow row, List<ImportValidationMessage> messages, string fieldName)
    {
        if (!TryGetValue(row, fieldName, out var portValue) || string.IsNullOrWhiteSpace(portValue))
        {
            return;
        }

        if (!int.TryParse(portValue, out var port) || port < 1 || port > 65535)
        {
            messages.Add(CreateMessage(
                row.RowNumber,
                fieldName,
                "InvalidPort",
                $"{fieldName} must be between 1 and 65535.",
                ImportValidationSeverity.Error));
        }
    }

    private static void ValidateRtspUrl(ImportRow row, List<ImportValidationMessage> messages)
    {
        if (!TryGetValue(row, "Brand", out var brand) || !string.Equals(brand, "RTSP", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!TryGetValue(row, "RTSP URL", out var rtspUrl) || string.IsNullOrWhiteSpace(rtspUrl))
        {
            messages.Add(CreateMessage(
                row.RowNumber,
                "RTSP URL",
                "Required",
                "RTSP URL is required when Brand is RTSP.",
                ImportValidationSeverity.Error));
            return;
        }

        if (!rtspUrl.StartsWith("rtsp://", StringComparison.OrdinalIgnoreCase))
        {
            messages.Add(CreateMessage(
                row.RowNumber,
                "RTSP URL",
                "InvalidRtspUrl",
                "RTSP URL must start with rtsp://.",
                ImportValidationSeverity.Error));
        }
    }

    private static bool TryGetValue(ImportRow row, string fieldName, out string value)
    {
        if (row.Values.TryGetValue(fieldName, out var existingValue))
        {
            value = existingValue;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static ImportValidationMessage CreateMessage(
        int rowNumber,
        string fieldName,
        string code,
        string message,
        ImportValidationSeverity severity)
    {
        return new ImportValidationMessage
        {
            RowNumber = rowNumber,
            FieldName = fieldName,
            Code = code,
            Message = message,
            Severity = severity
        };
    }
}
