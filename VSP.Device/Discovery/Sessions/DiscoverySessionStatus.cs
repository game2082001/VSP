namespace VSP.Device.Discovery.Sessions;

public enum DiscoverySessionStatus
{
    Created,
    Running,
    Completed,
    CompletedWithErrors,
    Cancelled,
    Failed
}
