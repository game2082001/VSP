using VSP.Device.Drivers.RTSP;
using Xunit;

namespace VSP.Tests.Drivers.RTSP;

public class RtspAuthorizationHeaderBuilderTests
{
    [Fact]
    public void BuildBasic_EncodesUsernameAndPasswordAsBase64()
    {
        var header = RtspAuthorizationHeaderBuilder.BuildBasic("admin", "12345");

        Assert.Equal("Basic YWRtaW46MTIzNDU=", header);
    }

    [Fact]
    public void BuildDigest_MatchesRfc2617WorkedExample_WithQopAuth()
    {
        // Classic RFC 2617 section 3.5 worked example.
        var challenge = new RtspAuthChallenge(
            "Digest",
            Realm: "testrealm@host.com",
            Nonce: "dcd98b7102dd2f0e8b11d0f600bfb0c093",
            Qop: "auth",
            Opaque: null);

        var header = RtspAuthorizationHeaderBuilder.BuildDigest(
            challenge,
            username: "Mufasa",
            password: "Circle Of Life",
            method: "GET",
            uri: "/dir/index.html",
            cnonce: "0a4f113b");

        Assert.NotNull(header);
        Assert.Contains("response=\"6629fae49393a05397450978507c4ef1\"", header);
        Assert.Contains("qop=auth", header);
        Assert.Contains("nc=00000001", header);
        Assert.Contains("cnonce=\"0a4f113b\"", header);
    }

    [Fact]
    public void BuildDigest_MatchesKnownVector_WithoutQop()
    {
        var challenge = new RtspAuthChallenge(
            "Digest",
            Realm: "CamRealm",
            Nonce: "abc123nonce",
            Qop: null,
            Opaque: null);

        var header = RtspAuthorizationHeaderBuilder.BuildDigest(
            challenge,
            username: "viewer",
            password: "secret",
            method: "DESCRIBE",
            uri: "rtsp://127.0.0.1:5541/stream1");

        Assert.NotNull(header);
        Assert.Contains("response=\"417d290e5adfc1f598460e4e5a705075\"", header);
        Assert.DoesNotContain("qop=", header);
        Assert.DoesNotContain("cnonce=", header);
    }

    [Fact]
    public void BuildDigest_ReturnsNullWhenNonceIsMissing()
    {
        var challenge = new RtspAuthChallenge("Digest", "CamRealm", null, null, null);

        var header = RtspAuthorizationHeaderBuilder.BuildDigest(
            challenge, "viewer", "secret", "DESCRIBE", "rtsp://127.0.0.1:5541/stream1");

        Assert.Null(header);
    }

    [Fact]
    public void Build_DispatchesToBasicOrDigestByScheme()
    {
        var basicChallenge = new RtspAuthChallenge("Basic", "test", null, null, null);
        var digestChallenge = new RtspAuthChallenge("Digest", "test", "somenonce", "auth", null);

        var basicHeader = RtspAuthorizationHeaderBuilder.Build(
            basicChallenge, "admin", "12345", "DESCRIBE", "rtsp://host/stream");
        var digestHeader = RtspAuthorizationHeaderBuilder.Build(
            digestChallenge, "admin", "12345", "DESCRIBE", "rtsp://host/stream");

        Assert.StartsWith("Basic ", basicHeader);
        Assert.StartsWith("Digest ", digestHeader);
    }

    [Fact]
    public void Build_ReturnsNullForUnsupportedScheme()
    {
        var challenge = new RtspAuthChallenge("NTLM", null, null, null, null);

        var header = RtspAuthorizationHeaderBuilder.Build(
            challenge, "admin", "12345", "DESCRIBE", "rtsp://host/stream");

        Assert.Null(header);
    }
}
