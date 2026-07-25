using VSP.Device.Drivers.RTSP;
using Xunit;

namespace VSP.Tests.Drivers.RTSP;

public class RtspDescribeResponseParserTests
{
    [Fact]
    public void Parse_Returns200WithNoAuthenticateHeader()
    {
        var result = RtspDescribeResponseParser.Parse("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n");

        Assert.Equal(200, result.StatusCode);
        Assert.Equal("OK", result.ReasonPhrase);
        Assert.Null(result.WwwAuthenticate);
    }

    [Fact]
    public void Parse_Returns2xxNonStandardStatus()
    {
        var result = RtspDescribeResponseParser.Parse("RTSP/1.0 250 Low on Storage Space\r\nCSeq: 1\r\n\r\n");

        Assert.Equal(250, result.StatusCode);
    }

    [Fact]
    public void Parse_ExtractsWwwAuthenticateHeaderOn401()
    {
        var result = RtspDescribeResponseParser.Parse(
            "RTSP/1.0 401 Unauthorized\r\nCSeq: 1\r\nWWW-Authenticate: Basic realm=\"test\"\r\n\r\n");

        Assert.Equal(401, result.StatusCode);
        Assert.Equal("Basic realm=\"test\"", result.WwwAuthenticate);
    }

    [Fact]
    public void Parse_ReturnsNullStatusForNonRtspResponse()
    {
        var result = RtspDescribeResponseParser.Parse("HTTP/1.1 200 OK\r\n\r\n");

        Assert.Null(result.StatusCode);
    }

    [Fact]
    public void Parse_ReturnsNullStatusForEmptyOrMalformedResponse()
    {
        Assert.Null(RtspDescribeResponseParser.Parse(null).StatusCode);
        Assert.Null(RtspDescribeResponseParser.Parse(string.Empty).StatusCode);
        Assert.Null(RtspDescribeResponseParser.Parse("garbage bytes with no status line").StatusCode);
    }
}
