using VSP.Device.Discovery.Execution;
using Xunit;

namespace VSP.Tests.Discovery;

public class DiscoveryExecutionContextTests
{
    [Fact]
    public void Constructor_SetsExecutionIdCorrelationIdAndCancellationToken()
    {
        var executionId = DiscoveryExecutionId.New();
        using var cancellationTokenSource = new CancellationTokenSource();

        var context = new DiscoveryExecutionContext(executionId, "correlation-1", cancellationTokenSource.Token);

        Assert.Equal(executionId, context.ExecutionId);
        Assert.Equal("correlation-1", context.CorrelationId);
        Assert.Equal(cancellationTokenSource.Token, context.CancellationToken);
    }

    [Fact]
    public void Constructor_WithNullCorrelationId_LeavesCorrelationIdNull()
    {
        var context = new DiscoveryExecutionContext(DiscoveryExecutionId.New(), null, CancellationToken.None);

        Assert.Null(context.CorrelationId);
    }

    [Fact]
    public void New_NeverReturnsEmptyExecutionId()
    {
        var executionId = DiscoveryExecutionId.New();

        Assert.NotEqual(Guid.Empty, executionId.Value);
    }

    [Fact]
    public void FromGuid_WithEmptyGuid_Throws()
    {
        Assert.Throws<ArgumentException>(() => DiscoveryExecutionId.FromGuid(Guid.Empty));
    }
}
