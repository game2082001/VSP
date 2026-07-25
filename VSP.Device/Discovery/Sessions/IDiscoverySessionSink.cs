namespace VSP.Device.Discovery.Sessions;

public interface IDiscoverySessionSink
{
    void Publish(DiscoverySession session);
}
