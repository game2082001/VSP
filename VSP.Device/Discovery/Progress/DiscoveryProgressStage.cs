namespace VSP.Device.Discovery.Progress;

public enum DiscoveryProgressStage
{
    NotStarted,
    Discovering,
    Mapping,
    SelectingDriver,
    AwaitingApproval,
    CreatingCamera,
    Registering,
    Completed,
    Cancelled,
    Failed
}
