using VSP.Core.Logging;
using VSP.Device.Drivers.Abstractions;
using VSP.Domain;
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

    public bool TestConnection(Camera camera, CameraCredentials credentials)
    {
        try
        {
            if (!RtspEndpointResolver.TryResolve(camera.RtspUrl, camera.RtspPort, out var endpointUri)
                || !endpointUri.Scheme.Equals("rtsp", StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(endpointUri.Host))
            {
                return false;
            }

            var effectiveRtspUrl = endpointUri.AbsoluteUri;
            var response = SendDescribe(effectiveRtspUrl, endpointUri, cSeq: 1, authorizationHeaderValue: null);

            if (IsSuccess(response.StatusCode))
            {
                return true;
            }

            if (response.StatusCode != 401)
            {
                return false;
            }

            // Task-AI00B Phase 10: sanitized, read-only observation of the real challenge(s) the
            // camera sent on this 401 -- does not affect which challenge is selected below (still
            // response.WwwAuthenticate, the first header, exactly as before this phase) or how
            // Authorization is built. See RtspAuthChallengeDiagnostics for what is/isn't logged.
            var observedChallenges = RtspWwwAuthenticateParser.ParseAll(response.WwwAuthenticateValues);
            const int selectedChallengeIndex = 0;

            if (string.IsNullOrEmpty(credentials.Username))
            {
                AppLog.Info(RtspAuthChallengeDiagnostics.BuildSummary(observedChallenges, selectedChallengeIndex, secondResponseStatus: null));
                return false;
            }

            var challenge = RtspWwwAuthenticateParser.Parse(response.WwwAuthenticate);
            if (challenge is null)
            {
                AppLog.Info(RtspAuthChallengeDiagnostics.BuildSummary(observedChallenges, selectedChallengeIndex, secondResponseStatus: null));
                return false;
            }

            var authorizationHeaderValue = RtspAuthorizationHeaderBuilder.Build(
                challenge,
                credentials.Username,
                credentials.Password,
                "DESCRIBE",
                effectiveRtspUrl);

            if (string.IsNullOrEmpty(authorizationHeaderValue))
            {
                AppLog.Info(RtspAuthChallengeDiagnostics.BuildSummary(observedChallenges, selectedChallengeIndex, secondResponseStatus: null));
                return false;
            }

            var retryResponse = SendDescribe(effectiveRtspUrl, endpointUri, cSeq: 2, authorizationHeaderValue);

            AppLog.Info(RtspAuthChallengeDiagnostics.BuildSummary(observedChallenges, selectedChallengeIndex, retryResponse.StatusCode));

            return IsSuccess(retryResponse.StatusCode);
        }
        catch (Exception ex)
        {
            AppLog.Warning($"RTSP TestConnection failed for camera at {camera.IpAddress}.", ex);
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

        var responsePayload = TcpRtspTransport.SendAndReceive(
            requestPayload,
            endpointUri.Host,
            endpointUri.Port,
            ConnectTimeout,
            ReadTimeout);

        return RtspDescribeResponseParser.Parse(responsePayload);
    }

    private static bool IsSuccess(int? statusCode)
    {
        return statusCode is >= 200 and < 300;
    }

    public DeviceInformation? GetDeviceInformation(Camera camera, CameraCredentials credentials)
    {
        return null;
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
