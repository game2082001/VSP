namespace VSP.Device.Discovery.Diagnostics;

public sealed class NoOpDiscoveryDiagnosticsSink : IDiscoveryDiagnosticsSink
{
    public static readonly NoOpDiscoveryDiagnosticsSink Instance = new();

    private NoOpDiscoveryDiagnosticsSink()
    {
    }

    public void Publish(DiscoveryDiagnosticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
    }
}
