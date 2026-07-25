namespace VSP.Device.Discovery.Diagnostics;

public interface IDiscoveryDiagnosticsSink
{
    void Publish(DiscoveryDiagnosticsSnapshot snapshot);
}
