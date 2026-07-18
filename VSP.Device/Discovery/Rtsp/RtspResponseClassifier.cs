namespace VSP.Device.Discovery.Rtsp;

public sealed class RtspResponseClassifier
{
    public RtspResponseClassificationResult Classify(string? responsePayload)
    {
        if (string.IsNullOrEmpty(responsePayload))
        {
            return new RtspResponseClassificationResult
            {
                Classification = RtspProbeResponseClassification.InvalidResponse,
                ReceivedAnyBytes = false
            };
        }

        var lines = responsePayload
            .Split(["\r\n", "\n"], StringSplitOptions.None);
        var statusLine = lines.FirstOrDefault(static line => !string.IsNullOrWhiteSpace(line))?.Trim();

        if (string.IsNullOrWhiteSpace(statusLine))
        {
            return new RtspResponseClassificationResult
            {
                Classification = RtspProbeResponseClassification.InvalidResponse,
                ReceivedAnyBytes = !string.IsNullOrWhiteSpace(responsePayload)
            };
        }

        if (statusLine.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase))
        {
            return new RtspResponseClassificationResult
            {
                Classification = RtspProbeResponseClassification.ProtocolNotSupported,
                ReceivedAnyBytes = true,
                RawStatusLine = statusLine
            };
        }

        if (!statusLine.StartsWith("RTSP/", StringComparison.OrdinalIgnoreCase))
        {
            return new RtspResponseClassificationResult
            {
                Classification = RtspProbeResponseClassification.InvalidResponse,
                ReceivedAnyBytes = true,
                RawStatusLine = statusLine
            };
        }

        var parts = statusLine.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !int.TryParse(parts[1], out var statusCode))
        {
            return new RtspResponseClassificationResult
            {
                Classification = RtspProbeResponseClassification.InvalidResponse,
                ReceivedAnyBytes = true,
                IsRtspResponse = true,
                RawStatusLine = statusLine
            };
        }

        var wwwAuthenticate = GetHeaderValue(lines, "WWW-Authenticate");

        return new RtspResponseClassificationResult
        {
            Classification = ClassifyStatusCode(statusCode),
            ReceivedAnyBytes = true,
            IsRtspResponse = true,
            StatusCode = statusCode,
            ReasonPhrase = parts.Length >= 3 ? parts[2] : string.Empty,
            WwwAuthenticate = wwwAuthenticate,
            RawStatusLine = statusLine
        };
    }

    private static string? GetHeaderValue(IEnumerable<string> lines, string headerName)
    {
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!line.StartsWith(headerName, StringComparison.OrdinalIgnoreCase))
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

    private static RtspProbeResponseClassification ClassifyStatusCode(int statusCode)
    {
        return statusCode switch
        {
            200 => RtspProbeResponseClassification.Success,
            401 => RtspProbeResponseClassification.AuthenticationRequired,
            404 => RtspProbeResponseClassification.NotFound,
            405 => RtspProbeResponseClassification.MethodNotAllowed,
            _ => RtspProbeResponseClassification.UnknownFailure
        };
    }
}
