using System.Xml.Linq;
using VSP.Device.Drivers.Abstractions;

namespace VSP.Device.Drivers.ONVIF;

public sealed class OnvifDeviceManagementResponseParser
{
    public bool IsSuccessfulResponse(string? responseXml)
    {
        var document = TryParse(responseXml);
        return document is not null && !HasFault(document);
    }

    public DeviceInformation? ParseDeviceInformation(string? responseXml)
    {
        var document = TryParse(responseXml);
        if (document is null || HasFault(document))
        {
            return null;
        }

        var responseElement = document
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "GetDeviceInformationResponse");

        if (responseElement is null)
        {
            return null;
        }

        return new DeviceInformation
        {
            Manufacturer = GetChildValue(responseElement, "Manufacturer"),
            Model = GetChildValue(responseElement, "Model"),
            FirmwareVersion = GetChildValue(responseElement, "FirmwareVersion"),
            SerialNumber = GetChildValue(responseElement, "SerialNumber")
        };
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
