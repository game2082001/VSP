namespace VSP.Device.Discovery.Progress;

public sealed class NoOpDiscoveryProgressPublisher : IDiscoveryProgressPublisher
{
    public static readonly NoOpDiscoveryProgressPublisher Instance = new();

    private NoOpDiscoveryProgressPublisher()
    {
    }

    public void Publish(DiscoveryProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
    }
}
