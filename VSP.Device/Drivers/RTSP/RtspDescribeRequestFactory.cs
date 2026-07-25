namespace VSP.Device.Drivers.RTSP;

public static class RtspDescribeRequestFactory
{
    private const string UserAgent = "VSP RTSP Client";

    public static string Create(string rtspUrl, int cSeq, string? authorizationHeaderValue = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rtspUrl);

        var authorizationLine = string.IsNullOrEmpty(authorizationHeaderValue)
            ? string.Empty
            : $"Authorization: {authorizationHeaderValue}\r\n";

        return
            $"DESCRIBE {rtspUrl} RTSP/1.0\r\n" +
            $"CSeq: {cSeq}\r\n" +
            $"User-Agent: {UserAgent}\r\n" +
            "Accept: application/sdp\r\n" +
            authorizationLine +
            "\r\n";
    }
}
