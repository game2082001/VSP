using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace VSP.Device.Drivers.ONVIF;

/// <summary>
/// Builds a WS-Security UsernameToken (PasswordDigest profile) header for ONVIF device
/// management calls that require authentication. Hand-rolled against BCL cryptography
/// (SHA-1, matching the WS-Security UsernameToken Profile 1.0 digest algorithm) — no
/// external SOAP/WS-Security package, consistent with this repository's existing
/// hand-rolled-XML house style (see <c>OnvifWsDiscoveryProbeBuilder</c>).
/// </summary>
public static class OnvifWsSecurityHeaderBuilder
{
    private static readonly XNamespace WsseNamespace =
        "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private static readonly XNamespace WsuNamespace =
        "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";

    public static XElement Build(string username, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);

        var nonceBytes = RandomNumberGenerator.GetBytes(16);
        var nonceBase64 = Convert.ToBase64String(nonceBytes);
        var created = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        var passwordDigest = ComputePasswordDigest(nonceBytes, created, password);

        return new XElement(
            WsseNamespace + "Security",
            new XAttribute(XNamespace.Xmlns + "wsse", WsseNamespace),
            new XAttribute(XNamespace.Xmlns + "wsu", WsuNamespace),
            new XElement(
                WsseNamespace + "UsernameToken",
                new XElement(WsseNamespace + "Username", username),
                new XElement(
                    WsseNamespace + "Password",
                    new XAttribute(
                        "Type",
                        "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest"),
                    passwordDigest),
                new XElement(
                    WsseNamespace + "Nonce",
                    new XAttribute(
                        "EncodingType",
                        "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary"),
                    nonceBase64),
                new XElement(WsuNamespace + "Created", created)));
    }

    private static string ComputePasswordDigest(byte[] nonceBytes, string created, string password)
    {
        var createdBytes = Encoding.UTF8.GetBytes(created);
        var passwordBytes = Encoding.UTF8.GetBytes(password ?? string.Empty);

        var combined = new byte[nonceBytes.Length + createdBytes.Length + passwordBytes.Length];
        Buffer.BlockCopy(nonceBytes, 0, combined, 0, nonceBytes.Length);
        Buffer.BlockCopy(createdBytes, 0, combined, nonceBytes.Length, createdBytes.Length);
        Buffer.BlockCopy(passwordBytes, 0, combined, nonceBytes.Length + createdBytes.Length, passwordBytes.Length);

        var hash = SHA1.HashData(combined);
        return Convert.ToBase64String(hash);
    }
}
