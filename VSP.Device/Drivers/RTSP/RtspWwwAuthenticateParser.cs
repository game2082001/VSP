namespace VSP.Device.Drivers.RTSP;

/// <summary>
/// <c>Algorithm</c> and <c>Stale</c> (Task-AI00B Phase 10) are parsed and retained for
/// diagnostics only -- see <c>RtspAuthChallengeDiagnostics</c>. <see cref="RtspAuthorizationHeaderBuilder"/>
/// does not read either field; Digest response calculation remains plain-MD5-only, unchanged
/// by this phase. Defaulted to null so every existing positional call site (tests, callers)
/// keeps compiling unchanged.
/// </summary>
public sealed record RtspAuthChallenge(
    string Scheme,
    string? Realm,
    string? Nonce,
    string? Qop,
    string? Opaque,
    string? Algorithm = null,
    string? Stale = null);

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
            parameters.GetValueOrDefault("opaque"),
            parameters.GetValueOrDefault("algorithm"),
            parameters.GetValueOrDefault("stale"));
    }

    // Phase 10 diagnostics only: parses every raw WWW-Authenticate header value (as collected by
    // RtspDescribeResponseParser.GetHeaderValues) into its own challenge. Does not change, and is
    // not consulted by, which single challenge RtspCameraDriver actually authenticates against.
    public static IReadOnlyList<RtspAuthChallenge> ParseAll(IEnumerable<string> wwwAuthenticateValues)
    {
        var challenges = new List<RtspAuthChallenge>();

        foreach (var value in wwwAuthenticateValues)
        {
            var challenge = Parse(value);
            if (challenge is not null)
            {
                challenges.Add(challenge);
            }
        }

        return challenges;
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
