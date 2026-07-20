namespace VSP.Device.Discovery.Sessions;

public sealed class DiscoverySession
{
    public DiscoverySession(
        DiscoverySessionId sessionId,
        DiscoverySessionStatus status,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        DiscoverySessionSnapshot snapshot,
        string? correlationId = null)
    {
        if (endTime < startTime)
        {
            throw new ArgumentException("Session end time must not be earlier than start time.", nameof(endTime));
        }

        SessionId = sessionId;
        CorrelationId = correlationId;
        Status = status;
        StartTime = startTime;
        EndTime = endTime;
        Duration = endTime - startTime;
        Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public DiscoverySessionId SessionId { get; }

    public string? CorrelationId { get; }

    public DiscoverySessionStatus Status { get; }

    public DateTimeOffset StartTime { get; }

    public DateTimeOffset EndTime { get; }

    public TimeSpan Duration { get; }

    public DiscoverySessionSnapshot Snapshot { get; }
}
