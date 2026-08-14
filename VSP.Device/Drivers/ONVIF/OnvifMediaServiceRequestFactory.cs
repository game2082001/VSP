using System.Xml.Linq;

namespace VSP.Device.Drivers.ONVIF;

/// <summary>
/// Builds SOAP requests for the ONVIF Media (ver10) service, plus the one Device service
/// call (<c>GetCapabilities</c>) needed to discover the Media service's real address --
/// never hardcode <c>/onvif/media_service</c>, since devices are not required to expose it
/// there. Media2 (ver20) is a documented future addition point (see class-level scope note
/// in <see cref="OnvifStreamUriResolver"/>), not implemented here -- do not expand
/// speculatively.
/// </summary>
public sealed class OnvifMediaServiceRequestFactory
{
    private static readonly XNamespace SoapNamespace = "http://www.w3.org/2003/05/soap-envelope";
    private static readonly XNamespace DeviceNamespace = "http://www.onvif.org/ver10/device/wsdl";
    private static readonly XNamespace MediaNamespace = "http://www.onvif.org/ver10/media/wsdl";
    private static readonly XNamespace SchemaNamespace = "http://www.onvif.org/ver10/schema";

    public string BuildGetCapabilitiesRequest(string? username, string? password)
    {
        var body = new XElement(
            DeviceNamespace + "GetCapabilities",
            new XElement(DeviceNamespace + "Category", "Media"));

        return BuildEnvelope(body, BuildSecurityHeader(username, password));
    }

    public string BuildGetProfilesRequest(string? username, string? password)
    {
        return BuildEnvelope(new XElement(MediaNamespace + "GetProfiles"), BuildSecurityHeader(username, password));
    }

    public string BuildGetStreamUriRequest(string profileToken, string? username, string? password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(profileToken);

        var body = new XElement(
            MediaNamespace + "GetStreamUri",
            new XElement(
                MediaNamespace + "StreamSetup",
                new XElement(SchemaNamespace + "Stream", "RTP-Unicast"),
                new XElement(
                    SchemaNamespace + "Transport",
                    new XElement(SchemaNamespace + "Protocol", "RTSP"))),
            new XElement(MediaNamespace + "ProfileToken", profileToken));

        return BuildEnvelope(body, BuildSecurityHeader(username, password));
    }

    private static XElement? BuildSecurityHeader(string? username, string? password)
    {
        return string.IsNullOrWhiteSpace(username)
            ? null
            : OnvifWsSecurityHeaderBuilder.Build(username, password ?? string.Empty);
    }

    private static string BuildEnvelope(XElement body, XElement? securityHeader)
    {
        var envelope = new XElement(
            SoapNamespace + "Envelope",
            new XAttribute(XNamespace.Xmlns + "s", SoapNamespace),
            new XAttribute(XNamespace.Xmlns + "tds", DeviceNamespace),
            new XAttribute(XNamespace.Xmlns + "trt", MediaNamespace),
            new XAttribute(XNamespace.Xmlns + "tt", SchemaNamespace));

        if (securityHeader is not null)
        {
            envelope.Add(new XElement(SoapNamespace + "Header", securityHeader));
        }

        envelope.Add(new XElement(SoapNamespace + "Body", body));

        return new XDocument(envelope).ToString(SaveOptions.DisableFormatting);
    }
}
