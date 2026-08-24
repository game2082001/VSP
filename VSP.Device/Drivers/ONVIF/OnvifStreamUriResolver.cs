using VSP.Core.Logging;
using VSP.Device.Drivers.Http;
using VSP.Domain.Entities;

namespace VSP.Device.Drivers.ONVIF;

/// <summary>
/// Orchestrates ONVIF stream URI resolution: Device Service GetCapabilities (to discover the
/// Media service's real address -- never a hardcoded <c>/onvif/media_service</c> path) -&gt;
/// Media service GetProfiles -&gt; <see cref="OnvifMediaProfileSelector"/> -&gt; GetStreamUri.
/// SOAP construction lives in <see cref="OnvifMediaServiceRequestFactory"/>, response parsing
/// in <see cref="OnvifMediaServiceResponseParser"/> -- this class only sequences the three
/// HTTP calls and maps outcomes to a specific <see cref="OnvifStreamUriFailureReason"/>.
///
/// Media (ver10) only. Media2 (ver20) is a documented future addition: it would be a second
/// request-factory/parser pair plus a version-selection point here, not a rewrite of this
/// orchestration shape or of <c>LiveViewViewModel</c>.
///
/// Never persisted -- callers must treat the returned URI as runtime-only, re-resolved on
/// every Live View connect (ONVIF stream URIs are commonly session/time-scoped).
/// </summary>
public sealed class OnvifStreamUriResolver : IOnvifStreamUriResolver
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);
    private static readonly OnvifMediaServiceRequestFactory RequestFactory = new();
    private static readonly OnvifMediaServiceResponseParser ResponseParser = new();

    public OnvifStreamUriResolution Resolve(Camera camera, CameraCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(camera);

        // Task-AI00B Phase 7C: diagnostics-only progress flags, read only by LogAndReturn below
        // to emit one consolidated sanitized summary per call. They never influence which
        // OnvifStreamUriResolution is returned -- every return statement below is byte-for-byte
        // the same value it would be without this instrumentation.
        var capabilitiesSuccess = false;
        var profilesSuccess = false;
        var streamUriSuccess = false;

        if (string.IsNullOrWhiteSpace(camera.IpAddress))
        {
            return LogAndReturn(
                camera,
                OnvifStreamUriResolution.Failure(
                    OnvifStreamUriFailureReason.DeviceServiceUnavailable,
                    "Camera has no IP address configured."),
                capabilitiesSuccess, profilesSuccess, streamUriSuccess, resolvedUri: null);
        }

        var capabilitiesResult = SendRequest(
            BuildDeviceServiceEndpoint(camera),
            RequestFactory.BuildGetCapabilitiesRequest(credentials.Username, credentials.Password),
            camera,
            "GetCapabilities");

        if (capabilitiesResult is null)
        {
            return LogAndReturn(
                camera,
                OnvifStreamUriResolution.Failure(
                    OnvifStreamUriFailureReason.DeviceServiceUnavailable,
                    "Could not reach the camera's ONVIF Device Service."),
                capabilitiesSuccess, profilesSuccess, streamUriSuccess, resolvedUri: null);
        }

        if (capabilitiesResult.StatusCode == 401)
        {
            return LogAndReturn(
                camera,
                OnvifStreamUriResolution.Failure(
                    OnvifStreamUriFailureReason.AuthenticationFailed,
                    "The camera rejected the configured ONVIF credentials."),
                capabilitiesSuccess, profilesSuccess, streamUriSuccess, resolvedUri: null);
        }

        if (!IsSuccessStatus(capabilitiesResult.StatusCode) || ResponseParser.HasFault(capabilitiesResult.Body))
        {
            return LogAndReturn(
                camera,
                OnvifStreamUriResolution.Failure(
                    OnvifStreamUriFailureReason.DeviceServiceUnavailable,
                    "The camera's ONVIF Device Service returned an error for GetCapabilities."),
                capabilitiesSuccess, profilesSuccess, streamUriSuccess, resolvedUri: null);
        }

        capabilitiesSuccess = true;

        var mediaXAddr = ResponseParser.ParseMediaXAddr(capabilitiesResult.Body);
        if (string.IsNullOrWhiteSpace(mediaXAddr) || !Uri.TryCreate(mediaXAddr, UriKind.Absolute, out var mediaServiceUri))
        {
            return LogAndReturn(
                camera,
                OnvifStreamUriResolution.Failure(
                    OnvifStreamUriFailureReason.MediaServiceUnavailable,
                    "The camera did not advertise an ONVIF Media service."),
                capabilitiesSuccess, profilesSuccess, streamUriSuccess, resolvedUri: null);
        }

        var profilesResult = SendRequest(
            mediaServiceUri,
            RequestFactory.BuildGetProfilesRequest(credentials.Username, credentials.Password),
            camera,
            "GetProfiles");

        if (profilesResult is null)
        {
            return LogAndReturn(
                camera,
                OnvifStreamUriResolution.Failure(
                    OnvifStreamUriFailureReason.MediaServiceUnavailable,
                    "Could not reach the camera's ONVIF Media Service."),
                capabilitiesSuccess, profilesSuccess, streamUriSuccess, resolvedUri: null);
        }

        if (profilesResult.StatusCode == 401)
        {
            return LogAndReturn(
                camera,
                OnvifStreamUriResolution.Failure(
                    OnvifStreamUriFailureReason.AuthenticationFailed,
                    "The camera rejected the configured ONVIF credentials."),
                capabilitiesSuccess, profilesSuccess, streamUriSuccess, resolvedUri: null);
        }

        if (!IsSuccessStatus(profilesResult.StatusCode) || ResponseParser.HasFault(profilesResult.Body))
        {
            return LogAndReturn(
                camera,
                OnvifStreamUriResolution.Failure(
                    OnvifStreamUriFailureReason.MediaServiceUnavailable,
                    "The camera's ONVIF Media Service returned an error for GetProfiles."),
                capabilitiesSuccess, profilesSuccess, streamUriSuccess, resolvedUri: null);
        }

        profilesSuccess = true;

        var profileTokens = ResponseParser.ParseProfileTokens(profilesResult.Body);
        var selectedProfileToken = OnvifMediaProfileSelector.SelectProfile(profileTokens);
        if (selectedProfileToken is null)
        {
            return LogAndReturn(
                camera,
                OnvifStreamUriResolution.Failure(
                    OnvifStreamUriFailureReason.NoMediaProfiles,
                    "The camera did not return any usable ONVIF media profiles."),
                capabilitiesSuccess, profilesSuccess, streamUriSuccess, resolvedUri: null);
        }

        var streamUriResult = SendRequest(
            mediaServiceUri,
            RequestFactory.BuildGetStreamUriRequest(selectedProfileToken, credentials.Username, credentials.Password),
            camera,
            "GetStreamUri");

        if (streamUriResult is null)
        {
            return LogAndReturn(
                camera,
                OnvifStreamUriResolution.Failure(
                    OnvifStreamUriFailureReason.GetStreamUriFailed,
                    "Could not retrieve the stream URI from the camera's ONVIF Media Service."),
                capabilitiesSuccess, profilesSuccess, streamUriSuccess, resolvedUri: null);
        }

        if (streamUriResult.StatusCode == 401)
        {
            return LogAndReturn(
                camera,
                OnvifStreamUriResolution.Failure(
                    OnvifStreamUriFailureReason.AuthenticationFailed,
                    "The camera rejected the configured ONVIF credentials."),
                capabilitiesSuccess, profilesSuccess, streamUriSuccess, resolvedUri: null);
        }

        if (!IsSuccessStatus(streamUriResult.StatusCode) || ResponseParser.HasFault(streamUriResult.Body))
        {
            return LogAndReturn(
                camera,
                OnvifStreamUriResolution.Failure(
                    OnvifStreamUriFailureReason.GetStreamUriFailed,
                    "The camera's ONVIF Media Service returned an error for GetStreamUri."),
                capabilitiesSuccess, profilesSuccess, streamUriSuccess, resolvedUri: null);
        }

        streamUriSuccess = true;

        var streamUri = ResponseParser.ParseStreamUri(streamUriResult.Body);
        if (string.IsNullOrWhiteSpace(streamUri) || !Uri.TryCreate(streamUri, UriKind.Absolute, out var parsedStreamUri))
        {
            return LogAndReturn(
                camera,
                OnvifStreamUriResolution.Failure(
                    OnvifStreamUriFailureReason.InvalidStreamUri,
                    "The camera returned an invalid stream URI."),
                capabilitiesSuccess, profilesSuccess, streamUriSuccess, resolvedUri: null);
        }

        return LogAndReturn(
            camera,
            OnvifStreamUriResolution.Success(streamUri),
            capabilitiesSuccess, profilesSuccess, streamUriSuccess, parsedStreamUri);
    }

    private static HttpDriverResponse? SendRequest(Uri endpoint, string body, Camera camera, string operationName)
    {
        try
        {
            return HttpDriverTransport.Send(new HttpDriverRequest
            {
                Endpoint = endpoint,
                ContentType = "application/soap+xml",
                Body = body,
                Timeout = RequestTimeout
            });
        }
        catch (Exception ex)
        {
            // Message intentionally omits Username/Password/endpoint query -- only IP/port/operation.
            AppLog.Warning($"ONVIF {operationName} failed for camera at {camera.IpAddress}:{camera.HttpPort}.", ex);
            return null;
        }
    }

    private static Uri BuildDeviceServiceEndpoint(Camera camera)
    {
        var port = camera.HttpPort > 0 ? camera.HttpPort : 80;
        return new Uri($"http://{camera.IpAddress}:{port}/onvif/device_service");
    }

    private static bool IsSuccessStatus(int statusCode)
    {
        return statusCode is >= 200 and < 300;
    }

    /// <summary>
    /// Task-AI00B Phase 7C: emits one consolidated, sanitized diagnostic line per
    /// <see cref="Resolve"/> call, then returns <paramref name="result"/> completely unchanged --
    /// this method never alters control flow or the resolution outcome. Only boolean/enum/count
    /// metadata derived from <paramref name="resolvedUri"/> is logged: never the host, path, or
    /// query values themselves, and never any SOAP body/WS-Security/credential content.
    /// </summary>
    private static OnvifStreamUriResolution LogAndReturn(
        Camera camera,
        OnvifStreamUriResolution result,
        bool capabilitiesSuccess,
        bool profilesSuccess,
        bool streamUriSuccess,
        Uri? resolvedUri)
    {
        var hasResolvedUri = resolvedUri is not null;

        AppLog.Info(
            "ONVIF stream resolution: " +
            $"onvifCapabilitiesSuccess={capabilitiesSuccess}, " +
            $"onvifProfilesSuccess={profilesSuccess}, " +
            $"onvifStreamUriSuccess={streamUriSuccess}, " +
            $"failureReason={(result.IsSuccess ? "none" : result.FailureReason?.ToString() ?? "unknown")}, " +
            $"resolvedUriScheme={(hasResolvedUri ? resolvedUri!.Scheme : "n/a")}, " +
            $"resolvedUriPort={(hasResolvedUri ? resolvedUri!.Port.ToString() : "n/a")}, " +
            $"resolvedUriHostMatchesCameraIp={(hasResolvedUri ? (resolvedUri!.Host == camera.IpAddress).ToString() : "n/a")}, " +
            $"resolvedUriHasUserInfo={(hasResolvedUri ? (!string.IsNullOrEmpty(resolvedUri!.UserInfo)).ToString() : "n/a")}, " +
            $"resolvedUriPathPresent={(hasResolvedUri ? HasNonRootPath(resolvedUri!).ToString() : "n/a")}, " +
            $"resolvedUriQueryPresent={(hasResolvedUri ? (!string.IsNullOrEmpty(resolvedUri!.Query)).ToString() : "n/a")}, " +
            $"resolvedUriQueryParameterCount={(hasResolvedUri ? CountQueryParameters(resolvedUri!.Query).ToString() : "n/a")}.");

        return result;
    }

    private static bool HasNonRootPath(Uri uri)
    {
        return !string.IsNullOrEmpty(uri.AbsolutePath) && uri.AbsolutePath != "/";
    }

    private static int CountQueryParameters(string query)
    {
        if (string.IsNullOrEmpty(query))
        {
            return 0;
        }

        var trimmed = query.StartsWith('?') ? query[1..] : query;
        return string.IsNullOrEmpty(trimmed) ? 0 : trimmed.Split('&').Length;
    }
}
