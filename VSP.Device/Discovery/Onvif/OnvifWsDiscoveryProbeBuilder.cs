using System.Xml.Linq;

namespace VSP.Device.Discovery.Onvif;

public sealed class OnvifWsDiscoveryProbeBuilder
{
    public string BuildProbeMessage()
    {
        XNamespace soap = "http://www.w3.org/2003/05/soap-envelope";
        XNamespace addressing = "http://schemas.xmlsoap.org/ws/2004/08/addressing";
        XNamespace discovery = "http://schemas.xmlsoap.org/ws/2005/04/discovery";
        XNamespace deviceNetwork = "http://www.onvif.org/ver10/network/wsdl";

        var document = new XDocument(
            new XElement(
                soap + "Envelope",
                new XAttribute(XNamespace.Xmlns + "s", soap),
                new XAttribute(XNamespace.Xmlns + "a", addressing),
                new XAttribute(XNamespace.Xmlns + "d", discovery),
                new XAttribute(XNamespace.Xmlns + "dn", deviceNetwork),
                new XElement(
                    soap + "Header",
                    new XElement(addressing + "Action", "http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe"),
                    new XElement(addressing + "MessageID", $"urn:uuid:{Guid.NewGuid()}"),
                    new XElement(addressing + "To", "urn:schemas-xmlsoap-org:ws:2005:04:discovery")),
                new XElement(
                    soap + "Body",
                    new XElement(
                        discovery + "Probe",
                        new XElement(discovery + "Types", "dn:NetworkVideoTransmitter")))));

        return document.ToString(SaveOptions.DisableFormatting);
    }
}
