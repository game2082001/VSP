using VSP.Core.Logging;
using VSP.Device.Drivers.RTSP;
using VSP.Tests.Logging;
using Xunit;
using CameraEntity = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Drivers.RTSP;

[Collection("AppLog")]
public class RtspCameraDriverTests
{
    [Fact]
    public void TestConnection_WhenConnectionIsRefused_LogsWarningWithException()
    {
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        using var server = new LoopbackRtspTestServer(firstResponse: null);
        var closedPort = server.Port;
        server.Dispose();
        var camera = CreateCamera(closedPort);

        var result = new RtspCameraDriver().TestConnection(camera);

        Assert.False(result);
        var call = Assert.Single(recorder.Calls);
        Assert.Equal(LogLevel.Warning, call.Level);
        Assert.NotNull(call.Exception);
        Assert.DoesNotContain(camera.RtspUrl, call.Message);
    }

    [Fact]
    public void TestConnection_ReturnsTrue_WhenDescribeReturns200()
    {
        using var server = new LoopbackRtspTestServer("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n");
        var camera = CreateCamera(server.Port);

        var result = new RtspCameraDriver().TestConnection(camera);

        Assert.True(result);
    }

    [Fact]
    public void TestConnection_ReturnsTrue_WhenDescribeReturnsNonStandard2xx()
    {
        using var server = new LoopbackRtspTestServer("RTSP/1.0 250 Low on Storage Space\r\nCSeq: 1\r\n\r\n");
        var camera = CreateCamera(server.Port);

        var result = new RtspCameraDriver().TestConnection(camera);

        Assert.True(result);
    }

    [Fact]
    public void TestConnection_ReturnsFalse_WhenDescribeReturnsNonSuccessStatus()
    {
        using var server = new LoopbackRtspTestServer("RTSP/1.0 404 Not Found\r\nCSeq: 1\r\n\r\n");
        var camera = CreateCamera(server.Port);

        var result = new RtspCameraDriver().TestConnection(camera);

        Assert.False(result);
    }

    [Fact]
    public void TestConnection_RetriesWithBasicAuthorization_WhenChallengedAndSucceeds()
    {
        using var server = new LoopbackRtspTestServer(
            firstResponse: "RTSP/1.0 401 Unauthorized\r\nCSeq: 1\r\nWWW-Authenticate: Basic realm=\"test\"\r\n\r\n",
            secondResponse: "RTSP/1.0 200 OK\r\nCSeq: 2\r\n\r\n");
        var camera = CreateCamera(server.Port, username: "admin", password: "12345");

        var result = new RtspCameraDriver().TestConnection(camera);

        Assert.True(result);
    }

    [Fact]
    public void TestConnection_RetriesWithDigestAuthorization_WhenChallengedAndSucceeds()
    {
        using var server = new LoopbackRtspTestServer(
            firstResponse: "RTSP/1.0 401 Unauthorized\r\nCSeq: 1\r\n" +
                           "WWW-Authenticate: Digest realm=\"CamRealm\", qop=\"auth\", nonce=\"abc123nonce\"\r\n\r\n",
            secondResponse: "RTSP/1.0 200 OK\r\nCSeq: 2\r\n\r\n");
        var camera = CreateCamera(server.Port, username: "viewer", password: "secret");

        var result = new RtspCameraDriver().TestConnection(camera);

        Assert.True(result);
    }

    [Fact]
    public void TestConnection_ReturnsFalse_WhenChallengedAndNoCredentialsConfigured()
    {
        using var server = new LoopbackRtspTestServer(
            "RTSP/1.0 401 Unauthorized\r\nCSeq: 1\r\nWWW-Authenticate: Basic realm=\"test\"\r\n\r\n");
        var camera = CreateCamera(server.Port);

        var result = new RtspCameraDriver().TestConnection(camera);

        Assert.False(result);
    }

    [Fact]
    public void TestConnection_ReturnsFalse_WhenRetryAlsoReturns401()
    {
        using var server = new LoopbackRtspTestServer(
            firstResponse: "RTSP/1.0 401 Unauthorized\r\nCSeq: 1\r\nWWW-Authenticate: Basic realm=\"test\"\r\n\r\n",
            secondResponse: "RTSP/1.0 401 Unauthorized\r\nCSeq: 2\r\nWWW-Authenticate: Basic realm=\"test\"\r\n\r\n");
        var camera = CreateCamera(server.Port, username: "admin", password: "wrong");

        var result = new RtspCameraDriver().TestConnection(camera);

        Assert.False(result);
    }

    [Fact]
    public void TestConnection_ReturnsFalse_WhenResponseIsMalformed()
    {
        using var server = new LoopbackRtspTestServer("this is not an rtsp response\r\n\r\n");
        var camera = CreateCamera(server.Port);

        var result = new RtspCameraDriver().TestConnection(camera);

        Assert.False(result);
    }

    [Fact]
    public void TestConnection_ReturnsFalse_WhenServerNeverResponds()
    {
        using var server = new LoopbackRtspTestServer(firstResponse: null);
        var camera = CreateCamera(server.Port);

        var result = new RtspCameraDriver().TestConnection(camera);

        Assert.False(result);
    }

    [Fact]
    public void TestConnection_ReturnsFalse_WhenConnectionIsRefused()
    {
        using var server = new LoopbackRtspTestServer(firstResponse: null);
        var closedPort = server.Port;
        server.Dispose();

        var camera = CreateCamera(closedPort);

        var result = new RtspCameraDriver().TestConnection(camera);

        Assert.False(result);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("http://127.0.0.1/stream")]
    [InlineData("")]
    public void TestConnection_ReturnsFalse_WhenRtspUrlIsInvalid(string rtspUrl)
    {
        var camera = new CameraEntity { RtspUrl = rtspUrl };

        var result = new RtspCameraDriver().TestConnection(camera);

        Assert.False(result);
    }

    [Fact]
    public void TestConnection_UsesConfiguredRtspPort_WhenUrlHasNoPort()
    {
        using var server = new LoopbackRtspTestServer("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n");
        var camera = new CameraEntity
        {
            RtspUrl = "rtsp://127.0.0.1/stream1",
            RtspPort = server.Port
        };

        var result = new RtspCameraDriver().TestConnection(camera);

        Assert.True(result);
    }

    [Fact]
    public void TestConnection_PrefersExplicitUrlPort_OverConfiguredRtspPort()
    {
        using var server = new LoopbackRtspTestServer("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n");
        var camera = new CameraEntity
        {
            RtspUrl = $"rtsp://127.0.0.1:{server.Port}/stream1",
            RtspPort = 1
        };

        var result = new RtspCameraDriver().TestConnection(camera);

        Assert.True(result);
    }

    [Fact]
    public void TestConnection_LogsSanitizedAuthChallengeDiagnostics_WithMultipleHeadersAlgorithmAndStale()
    {
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        const string secretRealm = "SuperSecretRealm-9f8e";
        const string secretNonce = "SuperSecretNonce-1a2b3c4d";
        const string secretOpaque = "SuperSecretOpaque-z9y8x7";
        const string secretUsername = "admin";
        const string secretPassword = "wrongpassword";

        using var server = new LoopbackRtspTestServer(
            firstResponse: "RTSP/1.0 401 Unauthorized\r\nCSeq: 1\r\n" +
                           $"WWW-Authenticate: Digest realm=\"{secretRealm}\", nonce=\"{secretNonce}\", " +
                           $"opaque=\"{secretOpaque}\", algorithm=MD5-sess, qop=\"auth,auth-int\", stale=false\r\n" +
                           "WWW-Authenticate: Basic realm=\"other\"\r\n\r\n",
            secondResponse: "RTSP/1.0 401 Unauthorized\r\nCSeq: 2\r\n\r\n");
        var camera = CreateCamera(server.Port, username: secretUsername, password: secretPassword);

        var result = new RtspCameraDriver().TestConnection(camera);

        Assert.False(result);
        var diagnosticCall = Assert.Single(recorder.Calls, call => call.Message.StartsWith("RTSP TestConnection auth challenge:"));
        Assert.Equal(LogLevel.Info, diagnosticCall.Level);

        var message = diagnosticCall.Message;
        Assert.Contains("authChallengeCount=2", message);
        Assert.Contains("challengeSchemes=Digest,Basic", message);
        Assert.Contains("selectedScheme=Digest", message);
        Assert.Contains("algorithmPresent=True", message);
        Assert.Contains("algorithmValue=MD5-sess", message);
        Assert.Contains("qopHasAuth=True", message);
        Assert.Contains("qopHasAuthInt=True", message);
        Assert.Contains("realmPresent=True", message);
        Assert.Contains("noncePresent=True", message);
        Assert.Contains("opaquePresent=True", message);
        Assert.Contains("stalePresent=True", message);
        Assert.Contains("staleValue=false", message);
        Assert.Contains("secondResponseStatus=401", message);

        // Secret-leak assertions: none of the raw challenge/credential values, the Authorization
        // header, or the full RTSP URI may appear in any logged message from this run.
        foreach (var call in recorder.Calls)
        {
            Assert.DoesNotContain(secretRealm, call.Message);
            Assert.DoesNotContain(secretNonce, call.Message);
            Assert.DoesNotContain(secretOpaque, call.Message);
            Assert.DoesNotContain(secretUsername, call.Message);
            Assert.DoesNotContain(secretPassword, call.Message);
            Assert.DoesNotContain("Authorization", call.Message);
            Assert.DoesNotContain("rtsp://", call.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void TestConnection_LogsAuthChallengeDiagnostics_WhenNoCredentialsConfigured()
    {
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        using var server = new LoopbackRtspTestServer(
            "RTSP/1.0 401 Unauthorized\r\nCSeq: 1\r\nWWW-Authenticate: Digest realm=\"test\", nonce=\"n\"\r\n\r\n");
        var camera = CreateCamera(server.Port);

        var result = new RtspCameraDriver().TestConnection(camera);

        Assert.False(result);
        var diagnosticCall = Assert.Single(recorder.Calls, call => call.Message.StartsWith("RTSP TestConnection auth challenge:"));
        Assert.Contains("authChallengeCount=1", diagnosticCall.Message);
        Assert.Contains("secondResponseStatus=,", diagnosticCall.Message + ",");
    }

    [Fact]
    public void TestConnection_DoesNotLogAuthChallengeDiagnostics_WhenFirstResponseIsNot401()
    {
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);

        using var server = new LoopbackRtspTestServer("RTSP/1.0 404 Not Found\r\nCSeq: 1\r\n\r\n");
        var camera = CreateCamera(server.Port);

        var result = new RtspCameraDriver().TestConnection(camera);

        Assert.False(result);
        Assert.DoesNotContain(recorder.Calls, call => call.Message.StartsWith("RTSP TestConnection auth challenge:"));
    }

    private static CameraEntity CreateCamera(int port, string username = "", string password = "")
    {
        return new CameraEntity
        {
            RtspUrl = $"rtsp://127.0.0.1:{port}/stream1",
            Username = username,
            Password = password
        };
    }
}
