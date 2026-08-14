using System.Xml.Linq;

namespace VSP.Device.Drivers.ONVIF;

public sealed class OnvifMediaServiceResponseParser
{
    public bool HasFault(string? responseXml)
    {
        var document = TryParse(responseXml);
        return document is null || HasFault(document);
    }

    /// <summary>
    /// Extracts the Media service's advertised address from a <c>GetCapabilitiesResponse</c>.
    /// Matched purely by element LocalName (ignoring the response's own namespace prefixes),
    /// the same tolerant approach <see cref="OnvifDeviceManagementResponseParser"/> already
    /// uses -- real devices vary in prefix choice, never in local element names.
    /// </summary>
    public string? ParseMediaXAddr(string? responseXml)
    {
        var document = TryParse(responseXml);
        if (document is null || HasFault(document))
        {
            return null;
        }

        var mediaElement = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Media");
        if (mediaElement is null)
        {
            return null;
        }

        return GetChildValue(mediaElement, "XAddr");
    }

    public IReadOnlyList<string> ParseProfileTokens(string? responseXml)
    {
        var document = TryParse(responseXml);
        if (document is null || HasFault(document))
        {
            return [];
        }

        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "Profiles")
            .Select(element => element.Attribute("token")?.Value)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token!)
            .ToList();
    }

    public string? ParseStreamUri(string? responseXml)
    {
        var document = TryParse(responseXml);
        if (document is null || HasFault(document))
        {
            return null;
        }

        var mediaUriElement = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "MediaUri");
        return mediaUriElement is null ? null : GetChildValue(mediaUriElement, "Uri");
    }

    private static XDocument? TryParse(string? responseXml)
    {
        if (string.IsNullOrWhiteSpace(responseXml))
        {
            return null;
        }

        try
        {
            return XDocument.Parse(responseXml, LoadOptions.None);
        }
        catch
        {
            return null;
        }
    }

    private static bool HasFault(XDocument document)
    {
        return document.Descendants().Any(element => element.Name.LocalName == "Fault");
    }

    private static string? GetChildValue(XElement element, string childName)
    {
        var value = element.Elements()
            .FirstOrDefault(child => child.Name.LocalName == childName)?
            .Value
            .Trim();

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
