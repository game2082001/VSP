namespace VSP.Device.Discovery.NetworkScan;

public sealed class NetworkScanResult
{
    public NetworkScanTarget Target { get; init; } = NetworkScanTarget.ForHost("unknown");

    public NetworkScanTargetKind TargetKind => Target.Kind;

    public DateTimeOffset ScanStartedAt { get; init; }

    public DateTimeOffset ScanEndedAt { get; init; }

    public TimeSpan ResponseTime { get; init; }

    public NetworkReachabilityClassification Reachability { get; init; }

    public bool TimeoutOccurred { get; init; }

    public string? ErrorCategory { get; init; }

    public string? ErrorMessage { get; init; }
}
