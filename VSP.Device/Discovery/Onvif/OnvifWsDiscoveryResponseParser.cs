using System.Net;
using System.Xml.Linq;

namespace VSP.Device.Discovery.Onvif;

public sealed class OnvifWsDiscoveryResponseParser
{
    public IReadOnlyList<OnvifDiscoveryResult> Parse(string responseXml, IPEndPoint? remoteEndPoint = null)
    {
        if (string.IsNullOrWhiteSpace(responseXml))
        {
            return Array.Empty<OnvifDiscoveryResult>();
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(responseXml, LoadOptions.None);
        }
        catch
        {
            return Array.Empty<OnvifDiscoveryResult>();
        }

        var probeMatches = document
            .Descendants()
            .Where(static element => element.Name.LocalName == "ProbeMatch");

        var results = new List<OnvifDiscoveryResult>();

        foreach (var probeMatch in probeMatches)
        {
            var endpointReferenceAddress = GetDescendantValue(probeMatch, "EndpointReference", "Address");
            var xaddrs = SplitByWhitespace(GetDirectChildValue(probeMatch, "XAddrs"));
            var scopes = SplitByWhitespace(GetDirectChildValue(probeMatch, "Scopes"));
            var types = SplitByWhitespace(GetDirectChildValue(probeMatch, "Types"));
            var discoveryEndpoint = GetFirstUsableXAddr(xaddrs);
            var remoteAddress = remoteEndPoint?.Address.ToString();
            var ipAddress = GetIpAddress(discoveryEndpoint, remoteEndPoint);

            results.Add(new OnvifDiscoveryResult
            {
                EndpointReferenceAddress = endpointReferenceAddress,
                DiscoveryEndpoint = discoveryEndpoint,
                IpAddress = ipAddress,
                RemoteAddress = remoteAddress,
                Name = TryGetScopeValue(scopes, "name"),
                Location = TryGetScopeValue(scopes, "location"),
                Model = TryGetScopeValue(scopes, "hardware"),
                Manufacturer = null,
                Types = types,
                Scopes = scopes,
                XAddrs = xaddrs
            });
        }

        return results;
    }

    private static string? GetDescendantValue(XElement element, string containerName, string valueName)
    {
        return element
            .Descendants()
            .FirstOrDefault(descendant =>
                descendant.Name.LocalName == containerName)?
            .Descendants()
            .FirstOrDefault(descendant =>
                descendant.Name.LocalName == valueName)?
            .Value
            .Trim();
    }

    private static string? GetDirectChildValue(XElement element, string childName)
    {
        return element.Elements().FirstOrDefault(child => child.Name.LocalName == childName)?.Value.Trim();
    }

    private static IReadOnlyList<string> SplitByWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    private static string? GetFirstUsableXAddr(IReadOnlyList<string> xaddrs)
    {
        foreach (var xaddr in xaddrs)
        {
            if (Uri.TryCreate(xaddr, UriKind.Absolute, out _))
            {
                return xaddr;
            }
        }

        return null;
    }

    private static string? GetIpAddress(string? discoveryEndpoint, IPEndPoint? remoteEndPoint)
    {
        if (!string.IsNullOrWhiteSpace(discoveryEndpoint)
            && Uri.TryCreate(discoveryEndpoint, UriKind.Absolute, out var uri)
            && IPAddress.TryParse(uri.Host, out var ipAddress))
        {
            return ipAddress.ToString();
        }

        return remoteEndPoint?.Address.ToString();
    }

    private static string? TryGetScopeValue(IReadOnlyList<string> scopes, string scopeName)
    {
        foreach (var scope in scopes)
        {
            if (!Uri.TryCreate(scope, UriKind.Absolute, out var uri))
            {
                continue;
            }

            var segments = uri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            for (var i = 0; i < segments.Length - 1; i++)
            {
                if (!segments[i].Equals(scopeName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                return Uri.UnescapeDataString(string.Join("/", segments.Skip(i + 1)));
            }
        }

        return null;
    }
}
