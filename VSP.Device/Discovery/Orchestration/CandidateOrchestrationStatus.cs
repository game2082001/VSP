namespace VSP.Device.Discovery.Orchestration;

public enum CandidateOrchestrationStatus
{
    Registered,
    Approved,
    AwaitingApproval,
    NoCompatibleDriver,
    CameraFactoryRejected,
    RegistrationRejected,
    DuplicateSkipped,
    Failed,
    Cancelled,
    Rejected
}
