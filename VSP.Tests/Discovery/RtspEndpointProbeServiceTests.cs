using System.Net.Sockets;
using VSP.Device.Discovery.Rtsp;
using Xunit;

namespace VSP.Tests.Discovery;

public class RtspEndpointProbeServiceTests
{
    [Fact]
    public async Task ProbeAsync_UsesExpectedOptionsRequestForRtspUri()
    {
        string? capturedPayload = null;
        string? capturedHost = null;
        int capturedPort = 0;

        var service = new RtspEndpointProbeService(
            new FakeRtspProbeTransport((payload, host, port, _) =>
            {
                capturedPayload = payload;
                capturedHost = host;
                capturedPort = port;
                return Task.FromResult("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n");
            }));

        var result = await service.ProbeAsync(new RtspEndpointProbeRequest
        {
            RtspUri = "rtsp://192.168.1.10:8554/stream1"
        });

        Assert.Equal(RtspProbeResponseClassification.Success, result.ResponseClassification);
        Assert.Equal("192.168.1.10", capturedHost);
        Assert.Equal(8554, capturedPort);
        Assert.NotNull(capturedPayload);
        Assert.Contains("OPTIONS rtsp://192.168.1.10:8554/stream1 RTSP/1.0", capturedPayload);
        Assert.Contains("CSeq: 1", capturedPayload);
    }

    [Fact]
    public async Task ProbeAsync_UsesExpectedOptionsRequestForHostPortAndPath()
    {
        string? capturedPayload = null;

        var service = new RtspEndpointProbeService(
            new FakeRtspProbeTransport((payload, _, _, _) =>
            {
                capturedPayload = payload;
                return Task.FromResult("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n");
            }));

        var result = await service.ProbeAsync(new RtspEndpointProbeRequest
        {
            Host = "camera.local",
            Port = 8554,
            Path = "live/main"
        });

        Assert.Equal(RtspProbeResponseClassification.Success, result.ResponseClassification);
        Assert.Equal("rtsp://camera.local:8554/live/main", result.NormalizedEndpointUri);
        Assert.NotNull(capturedPayload);
        Assert.Contains("OPTIONS rtsp://camera.local:8554/live/main RTSP/1.0", capturedPayload);
    }

    [Fact]
    public async Task ProbeAsync_UsesWildcardRequestTargetWhenPathIsEmpty()
    {
        string? capturedPayload = null;

        var service = new RtspEndpointProbeService(
            new FakeRtspProbeTransport((payload, _, _, _) =>
            {
                capturedPayload = payload;
                return Task.FromResult("RTSP/1.0 200 OK\r\nCSeq: 1\r\n\r\n");
            }));

        await service.ProbeAsync(new RtspEndpointProbeRequest
        {
            Host = "192.168.1.50",
            Path = string.Empty
        });

        Assert.NotNull(capturedPayload);
        Assert.Contains("OPTIONS * RTSP/1.0", capturedPayload);
    }

    [Fact]
    public async Task ProbeAsync_ReturnsProtocolNotSupportedForHttpResponse()
    {
        var service = new RtspEndpointProbeService(
            new FakeRtspProbeTransport((_, _, _, _) =>
                Task.FromResult("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\n\r\n")));

        var result = await service.ProbeAsync(new RtspEndpointProbeRequest
        {
            Host = "192.168.1.60"
        });

        Assert.True(result.TcpConnectionSucceeded);
        Assert.True(result.ReceivedAnyBytes);
        Assert.False(result.IsRtspResponse);
        Assert.Equal(RtspProbeResponseClassification.ProtocolNotSupported, result.ResponseClassification);
        Assert.True(result.ResponseTime > TimeSpan.Zero);
    }

    [Fact]
    public async Task ProbeAsync_ReturnsTimeoutResultWhenTimeoutOccurs()
    {
        var service = new RtspEndpointProbeService(
            new FakeRtspProbeTransport(async (_, _, _, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return string.Empty;
            }));

        var result = await service.ProbeAsync(
            new RtspEndpointProbeRequest
            {
                Host = "192.168.1.70",
                Timeout = TimeSpan.FromMilliseconds(50)
            });

        Assert.Equal(RtspProbeResponseClassification.Timeout, result.ResponseClassification);
        Assert.True(result.TimeoutOccurred);
        Assert.False(result.TcpConnectionSucceeded);
    }

    [Fact]
    public async Task ProbeAsync_PropagatesUserCancellation()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var service = new RtspEndpointProbeService(
            new FakeRtspProbeTransport(async (_, _, _, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return string.Empty;
            }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ProbeAsync(
                new RtspEndpointProbeRequest
                {
                    Host = "192.168.1.80"
                },
                cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ProbeAsync_ReturnsConnectionFailedWhenTransportThrowsSocketException()
    {
        var service = new RtspEndpointProbeService(
            new FakeRtspProbeTransport((_, _, _, _) =>
                throw new SocketException((int)SocketError.ConnectionRefused)));

        var result = await service.ProbeAsync(new RtspEndpointProbeRequest
        {
            Host = "192.168.1.90"
        });

        Assert.Equal(RtspProbeResponseClassification.ConnectionFailed, result.ResponseClassification);
        Assert.Equal(nameof(SocketException), result.ErrorCategory);
        Assert.False(result.TcpConnectionSucceeded);
    }

    [Fact]
    public async Task ProbeAsync_UsesDefaultPortWhenPortIsOmitted()
    {
        string? capturedHost = null;
        int capturedPort = 0;

        var service = new RtspEndpointProbeService(
            new FakeRtspProbeTransport((_, host, port, _) =>
            {
                capturedHost = host;
                capturedPort = port;
                return Task.FromResult("RTSP/1.0 404 Not Found\r\nCSeq: 1\r\n\r\n");
            }));

        var result = await service.ProbeAsync(new RtspEndpointProbeRequest
        {
            Host = "192.168.1.91",
            Path = "/missing"
        });

        Assert.Equal("192.168.1.91", capturedHost);
        Assert.Equal(554, capturedPort);
        Assert.Equal(RtspProbeResponseClassification.NotFound, result.ResponseClassification);
    }

    private sealed class FakeRtspProbeTransport(
        Func<string, string, int, CancellationToken, Task<string>> sendHandler)
        : IRtspProbeTransport
    {
        public Task<string> SendAndReceiveAsync(
            string requestPayload,
            string host,
            int port,
            CancellationToken cancellationToken)
        {
            return sendHandler(requestPayload, host, port, cancellationToken);
        }
    }
}
