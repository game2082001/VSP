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

    [Fact]
    public void Parse_ParsesAlgorithmMd5()
    {
        var challenge = RtspWwwAuthenticateParser.Parse(
            "Digest realm=\"CamRealm\", nonce=\"abc123nonce\", algorithm=MD5");

        Assert.NotNull(challenge);
        Assert.Equal("MD5", challenge!.Algorithm);
    }

    [Fact]
    public void Parse_ParsesAlgorithmMd5Sess()
    {
        var challenge = RtspWwwAuthenticateParser.Parse(
            "Digest realm=\"CamRealm\", nonce=\"abc123nonce\", algorithm=MD5-sess");

        Assert.NotNull(challenge);
        Assert.Equal("MD5-sess", challenge!.Algorithm);
    }

    [Fact]
    public void Parse_ParsesStaleTrue()
    {
        var challenge = RtspWwwAuthenticateParser.Parse(
            "Digest realm=\"CamRealm\", nonce=\"newnonce\", stale=true");

        Assert.NotNull(challenge);
        Assert.Equal("true", challenge!.Stale);
    }

    [Fact]
    public void Parse_WithoutAlgorithmOrStale_LeavesBothNull()
    {
        var challenge = RtspWwwAuthenticateParser.Parse("Digest realm=\"CamRealm\", nonce=\"abc123nonce\"");

        Assert.NotNull(challenge);
        Assert.Null(challenge!.Algorithm);
        Assert.Null(challenge.Stale);
    }

    [Fact]
    public void ParseAll_ParsesEachHeaderValueIntoItsOwnChallenge()
    {
        var challenges = RtspWwwAuthenticateParser.ParseAll(
        [
            "Digest realm=\"CamRealm\", nonce=\"abc123nonce\", qop=\"auth\"",
            "Basic realm=\"CamRealm\""
        ]);

        Assert.Equal(2, challenges.Count);
        Assert.Equal("Digest", challenges[0].Scheme);
        Assert.Equal("Basic", challenges[1].Scheme);
    }

    [Fact]
    public void ParseAll_ReturnsEmptyList_WhenNoHeaderValuesGiven()
    {
        var challenges = RtspWwwAuthenticateParser.ParseAll([]);

        Assert.Empty(challenges);
    }

    [Fact]
    public void ParseAll_SkipsValuesThatFailToParse()
    {
        var challenges = RtspWwwAuthenticateParser.ParseAll(["", "   ", "Digest realm=\"CamRealm\", nonce=\"n\""]);

        var only = Assert.Single(challenges);
        Assert.Equal("Digest", only.Scheme);
    }
}
