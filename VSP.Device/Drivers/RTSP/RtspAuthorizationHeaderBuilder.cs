using System.Security.Cryptography;
using System.Text;

namespace VSP.Device.Drivers.RTSP;

public static class RtspAuthorizationHeaderBuilder
{
    private const string DigestNonceCount = "00000001";

    public static string? Build(
        RtspAuthChallenge challenge,
        string username,
        string password,
        string method,
        string uri)
    {
        return challenge.Scheme.ToUpperInvariant() switch
        {
            "BASIC" => BuildBasic(username, password),
            "DIGEST" => BuildDigest(challenge, username, password, method, uri),
            _ => null
        };
    }

    public static string BuildBasic(string username, string password)
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        return $"Basic {encoded}";
    }

    public static string? BuildDigest(
        RtspAuthChallenge challenge,
        string username,
        string password,
        string method,
        string uri,
        string? cnonce = null)
    {
        if (string.IsNullOrEmpty(challenge.Nonce))
        {
            return null;
        }

        var realm = challenge.Realm ?? string.Empty;
        var nonce = challenge.Nonce;
        var ha1 = Md5Hex($"{username}:{realm}:{password}");
        var ha2 = Md5Hex($"{method}:{uri}");

        var usesQopAuth = ContainsAuthQop(challenge.Qop);

        string response;
        string? effectiveCnonce = null;

        if (usesQopAuth)
        {
            effectiveCnonce = cnonce ?? Guid.NewGuid().ToString("N");
            response = Md5Hex($"{ha1}:{nonce}:{DigestNonceCount}:{effectiveCnonce}:auth:{ha2}");
        }
        else
        {
            response = Md5Hex($"{ha1}:{nonce}:{ha2}");
        }

        var builder = new StringBuilder("Digest ");
        builder.Append($"username=\"{username}\", ");
        builder.Append($"realm=\"{realm}\", ");
        builder.Append($"nonce=\"{nonce}\", ");
        builder.Append($"uri=\"{uri}\", ");
        builder.Append($"response=\"{response}\"");

        if (usesQopAuth)
        {
            builder.Append($", qop=auth, nc={DigestNonceCount}, cnonce=\"{effectiveCnonce}\"");
        }

        if (!string.IsNullOrEmpty(challenge.Opaque))
        {
            builder.Append($", opaque=\"{challenge.Opaque}\"");
        }

        return builder.ToString();
    }

    private static bool ContainsAuthQop(string? qop)
    {
        if (string.IsNullOrWhiteSpace(qop))
        {
            return false;
        }

        return qop.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(static value => value.Equals("auth", StringComparison.OrdinalIgnoreCase));
    }

    private static string Md5Hex(string value)
    {
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(bytes);
    }
}
