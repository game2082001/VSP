namespace VSP.Device.Discovery.NetworkScan;

public sealed class NetworkScanTarget
{
    private NetworkScanTarget(NetworkScanTargetKind kind, string value, string? host, int? port)
    {
        Kind = kind;
        Value = value;
        Host = host;
        Port = port;
    }

    public NetworkScanTargetKind Kind { get; }

    public string Value { get; }

    public string? Host { get; }

    public int? Port { get; }

    public static NetworkScanTarget ForHost(string host)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        var normalizedHost = host.Trim();
        return new NetworkScanTarget(NetworkScanTargetKind.Host, normalizedHost, normalizedHost, null);
    }

    public static NetworkScanTarget ForHostPort(string host, int port)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ValidatePort(port);

        var normalizedHost = host.Trim();
        return new NetworkScanTarget(NetworkScanTargetKind.HostPort, $"{normalizedHost}:{port}", normalizedHost, port);
    }

    public static NetworkScanTarget ForEndpoint(string endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

        var normalizedEndpoint = endpoint.Trim();
        return new NetworkScanTarget(NetworkScanTargetKind.Endpoint, normalizedEndpoint, null, null);
    }

    private static void ValidatePort(int port)
    {
        if (port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be between 1 and 65535.");
        }
    }
}
