using VSP.Core.Logging;
using VSP.Device.Drivers.ONVIF;
using VSP.Tests.Logging;
using Xunit;
using EntityCamera = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Drivers.ONVIF;

internal static class OnvifStreamUriResolverTestExtensions
{
    public static OnvifStreamUriResolution Resolve(this OnvifStreamUriResolver resolver, EntityCamera camera)
    {
        return resolver.Resolve(
            camera,
            new VSP.Domain.Entities.CameraCredentials(camera.Username, camera.Password));
    }
}

[Collection("AppLog")]
public class OnvifStreamUriResolverTests
{
    private const string StreamUriValue = "rtsp://192.168.0.89:1025/Streaming/Channels/101";

    private const string GetProfilesResponse =
        """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body>
            <trt:GetProfilesResponse xmlns:trt="http://www.onvif.org/ver10/media/wsdl">
              <trt:Profiles token="Profile_1" fixed="true"/>
              <trt:Profiles token="Profile_2" fixed="true"/>
            </trt:GetProfilesResponse>
          </s:Body>
        </s:Envelope>
        """;

    private const string GetProfilesResponseEmpty =
        """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body>
            <trt:GetProfilesResponse xmlns:trt="http://www.onvif.org/ver10/media/wsdl"/>
          </s:Body>
        </s:Envelope>
        """;

    private const string GetStreamUriResponse =
        $"""
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body>
            <trt:GetStreamUriResponse xmlns:trt="http://www.onvif.org/ver10/media/wsdl">
              <trt:MediaUri xmlns:tt="http://www.onvif.org/ver10/schema">
                <tt:Uri>{StreamUriValue}</tt:Uri>
              </trt:MediaUri>
            </trt:GetStreamUriResponse>
          </s:Body>
        </s:Envelope>
        """;

    private const string GetStreamUriResponseInvalidUri =
        """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body>
            <trt:GetStreamUriResponse xmlns:trt="http://www.onvif.org/ver10/media/wsdl">
              <trt:MediaUri xmlns:tt="http://www.onvif.org/ver10/schema">
                <tt:Uri>not-a-uri</tt:Uri>
              </trt:MediaUri>
            </trt:GetStreamUriResponse>
          </s:Body>
        </s:Envelope>
        """;

    private const string FaultResponse =
        """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body>
            <s:Fault>
              <s:Reason><s:Text>Sender not Authorized</s:Text></s:Reason>
            </s:Fault>
          </s:Body>
        </s:Envelope>
        """;

    private const string GetCapabilitiesResponseWithoutMedia =
        """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body>
            <tds:GetCapabilitiesResponse xmlns:tds="http://www.onvif.org/ver10/device/wsdl">
              <tds:Capabilities xmlns:tt="http://www.onvif.org/ver10/schema"/>
            </tds:GetCapabilitiesResponse>
          </s:Body>
        </s:Envelope>
        """;

    [Fact]
    public void Resolve_FullHappyPath_ReturnsStreamUri()
    {
        using var mediaServer = new LoopbackHttpTestServer(GetProfilesResponse, secondResponseBody: GetStreamUriResponse);
        using var deviceServer = new LoopbackHttpTestServer(BuildCapabilitiesResponse(mediaServer.Port));
        var camera = CreateCamera(deviceServer.Port);

        var result = new OnvifStreamUriResolver().Resolve(camera);

        Assert.True(result.IsSuccess);
        Assert.Equal(StreamUriValue, result.StreamUri);
    }

    [Fact]
    public void Resolve_WithCredentials_SendsWsSecurityHeaderToMediaService()
    {
        using var mediaServer = new LoopbackHttpTestServer(GetProfilesResponse, secondResponseBody: GetStreamUriResponse);
        using var deviceServer = new LoopbackHttpTestServer(BuildCapabilitiesResponse(mediaServer.Port));
        var camera = CreateCamera(deviceServer.Port);
        camera.Username = "admin";
        camera.Password = "secret";

        new OnvifStreamUriResolver().Resolve(camera);

        Assert.All(mediaServer.ReceivedRequests, request => Assert.Contains("UsernameToken", request));
        Assert.All(mediaServer.ReceivedRequests, request => Assert.Contains("admin", request));
    }

    [Fact]
    public void Resolve_DeviceServiceUnreachable_ReturnsDeviceServiceUnavailable()
    {
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        var camera = CreateCamera(port: 1);

        var result = new OnvifStreamUriResolver().Resolve(camera);

        Assert.False(result.IsSuccess);
        Assert.Equal(OnvifStreamUriFailureReason.DeviceServiceUnavailable, result.FailureReason);
    }

    [Fact]
    public void Resolve_DeviceServiceUnreachable_LogsWarningWithoutLeakingCredentials()
    {
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        var camera = CreateCamera(port: 1);
        camera.Username = "admin";
        camera.Password = "super-secret";

        new OnvifStreamUriResolver().Resolve(camera);

        // Two log lines are expected on this path: SendRequest's own Warning (existing,
        // pre-Phase-7C behavior) plus the new Phase 7C sanitized Info summary emitted by every
        // Resolve() return path. Neither may ever contain the configured credentials.
        var warningCall = Assert.Single(recorder.Calls, call => call.Level == LogLevel.Warning);
        Assert.DoesNotContain(camera.Password, warningCall.Message);
        Assert.DoesNotContain(camera.Username, warningCall.Message);

        Assert.All(recorder.Calls, call => Assert.DoesNotContain(camera.Password, call.Message));
        Assert.All(recorder.Calls, call => Assert.DoesNotContain(camera.Username, call.Message));
    }

    // Task-AI00B Phase 7C: sanitized ONVIF resolution summary coverage.
    [Fact]
    public void Resolve_FullHappyPath_LogsSanitizedSummaryWithResolvedUriMetadata()
    {
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        using var mediaServer = new LoopbackHttpTestServer(GetProfilesResponse, secondResponseBody: GetStreamUriResponse);
        using var deviceServer = new LoopbackHttpTestServer(BuildCapabilitiesResponse(mediaServer.Port));
        var camera = CreateCamera(deviceServer.Port);

        new OnvifStreamUriResolver().Resolve(camera);

        var summaryCall = Assert.Single(recorder.Calls, call => call.Message.StartsWith("ONVIF stream resolution:"));
        Assert.Equal(LogLevel.Info, summaryCall.Level);
        Assert.Contains("onvifCapabilitiesSuccess=True", summaryCall.Message);
        Assert.Contains("onvifProfilesSuccess=True", summaryCall.Message);
        Assert.Contains("onvifStreamUriSuccess=True", summaryCall.Message);
        Assert.Contains("failureReason=none", summaryCall.Message);
        Assert.Contains("resolvedUriScheme=rtsp", summaryCall.Message);
        Assert.Contains("resolvedUriPort=1025", summaryCall.Message);
        // camera.IpAddress is "127.0.0.1" (the loopback test server) but StreamUriValue's host is
        // "192.168.0.89" -- these deliberately differ so this assertion proves the mismatch case.
        Assert.Contains("resolvedUriHostMatchesCameraIp=False", summaryCall.Message);
        Assert.Contains("resolvedUriHasUserInfo=False", summaryCall.Message);
        Assert.Contains("resolvedUriPathPresent=True", summaryCall.Message);
        Assert.Contains("resolvedUriQueryPresent=False", summaryCall.Message);
        Assert.Contains("resolvedUriQueryParameterCount=0", summaryCall.Message);

        Assert.DoesNotContain(StreamUriValue, summaryCall.Message);
        Assert.DoesNotContain("192.168.0.89", summaryCall.Message);
        Assert.DoesNotContain("Streaming/Channels/101", summaryCall.Message);
    }

    [Fact]
    public void Resolve_StreamUriWithQuery_LogsQueryPresenceAndCountButNeverValues()
    {
        // XML text content requires '&' to be escaped as '&amp;'.
        var streamUriResponse =
            """
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
              <s:Body>
                <trt:GetStreamUriResponse xmlns:trt="http://www.onvif.org/ver10/media/wsdl">
                  <trt:MediaUri xmlns:tt="http://www.onvif.org/ver10/schema">
                    <tt:Uri>rtsp://192.168.0.89:1025/Streaming/Channels/101?token=PHASE7C-SECRET-TOKEN&amp;channel=1</tt:Uri>
                  </trt:MediaUri>
                </trt:GetStreamUriResponse>
              </s:Body>
            </s:Envelope>
            """;

        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        using var mediaServer = new LoopbackHttpTestServer(GetProfilesResponse, secondResponseBody: streamUriResponse);
        using var deviceServer = new LoopbackHttpTestServer(BuildCapabilitiesResponse(mediaServer.Port));
        var camera = CreateCamera(deviceServer.Port);

        var result = new OnvifStreamUriResolver().Resolve(camera);

        Assert.True(result.IsSuccess);
        Assert.Equal("rtsp://192.168.0.89:1025/Streaming/Channels/101?token=PHASE7C-SECRET-TOKEN&channel=1", result.StreamUri);

        var summaryCall = Assert.Single(recorder.Calls, call => call.Message.StartsWith("ONVIF stream resolution:"));
        Assert.Contains("resolvedUriQueryPresent=True", summaryCall.Message);
        Assert.Contains("resolvedUriQueryParameterCount=2", summaryCall.Message);
        Assert.DoesNotContain("PHASE7C-SECRET-TOKEN", summaryCall.Message);
        Assert.DoesNotContain("token=", summaryCall.Message);
        Assert.DoesNotContain("channel=1", summaryCall.Message);
    }

    [Fact]
    public void Resolve_NoMediaProfiles_LogsSanitizedSummaryWithFailureReasonAndNoResolvedUri()
    {
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        using var mediaServer = new LoopbackHttpTestServer(GetProfilesResponseEmpty);
        using var deviceServer = new LoopbackHttpTestServer(BuildCapabilitiesResponse(mediaServer.Port));
        var camera = CreateCamera(deviceServer.Port);

        new OnvifStreamUriResolver().Resolve(camera);

        var summaryCall = Assert.Single(recorder.Calls, call => call.Message.StartsWith("ONVIF stream resolution:"));
        Assert.Contains("onvifCapabilitiesSuccess=True", summaryCall.Message);
        Assert.Contains("onvifProfilesSuccess=True", summaryCall.Message);
        Assert.Contains("onvifStreamUriSuccess=False", summaryCall.Message);
        Assert.Contains("failureReason=NoMediaProfiles", summaryCall.Message);
        Assert.Contains("resolvedUriScheme=n/a", summaryCall.Message);
        Assert.Contains("resolvedUriPort=n/a", summaryCall.Message);
        Assert.Contains("resolvedUriHostMatchesCameraIp=n/a", summaryCall.Message);
    }

    [Fact]
    public void Resolve_MissingIpAddress_ReturnsDeviceServiceUnavailable()
    {
        var camera = CreateCamera(80);
        camera.IpAddress = "";

        var result = new OnvifStreamUriResolver().Resolve(camera);

        Assert.False(result.IsSuccess);
        Assert.Equal(OnvifStreamUriFailureReason.DeviceServiceUnavailable, result.FailureReason);
    }

    [Fact]
    public void Resolve_DeviceServiceReturnsFault_ReturnsDeviceServiceUnavailable()
    {
        using var deviceServer = new LoopbackHttpTestServer(FaultResponse);
        var camera = CreateCamera(deviceServer.Port);

        var result = new OnvifStreamUriResolver().Resolve(camera);

        Assert.False(result.IsSuccess);
        Assert.Equal(OnvifStreamUriFailureReason.DeviceServiceUnavailable, result.FailureReason);
    }

    [Fact]
    public void Resolve_DeviceServiceReturns401_ReturnsAuthenticationFailed()
    {
        using var deviceServer = new LoopbackHttpTestServer(FaultResponse, statusCode: 401);
        var camera = CreateCamera(deviceServer.Port);

        var result = new OnvifStreamUriResolver().Resolve(camera);

        Assert.False(result.IsSuccess);
        Assert.Equal(OnvifStreamUriFailureReason.AuthenticationFailed, result.FailureReason);
    }

    [Fact]
    public void Resolve_CapabilitiesWithoutMediaEntry_ReturnsMediaServiceUnavailable()
    {
        using var deviceServer = new LoopbackHttpTestServer(GetCapabilitiesResponseWithoutMedia);
        var camera = CreateCamera(deviceServer.Port);

        var result = new OnvifStreamUriResolver().Resolve(camera);

        Assert.False(result.IsSuccess);
        Assert.Equal(OnvifStreamUriFailureReason.MediaServiceUnavailable, result.FailureReason);
    }

    [Fact]
    public void Resolve_MediaServiceUnreachable_ReturnsMediaServiceUnavailable()
    {
        // Media XAddr points at a closed port -- GetCapabilities succeeds, GetProfiles cannot connect.
        using var deviceServer = new LoopbackHttpTestServer(BuildCapabilitiesResponse(port: 1));
        var camera = CreateCamera(deviceServer.Port);

        var result = new OnvifStreamUriResolver().Resolve(camera);

        Assert.False(result.IsSuccess);
        Assert.Equal(OnvifStreamUriFailureReason.MediaServiceUnavailable, result.FailureReason);
    }

    [Fact]
    public void Resolve_GetProfilesReturnsFault_ReturnsMediaServiceUnavailable()
    {
        using var mediaServer = new LoopbackHttpTestServer(FaultResponse);
        using var deviceServer = new LoopbackHttpTestServer(BuildCapabilitiesResponse(mediaServer.Port));
        var camera = CreateCamera(deviceServer.Port);

        var result = new OnvifStreamUriResolver().Resolve(camera);

        Assert.False(result.IsSuccess);
        Assert.Equal(OnvifStreamUriFailureReason.MediaServiceUnavailable, result.FailureReason);
    }

    [Fact]
    public void Resolve_NoMediaProfiles_ReturnsNoMediaProfiles()
    {
        using var mediaServer = new LoopbackHttpTestServer(GetProfilesResponseEmpty);
        using var deviceServer = new LoopbackHttpTestServer(BuildCapabilitiesResponse(mediaServer.Port));
        var camera = CreateCamera(deviceServer.Port);

        var result = new OnvifStreamUriResolver().Resolve(camera);

        Assert.False(result.IsSuccess);
        Assert.Equal(OnvifStreamUriFailureReason.NoMediaProfiles, result.FailureReason);
    }

    [Fact]
    public void Resolve_GetStreamUriReturnsFault_ReturnsGetStreamUriFailed()
    {
        using var mediaServer = new LoopbackHttpTestServer(GetProfilesResponse, secondResponseBody: FaultResponse);
        using var deviceServer = new LoopbackHttpTestServer(BuildCapabilitiesResponse(mediaServer.Port));
        var camera = CreateCamera(deviceServer.Port);

        var result = new OnvifStreamUriResolver().Resolve(camera);

        Assert.False(result.IsSuccess);
        Assert.Equal(OnvifStreamUriFailureReason.GetStreamUriFailed, result.FailureReason);
    }

    [Fact]
    public void Resolve_GetStreamUriReturnsInvalidUri_ReturnsInvalidStreamUri()
    {
        using var mediaServer = new LoopbackHttpTestServer(GetProfilesResponse, secondResponseBody: GetStreamUriResponseInvalidUri);
        using var deviceServer = new LoopbackHttpTestServer(BuildCapabilitiesResponse(mediaServer.Port));
        var camera = CreateCamera(deviceServer.Port);

        var result = new OnvifStreamUriResolver().Resolve(camera);

        Assert.False(result.IsSuccess);
        Assert.Equal(OnvifStreamUriFailureReason.InvalidStreamUri, result.FailureReason);
    }

    private static string BuildCapabilitiesResponse(int port)
    {
        return $"""
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
          <s:Body>
            <tds:GetCapabilitiesResponse xmlns:tds="http://www.onvif.org/ver10/device/wsdl">
              <tds:Capabilities xmlns:tt="http://www.onvif.org/ver10/schema">
                <tt:Media>
                  <tt:XAddr>http://127.0.0.1:{port}/onvif/Media</tt:XAddr>
                </tt:Media>
              </tds:Capabilities>
            </tds:GetCapabilitiesResponse>
          </s:Body>
        </s:Envelope>
        """;
    }

    private static EntityCamera CreateCamera(int port)
    {
        return new EntityCamera
        {
            IpAddress = "127.0.0.1",
            HttpPort = port
        };
    }
}
