using VSP.Domain;
using Xunit;

namespace VSP.Tests.Domain;

public class RtspCredentialUriBuilderTests
{
    [Fact]
    public void Build_WithNoUsername_ReturnsBaseUriUnchanged()
    {
        var baseUri = new Uri("rtsp://192.168.0.89:1025/Streaming/Channels/101");

        var result = RtspCredentialUriBuilder.Build(baseUri, username: null, password: null);

        Assert.Same(baseUri, result);
    }

    [Fact]
    public void Build_WithBlankUsername_PreservesExistingUserInfoUnchanged()
    {
        // Backward compatibility: a manually-typed rtsp://user:pass@host URL predating this
        // feature must keep working exactly as before when Camera.Username is not configured.
        var baseUri = new Uri("rtsp://olduser:oldpass@192.168.0.89:1025/stream");

        var result = RtspCredentialUriBuilder.Build(baseUri, username: "", password: "");

        Assert.Equal("rtsp://olduser:oldpass@192.168.0.89:1025/stream", result.AbsoluteUri);
    }

    [Fact]
    public void Build_WithCredentials_ProducesExactExpectedUrl()
    {
        var baseUri = new Uri("rtsp://192.168.0.89:1025/Streaming/Channels/101");

        var result = RtspCredentialUriBuilder.Build(baseUri, "admin", "secret123");

        Assert.Equal("rtsp://admin:secret123@192.168.0.89:1025/Streaming/Channels/101", result.AbsoluteUri);
    }

    [Fact]
    public void Build_WithUsernamePresentAndBlankPassword_StillEmitsColonSeparator()
    {
        var baseUri = new Uri("rtsp://192.168.0.89:1025/stream");

        var result = RtspCredentialUriBuilder.Build(baseUri, "admin", password: "");

        Assert.Equal("rtsp://admin:@192.168.0.89:1025/stream", result.AbsoluteUri);
    }

    [Fact]
    public void Build_WithUsernamePresentAndNullPassword_StillEmitsColonSeparator()
    {
        var baseUri = new Uri("rtsp://192.168.0.89:1025/stream");

        var result = RtspCredentialUriBuilder.Build(baseUri, "admin", password: null);

        Assert.Equal("rtsp://admin:@192.168.0.89:1025/stream", result.AbsoluteUri);
    }

    [Fact]
    public void Build_WithExistingUserInfoAndConfiguredCredentials_ReplacesEntirely_NoDuplication()
    {
        // Must never produce rtsp://newuser:newpass@olduser:oldpass@host -- the configured
        // Camera credentials fully replace whatever userinfo the URL already carried.
        var baseUri = new Uri("rtsp://olduser:oldpass@192.168.0.89:1025/stream");

        var result = RtspCredentialUriBuilder.Build(baseUri, "newuser", "newpass");

        Assert.Equal("rtsp://newuser:newpass@192.168.0.89:1025/stream", result.AbsoluteUri);
        Assert.DoesNotContain("olduser", result.AbsoluteUri);
        Assert.DoesNotContain("oldpass", result.AbsoluteUri);
        Assert.Equal(1, CountOccurrences(result.AbsoluteUri, '@'));
    }

    [Theory]
    [InlineData("@")]
    [InlineData(":")]
    [InlineData("/")]
    [InlineData("%")]
    [InlineData(" ")]
    [InlineData("#")]
    [InlineData("?")]
    public void Build_PasswordContainingReservedCharacter_RoundTripsExactlyThroughFinalUrlString(string reservedCharacter)
    {
        var rawPassword = $"pass{reservedCharacter}word";
        var baseUri = new Uri("rtsp://192.168.0.89:1025/stream");

        var result = RtspCredentialUriBuilder.Build(baseUri, "admin", rawPassword);
        var finalUrl = result.AbsoluteUri;

        // Assert the exact value crossing the player boundary: re-parse that literal string
        // (not the Uri object we already built it from) and confirm the password segment
        // decodes back to precisely the original raw value -- proving the reserved character
        // was escaped, not left to corrupt/truncate the URI (e.g. an unescaped '#' or '?'
        // would silently truncate everything after it into a fragment/query instead).
        var reparsed = new Uri(finalUrl);
        var userInfoParts = reparsed.UserInfo.Split(':', 2);
        var recoveredPassword = Uri.UnescapeDataString(userInfoParts[1]);

        Assert.Equal(rawPassword, recoveredPassword);
        Assert.StartsWith("rtsp://admin:", finalUrl);
        Assert.Contains("@192.168.0.89:1025/stream", finalUrl);
    }

    [Theory]
    [InlineData("@")]
    [InlineData(":")]
    [InlineData("/")]
    [InlineData("%")]
    [InlineData(" ")]
    [InlineData("#")]
    [InlineData("?")]
    public void Build_UsernameContainingReservedCharacter_RoundTripsExactlyThroughFinalUrlString(string reservedCharacter)
    {
        var rawUsername = $"user{reservedCharacter}name";
        var baseUri = new Uri("rtsp://192.168.0.89:1025/stream");

        var result = RtspCredentialUriBuilder.Build(baseUri, rawUsername, "secret");
        var finalUrl = result.AbsoluteUri;

        var reparsed = new Uri(finalUrl);
        var userInfoParts = reparsed.UserInfo.Split(':', 2);
        var recoveredUsername = Uri.UnescapeDataString(userInfoParts[0]);

        Assert.Equal(rawUsername, recoveredUsername);
        Assert.Contains("@192.168.0.89:1025/stream", finalUrl);
    }

    [Fact]
    public void Build_PreservesQueryAndFragmentFromBaseUri()
    {
        var baseUri = new Uri("rtsp://192.168.0.89:1025/stream?token=abc#part1");

        var result = RtspCredentialUriBuilder.Build(baseUri, "admin", "secret");

        Assert.Equal("rtsp://admin:secret@192.168.0.89:1025/stream?token=abc#part1", result.AbsoluteUri);
    }

    private static int CountOccurrences(string value, char target)
    {
        var count = 0;
        foreach (var c in value)
        {
            if (c == target)
            {
                count++;
            }
        }

        return count;
    }
}
