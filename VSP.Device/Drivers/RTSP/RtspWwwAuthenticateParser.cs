namespace VSP.Device.Drivers.RTSP;

public sealed record RtspAuthChallenge(
    string Scheme,
    string? Realm,
    string? Nonce,
    string? Qop,
    string? Opaque);

public static class RtspWwwAuthenticateParser
{
    public static RtspAuthChallenge? Parse(string? wwwAuthenticateValue)
    {
        if (string.IsNullOrWhiteSpace(wwwAuthenticateValue))
        {
            return null;
        }

        var trimmed = wwwAuthenticateValue.Trim();
        var firstSpace = trimmed.IndexOf(' ');
        if (firstSpace < 0)
        {
            return new RtspAuthChallenge(trimmed, null, null, null, null);
        }

        var scheme = trimmed[..firstSpace];
        var parameters = ParseParameters(trimmed[(firstSpace + 1)..]);

        return new RtspAuthChallenge(
            scheme,
            parameters.GetValueOrDefault("realm"),
            parameters.GetValueOrDefault("nonce"),
            parameters.GetValueOrDefault("qop"),
            parameters.GetValueOrDefault("opaque"));
    }

    private static Dictionary<string, string> ParseParameters(string paramsSection)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rawPart in SplitParameters(paramsSection))
        {
            var separatorIndex = rawPart.IndexOf('=');
            if (separatorIndex < 0)
            {
                continue;
            }

            var key = rawPart[..separatorIndex].Trim();
            var value = rawPart[(separatorIndex + 1)..].Trim().Trim('"');

            if (key.Length > 0)
            {
                result[key] = value;
            }
        }

        return result;
    }

    private static IEnumerable<string> SplitParameters(string paramsSection)
    {
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        var insideQuotes = false;

        foreach (var c in paramsSection)
        {
            if (c == '"')
            {
                insideQuotes = !insideQuotes;
                current.Append(c);
                continue;
            }

            if (c == ',' && !insideQuotes)
            {
                parts.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        return parts;
    }
}
