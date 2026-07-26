using System.Collections.ObjectModel;
using System.Net;
using VSP.Device.Discovery.AutoDiscovery;

namespace VSP.Device.Discovery.Onvif;

public sealed class OnvifDiscoveryService : IOnvifDiscoveryService
{
    private static readonly IPEndPoint MulticastEndpoint = new(IPAddress.Parse("239.255.255.250"), 3702);

    private readonly IWsDiscoveryTransport _transport;
    private readonly OnvifWsDiscoveryProbeBuilder _probeBuilder;
    private readonly OnvifWsDiscoveryResponseParser _responseParser;

    public OnvifDiscoveryService(
        IWsDiscoveryTransport transport,
        OnvifWsDiscoveryProbeBuilder? probeBuilder = null,
        OnvifWsDiscoveryResponseParser? responseParser = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _probeBuilder = probeBuilder ?? new OnvifWsDiscoveryProbeBuilder();
        _responseParser = responseParser ?? new OnvifWsDiscoveryResponseParser();
    }

    public async Task<IReadOnlyList<OnvifDiscoveryResult>> DiscoverAsync(
        OnvifDiscoveryRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        var effectiveRequest = request ?? new OnvifDiscoveryRequest();
        var timeout = effectiveRequest.Timeout <= TimeSpan.Zero
            ? OnvifDiscoveryRequest.DefaultTimeout
            : effectiveRequest.Timeout;

        using var timeoutCts = new CancellationTokenSource(timeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        var resultsByKey = new Dictionary<string, OnvifDiscoveryResult>(StringComparer.OrdinalIgnoreCase);
        var probePayload = _probeBuilder.BuildProbeMessage();

        try
        {
            await foreach (var message in _transport.ProbeAsync(probePayload, MulticastEndpoint, linkedCts.Token))
            {
                var parsedResults = _responseParser.Parse(message.Payload, message.RemoteEndPoint);
                foreach (var parsedResult in parsedResults)
                {
                    var key = GetDeduplicationKey(parsedResult);
                    if (key is null)
                    {
                        continue;
                    }

                    if (resultsByKey.TryGetValue(key, out var existing))
                    {
                        resultsByKey[key] = Merge(existing, parsedResult);
                        continue;
                    }

                    resultsByKey.Add(key, parsedResult);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
        {
            return new ReadOnlyCollection<OnvifDiscoveryResult>(resultsByKey.Values.ToList());
        }

        return new ReadOnlyCollection<OnvifDiscoveryResult>(resultsByKey.Values.ToList());
    }

    private static string? GetDeduplicationKey(OnvifDiscoveryResult result)
    {
        if (!string.IsNullOrWhiteSpace(result.EndpointReferenceAddress))
        {
            return result.EndpointReferenceAddress.Trim();
        }

        if (!string.IsNullOrWhiteSpace(result.DiscoveryEndpoint))
        {
            return NormalizeXAddr(result.DiscoveryEndpoint);
        }

        if (!string.IsNullOrWhiteSpace(result.RemoteAddress))
        {
            return result.RemoteAddress.Trim();
        }

        return null;
    }

    private static string NormalizeXAddr(string xaddr)
    {
        if (!Uri.TryCreate(xaddr, UriKind.Absolute, out var uri))
        {
            return xaddr.Trim();
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        var host = uri.Host.ToLowerInvariant();
        var port = uri.IsDefaultPort ? string.Empty : $":{uri.Port}";
        var pathAndQuery = uri.PathAndQuery.TrimEnd('/');

        return $"{scheme}://{host}{port}{pathAndQuery}";
    }

    private static OnvifDiscoveryResult Merge(OnvifDiscoveryResult existing, OnvifDiscoveryResult newer)
    {
        return new OnvifDiscoveryResult
        {
            EndpointReferenceAddress = Choose(existing.EndpointReferenceAddress, newer.EndpointReferenceAddress),
            DiscoveryEndpoint = Choose(existing.DiscoveryEndpoint, newer.DiscoveryEndpoint),
            IpAddress = Choose(existing.IpAddress, newer.IpAddress),
            RemoteAddress = Choose(existing.RemoteAddress, newer.RemoteAddress),
            Name = Choose(existing.Name, newer.Name),
            Location = Choose(existing.Location, newer.Location),
            Model = Choose(existing.Model, newer.Model),
            Manufacturer = Choose(existing.Manufacturer, newer.Manufacturer),
            Types = MergeList(existing.Types, newer.Types),
            Scopes = MergeList(existing.Scopes, newer.Scopes),
            XAddrs = MergeList(existing.XAddrs, newer.XAddrs)
        };
    }

    private static string? Choose(string? existing, string? newer)
    {
        return string.IsNullOrWhiteSpace(existing) ? newer : existing;
    }

    private static IReadOnlyList<string> MergeList(IReadOnlyList<string> existing, IReadOnlyList<string> newer)
    {
        if (existing.Count == 0)
        {
            return newer;
        }

        if (newer.Count == 0)
        {
            return existing;
        }

        var merged = new List<string>(existing);
        foreach (var value in newer)
        {
            if (merged.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            merged.Add(value);
        }

        return merged;
    }
}
