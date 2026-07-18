using VSP.Device.Discovery.AutoDiscovery;
using VSP.Device.Discovery.NetworkScan;
using VSP.Device.Discovery.Onvif;
using VSP.Device.Discovery.Rtsp;
using Xunit;

namespace VSP.Tests.Discovery;

public class AutoDiscoveryCoordinatorTests
{
    [Fact]
    public async Task DiscoverAsync_CallsOnlyEnabledWorkflowSteps()
    {
        var onvifService = new FakeOnvifDiscoveryService();
        var rtspService = new FakeRtspEndpointProbeService();
        var networkScanService = new FakeNetworkScanService();
        var coordinator = new AutoDiscoveryCoordinator(onvifService, rtspService, networkScanService);

        var result = await coordinator.DiscoverAsync(new AutoDiscoveryRequest
        {
            EnableOnvifDiscovery = true,
            EnableNetworkScan = false,
            EnableRtspEndpointProbe = true,
            RtspEndpointProbeRequests =
            [
                new RtspEndpointProbeRequest
                {
                    RtspUri = "rtsp://192.168.1.10/stream1"
                }
            ]
        });

        Assert.Equal(1, onvifService.CallCount);
        Assert.Equal(1, rtspService.CallCount);
        Assert.Equal(0, networkScanService.CallCount);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task DiscoverAsync_MergesCandidatesAndPreservesSources()
    {
        var onvifService = new FakeOnvifDiscoveryService
        {
            Results =
            [
                new OnvifDiscoveryResult
                {
                    IpAddress = "192.168.1.10",
                    Name = "Lobby Camera",
                    Manufacturer = "Contoso"
                }
            ]
        };
        var rtspService = new FakeRtspEndpointProbeService
        {
            Results =
            [
                new RtspEndpointProbeResult
                {
                    Host = "192.168.1.10",
                    Port = 554,
                    NormalizedEndpointUri = "rtsp://192.168.1.10/stream1",
                    ResponseClassification = RtspProbeResponseClassification.Success
                }
            ]
        };
        var networkScanService = new FakeNetworkScanService
        {
            Results =
            [
                new NetworkScanResult
                {
                    Target = NetworkScanTarget.ForHostPort("192.168.1.10", 554),
                    Reachability = NetworkReachabilityClassification.Reachable
                }
            ]
        };
        var coordinator = new AutoDiscoveryCoordinator(onvifService, rtspService, networkScanService);

        var result = await coordinator.DiscoverAsync(new AutoDiscoveryRequest
        {
            EnableOnvifDiscovery = true,
            EnableNetworkScan = true,
            EnableRtspEndpointProbe = true,
            RtspEndpointProbeRequests =
            [
                new RtspEndpointProbeRequest
                {
                    RtspUri = "rtsp://192.168.1.10/stream1"
                }
            ]
        });

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal("host:192.168.1.10", candidate.CandidateKey);
        Assert.Contains(AutoDiscoverySource.ONVIF, candidate.Sources);
        Assert.Contains(AutoDiscoverySource.RTSP, candidate.Sources);
        Assert.Contains(AutoDiscoverySource.NetworkScan, candidate.Sources);
        Assert.Equal("Lobby Camera", candidate.Name);
        Assert.Equal("Contoso", candidate.Manufacturer);
        Assert.Equal("Reachable", candidate.Reachability);
        Assert.Equal("Success", candidate.ProbeClassification);
    }

    [Fact]
    public void Merge_IsOrderIndependentForSourceAttribution()
    {
        var merger = new AutoDiscoveryCandidateMerger();
        var onvifCandidate = new AutoDiscoveryCandidate
        {
            CandidateKey = "host:192.168.1.10",
            Sources = [AutoDiscoverySource.ONVIF],
            Host = "192.168.1.10",
            Name = "Lobby Camera"
        };
        var rtspCandidate = new AutoDiscoveryCandidate
        {
            CandidateKey = "host:192.168.1.10",
            Sources = [AutoDiscoverySource.RTSP],
            Host = "192.168.1.10",
            Port = 554,
            ProbeClassification = "Success"
        };

        var onvifThenRtsp = merger.Merge([onvifCandidate, rtspCandidate]);
        var rtspThenOnvif = merger.Merge([rtspCandidate, onvifCandidate]);

        var first = Assert.Single(onvifThenRtsp);
        var second = Assert.Single(rtspThenOnvif);
        Assert.Equal(first.CandidateKey, second.CandidateKey);
        Assert.Equal(first.Sources, second.Sources);
        Assert.Equal(first.Host, second.Host);
        Assert.Equal(first.Port, second.Port);
        Assert.Equal(first.ProbeClassification, second.ProbeClassification);
    }

    [Fact]
    public async Task DiscoverAsync_PassesCancellationTokenToEnabledStep()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var onvifService = new FakeOnvifDiscoveryService();
        var rtspService = new FakeRtspEndpointProbeService();
        var networkScanService = new FakeNetworkScanService();
        var coordinator = new AutoDiscoveryCoordinator(onvifService, rtspService, networkScanService);

        await coordinator.DiscoverAsync(new AutoDiscoveryRequest
        {
            EnableNetworkScan = true
        }, cancellationTokenSource.Token);

        Assert.Equal(cancellationTokenSource.Token, networkScanService.LastCancellationToken);
    }

    [Fact]
    public async Task DiscoverAsync_ThrowsWhenCancellationIsRequestedBeforeWorkflow()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        var coordinator = new AutoDiscoveryCoordinator(
            new FakeOnvifDiscoveryService(),
            new FakeRtspEndpointProbeService(),
            new FakeNetworkScanService());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            coordinator.DiscoverAsync(new AutoDiscoveryRequest
            {
                EnableNetworkScan = true
            }, cancellationTokenSource.Token));
    }

    private sealed class FakeOnvifDiscoveryService : IOnvifDiscoveryService
    {
        public int CallCount { get; private set; }

        public IReadOnlyList<OnvifDiscoveryResult> Results { get; init; } = Array.Empty<OnvifDiscoveryResult>();

        public Task<IReadOnlyList<OnvifDiscoveryResult>> DiscoverAsync(
            OnvifDiscoveryRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(Results);
        }
    }

    private sealed class FakeRtspEndpointProbeService : IRtspEndpointProbeService
    {
        private int _resultIndex;

        public int CallCount { get; private set; }

        public IReadOnlyList<RtspEndpointProbeResult> Results { get; init; } = Array.Empty<RtspEndpointProbeResult>();

        public Task<RtspEndpointProbeResult> ProbeAsync(
            RtspEndpointProbeRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            cancellationToken.ThrowIfCancellationRequested();

            var result = Results.Count == 0
                ? new RtspEndpointProbeResult()
                : Results[Math.Min(_resultIndex++, Results.Count - 1)];

            return Task.FromResult(result);
        }
    }

    private sealed class FakeNetworkScanService : INetworkScanService
    {
        public int CallCount { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public IReadOnlyList<NetworkScanResult> Results { get; init; } = Array.Empty<NetworkScanResult>();

        public Task<IReadOnlyList<NetworkScanResult>> ScanAsync(
            NetworkScanRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastCancellationToken = cancellationToken;
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(Results);
        }
    }
}
