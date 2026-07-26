using System.Xml.Linq;

namespace VSP.Device.Drivers.ONVIF;

/// <summary>
/// Builds SOAP requests for the ONVIF device management service (distinct from WS-Discovery,
/// see <c>OnvifWsDiscoveryProbeBuilder</c>). <c>GetSystemDateAndTime</c> is callable without
/// authentication per the ONVIF spec (used for <c>TestConnection</c>); <c>GetDeviceInformation</c>
/// includes a WS-Security UsernameToken header when credentials are available.
/// </summary>
public sealed class OnvifDeviceManagementRequestFactory
{
    private static readonly XNamespace SoapNamespace = "http://www.w3.org/2003/05/soap-envelope";
    private static readonly XNamespace DeviceNamespace = "http://www.onvif.org/ver10/device/wsdl";

    public string BuildGetSystemDateAndTimeRequest()
    {
        return BuildEnvelope(new XElement(DeviceNamespace + "GetSystemDateAndTime"), securityHeader: null);
    }

    public string BuildGetDeviceInformationRequest(string? username, string? password)
    {
        var securityHeader = string.IsNullOrWhiteSpace(username)
            ? null
            : OnvifWsSecurityHeaderBuilder.Build(username, password ?? string.Empty);

        return BuildEnvelope(new XElement(DeviceNamespace + "GetDeviceInformation"), securityHeader);
    }

    private static string BuildEnvelope(XElement body, XElement? securityHeader)
    {
        var envelope = new XElement(
            SoapNamespace + "Envelope",
            new XAttribute(XNamespace.Xmlns + "s", SoapNamespace),
            new XAttribute(XNamespace.Xmlns + "tds", DeviceNamespace));

        if (securityHeader is not null)
        {
            envelope.Add(new XElement(SoapNamespace + "Header", securityHeader));
        }

        envelope.Add(new XElement(SoapNamespace + "Body", body));

        return new XDocument(envelope).ToString(SaveOptions.DisableFormatting);
    }
}
