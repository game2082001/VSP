namespace VSP.Device.Discovery.Onvif;

public sealed class OnvifDiscoveryResult
{
    public string? EndpointReferenceAddress { get; init; }

    public string? DiscoveryEndpoint { get; init; }

    public string? IpAddress { get; init; }

    public string? RemoteAddress { get; init; }

    public string? Name { get; init; }

    public string? Location { get; init; }

    public string? Model { get; init; }

    public string? Manufacturer { get; init; }

    public IReadOnlyList<string> Types { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Scopes { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> XAddrs { get; init; } = Array.Empty<string>();
}
