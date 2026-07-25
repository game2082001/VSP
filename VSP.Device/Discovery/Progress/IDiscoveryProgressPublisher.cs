namespace VSP.Device.Discovery.Progress;

public interface IDiscoveryProgressPublisher
{
    void Publish(DiscoveryProgress progress);
}
