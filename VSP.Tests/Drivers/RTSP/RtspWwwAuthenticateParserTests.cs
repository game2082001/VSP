using VSP.Device.Drivers.RTSP;
using Xunit;

namespace VSP.Tests.Drivers.RTSP;

public class RtspWwwAuthenticateParserTests
{
    [Fact]
    public void Parse_ParsesBasicChallenge()
    {
        var challenge = RtspWwwAuthenticateParser.Parse("Basic realm=\"test\"");

        Assert.NotNull(challenge);
        Assert.Equal("Basic", challenge!.Scheme);
        Assert.Equal("test", challenge.Realm);
        Assert.Null(challenge.Nonce);
    }

    [Fact]
    public void Parse_ParsesDigestChallengeWithQopAndOpaque()
    {
        var challenge = RtspWwwAuthenticateParser.Parse(
            "Digest realm=\"testrealm@host.com\", qop=\"auth\", " +
            "nonce=\"dcd98b7102dd2f0e8b11d0f600bfb0c093\", opaque=\"5ccc069c403ebaf9f0171e9517f40e41\"");

        Assert.NotNull(challenge);
        Assert.Equal("Digest", challenge!.Scheme);
        Assert.Equal("testrealm@host.com", challenge.Realm);
        Assert.Equal("auth", challenge.Qop);
        Assert.Equal("dcd98b7102dd2f0e8b11d0f600bfb0c093", challenge.Nonce);
        Assert.Equal("5ccc069c403ebaf9f0171e9517f40e41", challenge.Opaque);
    }

    [Fact]
    public void Parse_ParsesDigestChallengeWithoutQop()
    {
        var challenge = RtspWwwAuthenticateParser.Parse("Digest realm=\"CamRealm\", nonce=\"abc123nonce\"");

        Assert.NotNull(challenge);
        Assert.Equal("Digest", challenge!.Scheme);
        Assert.Null(challenge.Qop);
        Assert.Equal("abc123nonce", challenge.Nonce);
    }

    [Fact]
    public void Parse_ReturnsNullForEmptyOrMissingHeader()
    {
        Assert.Null(RtspWwwAuthenticateParser.Parse(null));
        Assert.Null(RtspWwwAuthenticateParser.Parse(string.Empty));
        Assert.Null(RtspWwwAuthenticateParser.Parse("   "));
    }
}
