using VSP.Device.Discovery.Rtsp;
using Xunit;

namespace VSP.Tests.Discovery;

public class RtspResponseClassifierTests
{
    private readonly RtspResponseClassifier _classifier = new();

    [Fact]
    public void Classify_ReturnsSuccessForRtsp200Response()
    {
        var result = _classifier.Classify(
            """
            RTSP/1.0 200 OK
            CSeq: 1
            Public: OPTIONS, DESCRIBE

            """);

        Assert.True(result.ReceivedAnyBytes);
        Assert.True(result.IsRtspResponse);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("OK", result.ReasonPhrase);
        Assert.Equal(RtspProbeResponseClassification.Success, result.Classification);
    }

    [Fact]
    public void Classify_ReturnsAuthenticationRequiredAndHeaderValueForRtsp401Response()
    {
        var result = _classifier.Classify(
            """
            RTSP/1.0 401 Unauthorized
            CSeq: 1
            WWW-Authenticate: Digest realm="Camera", nonce="abc"

            """);

        Assert.True(result.IsRtspResponse);
        Assert.Equal(401, result.StatusCode);
        Assert.Equal("Unauthorized", result.ReasonPhrase);
        Assert.Equal("Digest realm=\"Camera\", nonce=\"abc\"", result.WwwAuthenticate);
        Assert.Equal(RtspProbeResponseClassification.AuthenticationRequired, result.Classification);
    }

    [Fact]
    public void Classify_ReturnsProtocolNotSupportedForHttpResponse()
    {
        var result = _classifier.Classify(
            """
            HTTP/1.1 200 OK
            Content-Type: text/plain

            """);

        Assert.True(result.ReceivedAnyBytes);
        Assert.False(result.IsRtspResponse);
        Assert.Null(result.StatusCode);
        Assert.Equal("HTTP/1.1 200 OK", result.RawStatusLine);
        Assert.Equal(RtspProbeResponseClassification.ProtocolNotSupported, result.Classification);
    }

    [Fact]
    public void Classify_ReturnsInvalidResponseForMalformedPayload()
    {
        var result = _classifier.Classify("HELLO CAMERA");

        Assert.True(result.ReceivedAnyBytes);
        Assert.False(result.IsRtspResponse);
        Assert.Equal(RtspProbeResponseClassification.InvalidResponse, result.Classification);
    }
}
