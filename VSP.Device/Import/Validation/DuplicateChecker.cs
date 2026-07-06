namespace VSP.Device.Import.Validation;

public class DuplicateChecker
{
    private static readonly IReadOnlyList<DuplicateRule> Rules =
    [
        new("Name", "DuplicateName", "Name"),
        new("IP Address", "DuplicateIpAddress", "IP Address"),
        new("RTSP URL", "DuplicateRtspUrl", "RTSP URL")
    ];

    public IReadOnlyList<ImportValidationResult> Check(IReadOnlyList<ImportValidationResult>? results)
    {
        if (results is null)
        {
            return Array.Empty<ImportValidationResult>();
        }

        var messagesByRowNumber = results.ToDictionary(
            result => result.RowNumber,
            result => result.Messages.ToList());

        foreach (var rule in Rules)
        {
            AppendDuplicateMessages(results, messagesByRowNumber, rule);
        }

        return results
            .Select(result => CreateUpdatedResult(result, messagesByRowNumber[result.RowNumber]))
            .ToList();
    }

    private static void AppendDuplicateMessages(
        IReadOnlyList<ImportValidationResult> results,
        IReadOnlyDictionary<int, List<ImportValidationMessage>> messagesByRowNumber,
        DuplicateRule rule)
    {
        var groups = results
            .Select(result => new
            {
                Result = result,
                Key = Normalize(rule.GetValue(result.Row))
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .GroupBy(x => x.Key!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1);

        foreach (var group in groups)
        {
            var orderedRows = group
                .Select(x => x.Result.RowNumber)
                .OrderBy(rowNumber => rowNumber)
                .ToList();

            foreach (var item in group)
            {
                var duplicateRows = orderedRows.Where(rowNumber => rowNumber != item.Result.RowNumber).ToList();
                if (duplicateRows.Count == 0)
                {
                    continue;
                }

                messagesByRowNumber[item.Result.RowNumber].Add(new ImportValidationMessage
                {
                    RowNumber = item.Result.RowNumber,
                    FieldName = rule.FieldName,
                    Code = rule.Code,
                    Message = $"{rule.FieldName} is duplicated with row(s): {string.Join(", ", duplicateRows)}.",
                    Severity = ImportValidationSeverity.Error
                });
            }
        }
    }

    private static ImportValidationResult CreateUpdatedResult(
        ImportValidationResult original,
        IReadOnlyList<ImportValidationMessage> messages)
    {
        return new ImportValidationResult
        {
            Row = original.Row,
            RowNumber = original.RowNumber,
            IsValid = messages.All(message => message.Severity != ImportValidationSeverity.Error),
            Messages = messages
        };
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private sealed class DuplicateRule(string fieldName, string code, string valueFieldName)
    {
        public string FieldName { get; } = fieldName;
        public string Code { get; } = code;

        public string? GetValue(ImportRow row)
        {
            return row.Values.TryGetValue(valueFieldName, out var value)
                ? value
                : null;
        }
    }
}
