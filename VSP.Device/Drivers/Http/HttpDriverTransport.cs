using System.Text;

namespace VSP.Device.Drivers.Http;

/// <summary>
/// Bounded-timeout HTTP send/receive shared across HTTP-based camera drivers (ONVIF today,
/// a future Hikvision ISAPI driver later). Deliberately carries no protocol-specific
/// concerns (SOAP envelopes, WS-Security, Digest auth, ISAPI paths) — those stay in each
/// driver's own request/response code, mirroring how <c>RtspAuthorizationHeaderBuilder</c>
/// is RTSP-specific rather than shared. Mirrors <see cref="RTSP.TcpRtspTransport"/>'s
/// static, throw-on-failure shape rather than a Result/interface wrapper — the caller
/// (each driver's own TestConnection/GetDeviceInformation) is expected to catch and
/// translate failures, exactly as <c>RtspCameraDriver</c> already does.
/// </summary>
public static class HttpDriverTransport
{
    public static HttpDriverResponse Send(HttpDriverRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        using var httpClient = new HttpClient { Timeout = request.Timeout };
        using var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method), request.Endpoint);

        if (request.Body is not null)
        {
            httpRequest.Content = new StringContent(
                request.Body,
                Encoding.UTF8,
                request.ContentType ?? "text/xml");
        }

        using var httpResponse = httpClient.Send(httpRequest);
        var body = ReadBody(httpResponse);

        return new HttpDriverResponse
        {
            StatusCode = (int)httpResponse.StatusCode,
            Body = body
        };
    }

    private static string ReadBody(HttpResponseMessage response)
    {
        using var stream = response.Content.ReadAsStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
