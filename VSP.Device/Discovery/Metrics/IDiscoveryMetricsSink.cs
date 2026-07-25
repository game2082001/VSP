namespace VSP.Device.Discovery.Metrics;

public interface IDiscoveryMetricsSink
{
    void Record(DiscoveryMetricsSample sample);
}
