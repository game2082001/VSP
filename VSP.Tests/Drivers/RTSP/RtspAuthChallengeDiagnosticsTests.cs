using VSP.Device.Drivers.RTSP;
using Xunit;

namespace VSP.Tests.Drivers.RTSP;

public class RtspAuthChallengeDiagnosticsTests
{
    private const string SecretRealm = "SuperSecretRealm-9f8e";
    private const string SecretNonce = "SuperSecretNonce-1a2b3c4d";
    private const string SecretOpaque = "SuperSecretOpaque-z9y8x7";

    [Fact]
    public void BuildSummary_ReportsAlgorithmMd5()
    {
        var challenge = new RtspAuthChallenge("Digest", "r", "n", null, null, Algorithm: "MD5");

        var summary = RtspAuthChallengeDiagnostics.BuildSummary([challenge], 0, null);

        Assert.Contains("algorithmPresent=True", summary);
        Assert.Contains("algorithmValue=MD5", summary);
    }

    [Fact]
    public void BuildSummary_ReportsAlgorithmMd5Sess()
    {
        var challenge = new RtspAuthChallenge("Digest", "r", "n", null, null, Algorithm: "MD5-sess");

        var summary = RtspAuthChallengeDiagnostics.BuildSummary([challenge], 0, null);

        Assert.Contains("algorithmPresent=True", summary);
        Assert.Contains("algorithmValue=MD5-sess", summary);
    }

    [Fact]
    public void BuildSummary_ReportsAlgorithmAbsent()
    {
        var challenge = new RtspAuthChallenge("Digest", "r", "n", null, null);

        var summary = RtspAuthChallengeDiagnostics.BuildSummary([challenge], 0, null);

        Assert.Contains("algorithmPresent=False", summary);
        Assert.Contains("algorithmValue=,", summary);
    }

    [Fact]
    public void BuildSummary_ReportsStaleTrue()
    {
        var challenge = new RtspAuthChallenge("Digest", "r", "n", null, null, Stale: "true");

        var summary = RtspAuthChallengeDiagnostics.BuildSummary([challenge], 0, null);

        Assert.Contains("stalePresent=True", summary);
        Assert.Contains("staleValue=true", summary);
    }

    [Fact]
    public void BuildSummary_ReportsQopAuthOnly()
    {
        var challenge = new RtspAuthChallenge("Digest", "r", "n", "auth", null);

        var summary = RtspAuthChallengeDiagnostics.BuildSummary([challenge], 0, null);

        Assert.Contains("qopPresent=True", summary);
        Assert.Contains("qopHasAuth=True", summary);
        Assert.Contains("qopHasAuthInt=False", summary);
    }

    [Fact]
    public void BuildSummary_ReportsQopAuthIntOnly()
    {
        var challenge = new RtspAuthChallenge("Digest", "r", "n", "auth-int", null);

        var summary = RtspAuthChallengeDiagnostics.BuildSummary([challenge], 0, null);

        Assert.Contains("qopHasAuth=False", summary);
        Assert.Contains("qopHasAuthInt=True", summary);
    }

    [Fact]
    public void BuildSummary_ReportsQopAuthAndAuthInt()
    {
        var challenge = new RtspAuthChallenge("Digest", "r", "n", "auth,auth-int", null);

        var summary = RtspAuthChallengeDiagnostics.BuildSummary([challenge], 0, null);

        Assert.Contains("qopHasAuth=True", summary);
        Assert.Contains("qopHasAuthInt=True", summary);
    }

    [Fact]
    public void BuildSummary_ReportsMultipleChallengeSchemesInOrder()
    {
        var challenges = new[]
        {
            new RtspAuthChallenge("Digest", "r", "n", "auth", null),
            new RtspAuthChallenge("Basic", "r", null, null, null),
        };

        var summary = RtspAuthChallengeDiagnostics.BuildSummary(challenges, 0, null);

        Assert.Contains("authChallengeCount=2", summary);
        Assert.Contains("challengeSchemes=Digest,Basic", summary);
        Assert.Contains("selectedScheme=Digest", summary);
    }

    [Fact]
    public void BuildSummary_ReportsSecondResponseStatus_WhenProvided()
    {
        var challenge = new RtspAuthChallenge("Digest", "r", "n", "auth", null);

        var summary = RtspAuthChallengeDiagnostics.BuildSummary([challenge], 0, 401);

        Assert.Contains("secondResponseStatus=401", summary);
    }

    [Fact]
    public void BuildSummary_ReportsEmptySecondResponseStatus_WhenNotProvided()
    {
        var challenge = new RtspAuthChallenge("Digest", "r", "n", "auth", null);

        var summary = RtspAuthChallengeDiagnostics.BuildSummary([challenge], 0, null);

        Assert.Contains("secondResponseStatus=,", summary + ",");
    }

    [Fact]
    public void BuildSummary_HandlesEmptyChallengeList()
    {
        var summary = RtspAuthChallengeDiagnostics.BuildSummary([], 0, null);

        Assert.Contains("authChallengeCount=0", summary);
        Assert.Contains("selectedScheme=,", summary + ",");
    }

    [Fact]
    public void BuildSummary_NeverContainsRealmNonceOrOpaqueValues()
    {
        var challenge = new RtspAuthChallenge(
            "Digest", SecretRealm, SecretNonce, "auth", SecretOpaque, Algorithm: "MD5-sess", Stale: "true");

        var summary = RtspAuthChallengeDiagnostics.BuildSummary([challenge], 0, 401);

        Assert.DoesNotContain(SecretRealm, summary);
        Assert.DoesNotContain(SecretNonce, summary);
        Assert.DoesNotContain(SecretOpaque, summary);
    }

    [Fact]
    public void BuildSummary_NeverContainsUsernamePasswordAuthorizationOrUri()
    {
        var challenge = new RtspAuthChallenge("Digest", SecretRealm, SecretNonce, "auth", SecretOpaque);

        var summary = RtspAuthChallengeDiagnostics.BuildSummary([challenge], 0, 401);

        Assert.DoesNotContain("admin", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", summary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Authorization", summary);
        Assert.DoesNotContain("rtsp://", summary);
        Assert.DoesNotContain("192.168", summary);
    }
}
