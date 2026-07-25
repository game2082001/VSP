namespace VSP.Device.Discovery.Sessions;

public sealed class NoOpDiscoverySessionSink : IDiscoverySessionSink
{
    public static readonly NoOpDiscoverySessionSink Instance = new();

    private NoOpDiscoverySessionSink()
    {
    }

    public void Publish(DiscoverySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
    }
}
