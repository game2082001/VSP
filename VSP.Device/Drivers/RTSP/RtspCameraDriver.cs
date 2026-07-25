using VSP.Device.Drivers.Abstractions;
using VSP.Domain.Entities;

namespace VSP.Device.Drivers.RTSP;

public class RtspCameraDriver : ICameraDriver
{
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(3);

    public string DriverId => "generic.rtsp";

    public string DisplayName => "Generic RTSP Driver";

    public DeviceCapability Capability { get; } = new()
    {
        SupportsLiveView = true,
        SupportsSnapshot = true,
        SupportsPlayback = false,
        SupportsPTZ = false,
        SupportsAudio = false,
        SupportsEvent = false,
        SupportsDiscovery = false
    };

    public bool TestConnection(Camera camera)
    {
        try
        {
            if (!Uri.TryCreate(camera.RtspUrl, UriKind.Absolute, out var endpointUri)
                || !endpointUri.Scheme.Equals("rtsp", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(endpointUri.Host))
            {
                return false;
            }

            var response = SendDescribe(camera.RtspUrl, endpointUri, cSeq: 1, authorizationHeaderValue: null);

            if (IsSuccess(response.StatusCode))
            {
                return true;
            }

            if (response.StatusCode != 401 || string.IsNullOrEmpty(camera.Username))
            {
                return false;
            }

            var challenge = RtspWwwAuthenticateParser.Parse(response.WwwAuthenticate);
            if (challenge is null)
            {
                return false;
            }

            var authorizationHeaderValue = RtspAuthorizationHeaderBuilder.Build(
                challenge,
                camera.Username,
                camera.Password,
                "DESCRIBE",
                camera.RtspUrl);

            if (string.IsNullOrEmpty(authorizationHeaderValue))
            {
                return false;
            }

            var retryResponse = SendDescribe(camera.RtspUrl, endpointUri, cSeq: 2, authorizationHeaderValue);

            return IsSuccess(retryResponse.StatusCode);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static RtspDescribeResponse SendDescribe(
        string rtspUrl,
        Uri endpointUri,
        int cSeq,
        string? authorizationHeaderValue)
    {
        var requestPayload = RtspDescribeRequestFactory.Create(rtspUrl, cSeq, authorizationHeaderValue);
        var port = endpointUri.Port > 0 ? endpointUri.Port : 554;

        var responsePayload = TcpRtspTransport.SendAndReceive(
            requestPayload,
            endpointUri.Host,
            port,
            ConnectTimeout,
            ReadTimeout);

        return RtspDescribeResponseParser.Parse(responsePayload);
    }

    private static bool IsSuccess(int? statusCode)
    {
        return statusCode is >= 200 and < 300;
    }

    public bool StartLive(Camera camera)
    {
        // TODO: Milestone 4 Live View
        return false;
    }

    public bool StopLive(Camera camera)
    {
        // TODO: Milestone 4 Live View
        return false;
    }

    public bool Snapshot(Camera camera)
    {
        // TODO: Milestone 4 Snapshot
        return false;
    }
}