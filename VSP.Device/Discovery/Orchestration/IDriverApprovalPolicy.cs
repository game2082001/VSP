namespace VSP.Device.Discovery.Orchestration;

public interface IDriverApprovalPolicy
{
    DriverApprovalResult Evaluate(DriverApprovalRequest request);
}
