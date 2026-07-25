using VSP.Device.Drivers.RTSP;
using Xunit;

namespace VSP.Tests.Drivers.RTSP;

public class RtspDescribeRequestFactoryTests
{
    [Fact]
    public void Create_BuildsRequestLineWithExactRtspUrlAndCSeq()
    {
        var request = RtspDescribeRequestFactory.Create("rtsp://192.168.1.10:554/stream1", 1);

        Assert.StartsWith("DESCRIBE rtsp://192.168.1.10:554/stream1 RTSP/1.0\r\n", request);
        Assert.Contains("CSeq: 1\r\n", request);
        Assert.Contains("Accept: application/sdp\r\n", request);
        Assert.EndsWith("\r\n\r\n", request);
    }

    [Fact]
    public void Create_OmitsAuthorizationHeaderWhenNotProvided()
    {
        var request = RtspDescribeRequestFactory.Create("rtsp://192.168.1.10:554/stream1", 1);

        Assert.DoesNotContain("Authorization:", request);
    }

    [Fact]
    public void Create_IncludesAuthorizationHeaderWhenProvided()
    {
        var request = RtspDescribeRequestFactory.Create(
            "rtsp://192.168.1.10:554/stream1",
            2,
            "Basic YWRtaW46MTIzNDU=");

        Assert.Contains("Authorization: Basic YWRtaW46MTIzNDU=\r\n", request);
        Assert.Contains("CSeq: 2\r\n", request);
    }
}
