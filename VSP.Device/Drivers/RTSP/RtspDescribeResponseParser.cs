namespace VSP.Device.Drivers.RTSP;

/// <summary>
/// <c>WwwAuthenticate</c> is the first <c>WWW-Authenticate</c> header line only -- unchanged
/// since before Task-AI00B Phase 10 and still the sole value <see cref="RtspWwwAuthenticateParser"/>
/// and <c>RtspCameraDriver</c> use to select/build the actual Authorization header. Behavior is
/// unchanged in Phase 10.
///
/// <c>WwwAuthenticateValues</c> is diagnostics-only (Phase 10): every <c>WWW-Authenticate</c>
/// header line found, in response order, so a camera sending multiple challenges (e.g. both
/// Digest and Basic) can be observed for logging. It is read but never acted upon for
/// authentication -- see <c>RtspAuthChallengeDiagnostics</c>.
/// </summary>
public sealed record RtspDescribeResponse(
    int? StatusCode,
    string? ReasonPhrase,
    string? WwwAuthenticate,
    IReadOnlyList<string> WwwAuthenticateValues);

public static class RtspDescribeResponseParser
{
    public static RtspDescribeResponse Parse(string? responsePayload)
    {
        if (string.IsNullOrEmpty(responsePayload))
        {
            return new RtspDescribeResponse(null, null, null, []);
        }

        var lines = responsePayload.Split(["\r\n", "\n"], StringSplitOptions.None);
        var statusLine = lines.FirstOrDefault(static line => !string.IsNullOrWhiteSpace(line))?.Trim();

        if (string.IsNullOrWhiteSpace(statusLine) || !statusLine.StartsWith("RTSP/", StringComparison.OrdinalIgnoreCase))
        {
            return new RtspDescribeResponse(null, null, null, []);
        }

        var parts = statusLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !int.TryParse(parts[1], out var statusCode))
        {
            return new RtspDescribeResponse(null, null, null, []);
        }

        var reasonPhrase = parts.Length >= 3 ? parts[2] : string.Empty;
        var wwwAuthenticate = GetHeaderValue(lines, "WWW-Authenticate");
        var wwwAuthenticateValues = GetHeaderValues(lines, "WWW-Authenticate");

        return new RtspDescribeResponse(statusCode, reasonPhrase, wwwAuthenticate, wwwAuthenticateValues);
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

    // Phase 10 diagnostics only: collects every matching header line instead of stopping at the
    // first one. Deliberately a separate method from GetHeaderValue above rather than a
    // refactor of it, so the existing single-value selection behavior (used for real
    // authentication) is provably untouched.
    private static List<string> GetHeaderValues(IEnumerable<string> lines, string headerName)
    {
        var values = new List<string>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith(headerName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var separatorIndex = line.IndexOf(':');
            if (separatorIndex < 0)
            {
                continue;
            }

            values.Add(separatorIndex == line.Length - 1 ? string.Empty : line[(separatorIndex + 1)..].Trim());
        }

        return values;
    }
}
