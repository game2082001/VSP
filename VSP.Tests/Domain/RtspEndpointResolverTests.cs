using VSP.Domain;
using Xunit;

namespace VSP.Tests.Domain;

public class RtspEndpointResolverTests
{
    [Fact]
    public void TryResolve_UrlHasExplicitPort_UsesUrlPort_IgnoringConfiguredPort()
    {
        var resolved = RtspEndpointResolver.TryResolve("rtsp://host:8554/stream", rtspPort: 554, out var effectiveUri);

        Assert.True(resolved);
        Assert.Equal(8554, effectiveUri.Port);
    }

    [Fact]
    public void TryResolve_UrlHasNoPort_FallsBackToConfiguredPort()
    {
        var resolved = RtspEndpointResolver.TryResolve("rtsp://host/stream", rtspPort: 8554, out var effectiveUri);

        Assert.True(resolved);
        Assert.Equal(8554, effectiveUri.Port);
    }

    [Fact]
    public void TryResolve_UrlHasNoPort_AndConfiguredPortIsDefault_ResolvesToDefault()
    {
        var resolved = RtspEndpointResolver.TryResolve("rtsp://host/stream", rtspPort: 554, out var effectiveUri);

        Assert.True(resolved);
        Assert.Equal(554, effectiveUri.Port);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("")]
    [InlineData(null)]
    public void TryResolve_InvalidUrl_ReturnsFalse(string? rtspUrl)
    {
        var resolved = RtspEndpointResolver.TryResolve(rtspUrl, rtspPort: 554, out _);

        Assert.False(resolved);
    }
}
