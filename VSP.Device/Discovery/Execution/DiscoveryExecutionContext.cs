namespace VSP.Device.Discovery.Execution;

public sealed class DiscoveryExecutionContext
{
    public DiscoveryExecutionContext(
        DiscoveryExecutionId executionId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        ExecutionId = executionId;
        CorrelationId = correlationId;
        CancellationToken = cancellationToken;
    }

    public DiscoveryExecutionId ExecutionId { get; }

    public string? CorrelationId { get; }

    public CancellationToken CancellationToken { get; }
}
