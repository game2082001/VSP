namespace VSP.Device.Discovery.Metrics;

public sealed class NoOpDiscoveryMetricsSink : IDiscoveryMetricsSink
{
    public static readonly NoOpDiscoveryMetricsSink Instance = new();

    private NoOpDiscoveryMetricsSink()
    {
    }

    public void Record(DiscoveryMetricsSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
    }
}
