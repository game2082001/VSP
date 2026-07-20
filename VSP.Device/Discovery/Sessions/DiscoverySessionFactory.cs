using VSP.Device.Discovery.Orchestration;

namespace VSP.Device.Discovery.Sessions;

public sealed class DiscoverySessionFactory
{
    public DiscoverySession Create(DiscoverySessionFactoryRequest? request)
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = request?.SessionId ?? DiscoverySessionId.New();
        var startTime = request?.StartTime ?? now;
        var endTime = request?.EndTime ?? now;

        if (request?.DiscoveryOrchestrationResult is null)
        {
            return CreateFailedSession(
                sessionId,
                request?.CorrelationId,
                startTime,
                endTime,
                new SessionReason(
                    "MissingOrchestrationResult",
                    "Discovery orchestration result is required to create a discovery session."));
        }

        if (endTime < startTime)
        {
            return new DiscoverySession(
                sessionId,
                DiscoverySessionStatus.Failed,
                startTime,
                startTime,
                new DiscoverySessionSnapshot(
                    request.DiscoveryOrchestrationResult.Summary,
                    [
                        new SessionReason(
                            "InvalidTimeRange",
                            "Discovery session end time must not be earlier than start time.")
                    ]),
                request.CorrelationId);
        }

        var result = request.DiscoveryOrchestrationResult;
        var status = MapStatus(result.Status);
        var reasons = result.Reasons
            .Select(reason => new SessionReason(reason.Code, reason.Message))
            .ToList();

        if (result.Status == DiscoveryOrchestrationStatus.InvalidRequest)
        {
            reasons.Add(new SessionReason(
                "InvalidRequestMappedToFailed",
                "Invalid orchestration request was mapped to failed discovery session status."));
        }

        return new DiscoverySession(
            sessionId,
            status,
            startTime,
            endTime,
            new DiscoverySessionSnapshot(result.Summary, reasons),
            request.CorrelationId);
    }

    private static DiscoverySessionStatus MapStatus(DiscoveryOrchestrationStatus status)
    {
        return status switch
        {
            DiscoveryOrchestrationStatus.Completed => DiscoverySessionStatus.Completed,
            DiscoveryOrchestrationStatus.CompletedWithErrors => DiscoverySessionStatus.CompletedWithErrors,
            DiscoveryOrchestrationStatus.Cancelled => DiscoverySessionStatus.Cancelled,
            DiscoveryOrchestrationStatus.Failed => DiscoverySessionStatus.Failed,
            DiscoveryOrchestrationStatus.InvalidRequest => DiscoverySessionStatus.Failed,
            _ => DiscoverySessionStatus.Failed
        };
    }

    private static DiscoverySession CreateFailedSession(
        DiscoverySessionId sessionId,
        string? correlationId,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        SessionReason reason)
    {
        var normalizedEndTime = endTime < startTime ? startTime : endTime;

        return new DiscoverySession(
            sessionId,
            DiscoverySessionStatus.Failed,
            startTime,
            normalizedEndTime,
            new DiscoverySessionSnapshot(new DiscoveryOrchestrationSummary(), [reason]),
            correlationId);
    }
}
