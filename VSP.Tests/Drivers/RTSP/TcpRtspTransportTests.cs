using VSP.Device.Drivers.RTSP;
using Xunit;

namespace VSP.Tests.Drivers.RTSP;

public class TcpRtspTransportTests
{
    [Fact]
    public void SendAndReceive_ReturnsScriptedResponseFromServer()
    {
        using var server = new LoopbackRtspTestServer("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n");

        var response = TcpRtspTransport.SendAndReceive(
            "OPTIONS rtsp://127.0.0.1/stream RTSP/1.0\r\nCSeq: 1\r\n\r\n",
            "127.0.0.1",
            server.Port,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(3));

        Assert.Contains("RTSP/1.0 200 OK", response);
    }

    [Fact]
    public void SendAndReceive_ThrowsTimeoutException_WhenServerNeverResponds()
    {
        using var server = new LoopbackRtspTestServer(firstResponse: null);

        Assert.Throws<TimeoutException>(() => TcpRtspTransport.SendAndReceive(
            "OPTIONS rtsp://127.0.0.1/stream RTSP/1.0\r\nCSeq: 1\r\n\r\n",
            "127.0.0.1",
            server.Port,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void SendAndReceive_Throws_WhenConnectionIsRefused()
    {
        using var server = new LoopbackRtspTestServer(firstResponse: null);
        var closedPort = server.Port;
        server.Dispose();

        Assert.ThrowsAny<Exception>(() => TcpRtspTransport.SendAndReceive(
            "OPTIONS rtsp://127.0.0.1/stream RTSP/1.0\r\nCSeq: 1\r\n\r\n",
            "127.0.0.1",
            closedPort,
            TimeSpan.FromSeconds(3),
            TimeSpan.FromSeconds(3)));
    }
}
