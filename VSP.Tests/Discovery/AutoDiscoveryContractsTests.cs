using VSP.Device.Discovery.AutoDiscovery;
using VSP.Device.Discovery.NetworkScan;
using VSP.Device.Discovery.Rtsp;
using Xunit;

namespace VSP.Tests.Discovery;

public class AutoDiscoveryContractsTests
{
    [Fact]
    public void AutoDiscoveryCandidate_SupportsMultipleSources()
    {
        var candidate = new AutoDiscoveryCandidate
        {
            CandidateKey = "192.168.1.10",
            Sources =
            [
                AutoDiscoverySource.NetworkScan,
                AutoDiscoverySource.ONVIF,
                AutoDiscoverySource.RTSP
            ]
        };

        Assert.Equal(3, candidate.Sources.Count);
        Assert.Contains(AutoDiscoverySource.NetworkScan, candidate.Sources);
        Assert.Contains(AutoDiscoverySource.ONVIF, candidate.Sources);
        Assert.Contains(AutoDiscoverySource.RTSP, candidate.Sources);
    }

    [Fact]
    public void AutoDiscoveryCandidate_StoresCoordinatorOwnedSummaryFields()
    {
        var candidate = new AutoDiscoveryCandidate
        {
            CandidateKey = "rtsp://192.168.1.10/stream1",
            Sources = [AutoDiscoverySource.RTSP],
            Host = "192.168.1.10",
            Port = 554,
            Endpoint = "rtsp://192.168.1.10/stream1",
            ProbeClassification = "Success"
        };

        Assert.Equal("rtsp://192.168.1.10/stream1", candidate.CandidateKey);
        Assert.Equal("192.168.1.10", candidate.Host);
        Assert.Equal(554, candidate.Port);
        Assert.Equal("Success", candidate.ProbeClassification);
    }

    [Fact]
    public void AutoDiscoveryRequest_AllowsComposableWorkflowInputs()
    {
        var request = new AutoDiscoveryRequest
        {
            EnableNetworkScan = true,
            EnableRtspEndpointProbe = true,
            NetworkScanRequest = new NetworkScanRequest
            {
                Targets = [NetworkScanTarget.ForHostPort("192.168.1.10", 554)]
            },
            RtspEndpointProbeRequests =
            [
                new RtspEndpointProbeRequest
                {
                    RtspUri = "rtsp://192.168.1.10/stream1"
                }
            ]
        };

        Assert.True(request.EnableNetworkScan);
        Assert.True(request.EnableRtspEndpointProbe);
        Assert.False(request.EnableOnvifDiscovery);
        Assert.NotNull(request.NetworkScanRequest);
        Assert.Single(request.RtspEndpointProbeRequests);
    }
}
