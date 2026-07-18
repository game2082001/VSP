using VSP.Device.Discovery.NetworkScan;
using Xunit;

namespace VSP.Tests.Discovery;

public class NetworkScanServiceTests
{
    [Fact]
    public async Task ScanAsync_ReturnsEmptyResultsForEmptyTargets()
    {
        var service = new NetworkScanService(new FakeNetworkReachabilityProbe());

        var results = await service.ScanAsync(new NetworkScanRequest
        {
            Targets = Array.Empty<NetworkScanTarget>()
        });

        Assert.Empty(results);
    }

    [Fact]
    public async Task ScanAsync_RejectsInvalidMaxConcurrency()
    {
        var service = new NetworkScanService(new FakeNetworkReachabilityProbe());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ScanAsync(new NetworkScanRequest
            {
                Targets = [NetworkScanTarget.ForHost("192.168.1.10")],
                MaxConcurrency = 0
            }));
    }

    [Fact]
    public async Task ScanAsync_RejectsInvalidTimeout()
    {
        var service = new NetworkScanService(new FakeNetworkReachabilityProbe());

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.ScanAsync(new NetworkScanRequest
            {
                Targets = [NetworkScanTarget.ForHost("192.168.1.10")],
                Timeout = TimeSpan.Zero
            }));
    }

    [Fact]
    public async Task ScanAsync_ReturnsReachabilityResultsInInputOrder()
    {
        var service = new NetworkScanService(
            new FakeNetworkReachabilityProbe(target => target.Value.EndsWith(":554", StringComparison.Ordinal)
                ? NetworkReachabilityClassification.Reachable
                : NetworkReachabilityClassification.Unreachable));

        var results = await service.ScanAsync(new NetworkScanRequest
        {
            Targets =
            [
                NetworkScanTarget.ForHostPort("192.168.1.10", 554),
                NetworkScanTarget.ForHostPort("192.168.1.11", 8554)
            ]
        });

        Assert.Equal(2, results.Count);
        Assert.Equal("192.168.1.10:554", results[0].Target.Value);
        Assert.Equal(NetworkReachabilityClassification.Reachable, results[0].Reachability);
        Assert.Equal("192.168.1.11:8554", results[1].Target.Value);
        Assert.Equal(NetworkReachabilityClassification.Unreachable, results[1].Reachability);
    }

    [Fact]
    public async Task ScanAsync_PropagatesUserCancellation()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var service = new NetworkScanService(new FakeNetworkReachabilityProbe(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return NetworkReachabilityClassification.UnknownFailure;
        }));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ScanAsync(
                new NetworkScanRequest
                {
                    Targets = [NetworkScanTarget.ForHost("192.168.1.10")]
                },
                cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ScanAsync_ReturnsTimeoutResultWhenTargetTimesOut()
    {
        var service = new NetworkScanService(new FakeNetworkReachabilityProbe(async (_, cancellationToken) =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return NetworkReachabilityClassification.UnknownFailure;
        }));

        var results = await service.ScanAsync(new NetworkScanRequest
        {
            Targets = [NetworkScanTarget.ForHost("192.168.1.10")],
            Timeout = TimeSpan.FromMilliseconds(50)
        });

        var result = Assert.Single(results);
        Assert.Equal(NetworkReachabilityClassification.TimedOut, result.Reachability);
        Assert.True(result.TimeoutOccurred);
        Assert.True(result.ResponseTime > TimeSpan.Zero);
    }

    [Fact]
    public async Task ScanAsync_UsesConfiguredMaxConcurrency()
    {
        var running = 0;
        var maxObservedConcurrency = 0;

        var service = new NetworkScanService(new FakeNetworkReachabilityProbe(async (_, cancellationToken) =>
        {
            var current = Interlocked.Increment(ref running);
            UpdateMaxObservedConcurrency(ref maxObservedConcurrency, current);

            await Task.Delay(25, cancellationToken);

            Interlocked.Decrement(ref running);
            return NetworkReachabilityClassification.Reachable;
        }));

        await service.ScanAsync(new NetworkScanRequest
        {
            Targets =
            [
                NetworkScanTarget.ForHost("192.168.1.10"),
                NetworkScanTarget.ForHost("192.168.1.11"),
                NetworkScanTarget.ForHost("192.168.1.12"),
                NetworkScanTarget.ForHost("192.168.1.13")
            ],
            MaxConcurrency = 2
        });

        Assert.InRange(maxObservedConcurrency, 1, 2);
    }

    [Fact]
    public async Task ScanAsync_DoesNotInterpretEndpointProtocol()
    {
        NetworkScanTarget? capturedTarget = null;

        var service = new NetworkScanService(new FakeNetworkReachabilityProbe(target =>
        {
            capturedTarget = target;
            return NetworkReachabilityClassification.Reachable;
        }));

        var results = await service.ScanAsync(new NetworkScanRequest
        {
            Targets = [NetworkScanTarget.ForEndpoint("http://192.168.1.20/onvif/device_service")]
        });

        var result = Assert.Single(results);
        Assert.Equal(NetworkScanTargetKind.Endpoint, result.TargetKind);
        Assert.Equal("http://192.168.1.20/onvif/device_service", result.Target.Value);
        Assert.Same(capturedTarget, result.Target);
        Assert.Null(result.Target.Host);
        Assert.Null(result.Target.Port);
    }

    private static void UpdateMaxObservedConcurrency(ref int maxObservedConcurrency, int current)
    {
        while (true)
        {
            var previous = maxObservedConcurrency;
            if (current <= previous)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref maxObservedConcurrency, current, previous) == previous)
            {
                return;
            }
        }
    }

    private sealed class FakeNetworkReachabilityProbe : INetworkReachabilityProbe
    {
        private readonly Func<NetworkScanTarget, CancellationToken, Task<NetworkReachabilityClassification>> _probeHandler;

        public FakeNetworkReachabilityProbe()
            : this(static (_, _) => Task.FromResult(NetworkReachabilityClassification.Reachable))
        {
        }

        public FakeNetworkReachabilityProbe(Func<NetworkScanTarget, NetworkReachabilityClassification> probeHandler)
            : this((target, _) => Task.FromResult(probeHandler(target)))
        {
        }

        public FakeNetworkReachabilityProbe(
            Func<NetworkScanTarget, CancellationToken, Task<NetworkReachabilityClassification>> probeHandler)
        {
            _probeHandler = probeHandler;
        }

        public Task<NetworkReachabilityClassification> ProbeAsync(
            NetworkScanTarget target,
            CancellationToken cancellationToken)
        {
            return _probeHandler(target, cancellationToken);
        }
    }
}
