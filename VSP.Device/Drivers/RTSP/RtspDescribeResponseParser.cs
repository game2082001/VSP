namespace VSP.Device.Drivers.RTSP;

public sealed record RtspDescribeResponse(int? StatusCode, string? ReasonPhrase, string? WwwAuthenticate);

public static class RtspDescribeResponseParser
{
    public static RtspDescribeResponse Parse(string? responsePayload)
    {
        if (string.IsNullOrEmpty(responsePayload))
        {
            return new RtspDescribeResponse(null, null, null);
        }

        var lines = responsePayload.Split(["\r\n", "\n"], StringSplitOptions.None);
        var statusLine = lines.FirstOrDefault(static line => !string.IsNullOrWhiteSpace(line))?.Trim();

        if (string.IsNullOrWhiteSpace(statusLine) || !statusLine.StartsWith("RTSP/", StringComparison.OrdinalIgnoreCase))
        {
            return new RtspDescribeResponse(null, null, null);
        }

        var parts = statusLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !int.TryParse(parts[1], out var statusCode))
        {
            return new RtspDescribeResponse(null, null, null);
        }

        var reasonPhrase = parts.Length >= 3 ? parts[2] : string.Empty;
        var wwwAuthenticate = GetHeaderValue(lines, "WWW-Authenticate");

        return new RtspDescribeResponse(statusCode, reasonPhrase, wwwAuthenticate);
    }

    private static string? GetHeaderValue(IEnumerable<string> lines, string headerName)
    {
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith(headerName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex < 0 || separatorIndex == line.Length - 1)
            {
                return string.Empty;
            }

            return line[(separatorIndex + 1)..].Trim();
        }

        return null;
    }
}
