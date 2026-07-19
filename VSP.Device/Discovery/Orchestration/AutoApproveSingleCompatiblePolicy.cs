using VSP.Device.Cameras.Factory;

namespace VSP.Device.Discovery.Orchestration;

public sealed class AutoApproveSingleCompatiblePolicy : IDriverApprovalPolicy
{
    public DriverApprovalResult Evaluate(DriverApprovalRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var compatibleDrivers = request.CompatibleDrivers.ToArray();

        if (compatibleDrivers.Length == 0)
        {
            return new DriverApprovalResult
            {
                Status = DriverApprovalStatus.NoCompatibleDriver,
                CompatibleDrivers = compatibleDrivers,
                Reasons =
                [
                    new DiscoveryOrchestrationReason(
                        "NoCompatibleDriver",
                        "No compatible driver is available for the candidate.")
                ]
            };
        }

        if (compatibleDrivers.Length > 1)
        {
            return new DriverApprovalResult
            {
                Status = DriverApprovalStatus.AwaitingApproval,
                CompatibleDrivers = compatibleDrivers,
                Reasons =
                [
                    new DiscoveryOrchestrationReason(
                        "AwaitingApproval",
                        "Multiple compatible drivers require approval before camera creation.")
                ]
            };
        }

        var driver = compatibleDrivers[0];
        return new DriverApprovalResult
        {
            Status = DriverApprovalStatus.Approved,
            CompatibleDrivers = compatibleDrivers,
            ApprovedDriver = new ApprovedDriverReference
            {
                DriverId = driver.DriverId,
                ConnectionType = driver.ConnectionType,
                DriverDisplayName = driver.DisplayName,
                IsApproved = true
            },
            Reasons =
            [
                new DiscoveryOrchestrationReason(
                    "DriverApproved",
                    "A single compatible driver was approved by policy.")
            ]
        };
    }
}
