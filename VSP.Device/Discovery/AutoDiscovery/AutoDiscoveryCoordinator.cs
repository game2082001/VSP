using System.Collections.ObjectModel;
using VSP.Device.Discovery.NetworkScan;
using VSP.Device.Discovery.Onvif;
using VSP.Device.Discovery.Rtsp;

namespace VSP.Device.Discovery.AutoDiscovery;

public sealed class AutoDiscoveryCoordinator
{
    private readonly IOnvifDiscoveryService _onvifDiscoveryService;
    private readonly IRtspEndpointProbeService _rtspEndpointProbeService;
    private readonly INetworkScanService _networkScanService;
    private readonly AutoDiscoveryCandidateMerger _candidateMerger;

    public AutoDiscoveryCoordinator(
        IOnvifDiscoveryService onvifDiscoveryService,
        IRtspEndpointProbeService rtspEndpointProbeService,
        INetworkScanService networkScanService)
        : this(
            onvifDiscoveryService,
            rtspEndpointProbeService,
            networkScanService,
            new AutoDiscoveryCandidateMerger())
    {
    }

    public AutoDiscoveryCoordinator(
        IOnvifDiscoveryService onvifDiscoveryService,
        IRtspEndpointProbeService rtspEndpointProbeService,
        INetworkScanService networkScanService,
        AutoDiscoveryCandidateMerger candidateMerger)
    {
        _onvifDiscoveryService = onvifDiscoveryService
            ?? throw new ArgumentNullException(nameof(onvifDiscoveryService));
        _rtspEndpointProbeService = rtspEndpointProbeService
            ?? throw new ArgumentNullException(nameof(rtspEndpointProbeService));
        _networkScanService = networkScanService
            ?? throw new ArgumentNullException(nameof(networkScanService));
        _candidateMerger = candidateMerger
            ?? throw new ArgumentNullException(nameof(candidateMerger));
    }

    public async Task<AutoDiscoveryResult> DiscoverAsync(
        AutoDiscoveryRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        request ??= new AutoDiscoveryRequest();

        var candidates = new List<AutoDiscoveryCandidate>();
        var workflowSteps = CreateWorkflowSteps(request);

        foreach (var workflowStep in workflowSteps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            candidates.AddRange(await workflowStep(cancellationToken).ConfigureAwait(false));
        }

        return new AutoDiscoveryResult
        {
            Candidates = _candidateMerger.Merge(candidates),
            Messages = new ReadOnlyCollection<string>(Array.Empty<string>())
        };
    }

    private IReadOnlyList<Func<CancellationToken, Task<IReadOnlyList<AutoDiscoveryCandidate>>>> CreateWorkflowSteps(
        AutoDiscoveryRequest request)
    {
        var workflowSteps = new List<Func<CancellationToken, Task<IReadOnlyList<AutoDiscoveryCandidate>>>>();

        AddStep(
            request.EnableOnvifDiscovery,
            cancellationToken => RunOnvifDiscoveryAsync(request, cancellationToken));
        AddStep(
            request.EnableNetworkScan,
            cancellationToken => RunNetworkScanAsync(request, cancellationToken));
        AddStep(
            request.EnableRtspEndpointProbe,
            cancellationToken => RunRtspEndpointProbeAsync(request, cancellationToken));

        return workflowSteps;

        void AddStep(
            bool enabled,
            Func<CancellationToken, Task<IReadOnlyList<AutoDiscoveryCandidate>>> workflowStep)
        {
            if (enabled)
            {
                workflowSteps.Add(workflowStep);
            }
        }
    }

    private async Task<IReadOnlyList<AutoDiscoveryCandidate>> RunOnvifDiscoveryAsync(
        AutoDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        var results = await _onvifDiscoveryService
            .DiscoverAsync(request.OnvifDiscoveryRequest, cancellationToken)
            .ConfigureAwait(false);

        return results.Select(MapOnvifResultToCandidate).ToArray();
    }

    private async Task<IReadOnlyList<AutoDiscoveryCandidate>> RunNetworkScanAsync(
        AutoDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        var results = await _networkScanService
            .ScanAsync(request.NetworkScanRequest, cancellationToken)
            .ConfigureAwait(false);

        return results.Select(MapNetworkScanResultToCandidate).ToArray();
    }

    private async Task<IReadOnlyList<AutoDiscoveryCandidate>> RunRtspEndpointProbeAsync(
        AutoDiscoveryRequest request,
        CancellationToken cancellationToken)
    {
        var candidates = new List<AutoDiscoveryCandidate>();

        foreach (var probeRequest in request.RtspEndpointProbeRequests)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result = await _rtspEndpointProbeService
                .ProbeAsync(probeRequest, cancellationToken)
                .ConfigureAwait(false);

            candidates.Add(MapRtspEndpointProbeResultToCandidate(result));
        }

        return candidates;
    }

    private static AutoDiscoveryCandidate MapNetworkScanResultToCandidate(NetworkScanResult result)
    {
        var identity = CreateIdentity(
            result.Target.Host,
            result.Target.Kind == NetworkScanTargetKind.Endpoint ? result.Target.Value : null,
            result.Target.Value);

        return new AutoDiscoveryCandidate
        {
            CandidateKey = identity.CandidateKey,
            Sources = [AutoDiscoverySource.NetworkScan],
            Host = result.Target.Host,
            Port = result.Target.Port,
            Endpoint = result.Target.Kind == NetworkScanTargetKind.Endpoint ? result.Target.Value : null,
            Reachability = result.Reachability.ToString(),
            Warnings = CreateWarnings(result.ErrorMessage)
        };
    }

    private static AutoDiscoveryCandidate MapOnvifResultToCandidate(OnvifDiscoveryResult result)
    {
        var host = FirstNonEmpty(result.IpAddress, result.RemoteAddress);
        var endpoint = FirstNonEmpty(result.DiscoveryEndpoint, result.EndpointReferenceAddress);
        var identity = CreateIdentity(host, endpoint, result.EndpointReferenceAddress);

        return new AutoDiscoveryCandidate
        {
            CandidateKey = identity.CandidateKey,
            Sources = [AutoDiscoverySource.ONVIF],
            Host = host,
            Endpoint = endpoint,
            Name = result.Name,
            Location = result.Location,
            Model = result.Model,
            Manufacturer = result.Manufacturer
        };
    }

    private static AutoDiscoveryCandidate MapRtspEndpointProbeResultToCandidate(RtspEndpointProbeResult result)
    {
        var identity = CreateIdentity(result.Host, result.NormalizedEndpointUri, result.NormalizedEndpointUri);

        return new AutoDiscoveryCandidate
        {
            CandidateKey = identity.CandidateKey,
            Sources = [AutoDiscoverySource.RTSP],
            Host = result.Host,
            Port = result.Port,
            Endpoint = result.NormalizedEndpointUri,
            ProbeClassification = result.ResponseClassification.ToString(),
            Warnings = CreateWarnings(result.ErrorMessage)
        };
    }

    private static AutoDiscoveryIdentity CreateIdentity(string? host, string? endpoint, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(host))
        {
            return AutoDiscoveryIdentity.ForHost(host);
        }

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            return AutoDiscoveryIdentity.ForEndpoint(endpoint);
        }

        return AutoDiscoveryIdentity.ForUnknown(
            string.IsNullOrWhiteSpace(fallback) ? "unknown" : fallback);
    }

    private static IReadOnlyCollection<string> CreateWarnings(string? warning)
    {
        if (string.IsNullOrWhiteSpace(warning))
        {
            return new ReadOnlyCollection<string>(Array.Empty<string>());
        }

        return new ReadOnlyCollection<string>([warning]);
    }

    private static string? FirstNonEmpty(string? first, string? second)
    {
        if (!string.IsNullOrWhiteSpace(first))
        {
            return first;
        }

        return string.IsNullOrWhiteSpace(second) ? null : second;
    }
}
