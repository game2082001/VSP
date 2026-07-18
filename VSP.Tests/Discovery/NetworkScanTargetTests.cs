using VSP.Device.Discovery.NetworkScan;
using Xunit;

namespace VSP.Tests.Discovery;

public class NetworkScanTargetTests
{
    [Fact]
    public void ForHost_CreatesHostTarget()
    {
        var target = NetworkScanTarget.ForHost(" 192.168.1.10 ");

        Assert.Equal(NetworkScanTargetKind.Host, target.Kind);
        Assert.Equal("192.168.1.10", target.Value);
        Assert.Equal("192.168.1.10", target.Host);
        Assert.Null(target.Port);
    }

    [Fact]
    public void ForHostPort_CreatesHostPortTarget()
    {
        var target = NetworkScanTarget.ForHostPort("camera.local", 554);

        Assert.Equal(NetworkScanTargetKind.HostPort, target.Kind);
        Assert.Equal("camera.local:554", target.Value);
        Assert.Equal("camera.local", target.Host);
        Assert.Equal(554, target.Port);
    }

    [Fact]
    public void ForEndpoint_TreatsEndpointAsOpaqueTarget()
    {
        var target = NetworkScanTarget.ForEndpoint(" rtsp://192.168.1.10/stream1 ");

        Assert.Equal(NetworkScanTargetKind.Endpoint, target.Kind);
        Assert.Equal("rtsp://192.168.1.10/stream1", target.Value);
        Assert.Null(target.Host);
        Assert.Null(target.Port);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void ForHostPort_RejectsInvalidPort(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NetworkScanTarget.ForHostPort("192.168.1.10", port));
    }
}
