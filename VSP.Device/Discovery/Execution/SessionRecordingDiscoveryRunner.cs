using VSP.Device.Discovery.Orchestration;
using VSP.Device.Discovery.Sessions;

namespace VSP.Device.Discovery.Execution;

public sealed class SessionRecordingDiscoveryRunner : IDiscoveryRunner
{
    private readonly IDiscoveryRunner _inner;
    private readonly DiscoverySessionFactory _sessionFactory;
    private readonly IDiscoverySessionSink _sessionSink;

    public SessionRecordingDiscoveryRunner(IDiscoveryRunner inner)
        : this(inner, new DiscoverySessionFactory(), NoOpDiscoverySessionSink.Instance)
    {
    }

    public SessionRecordingDiscoveryRunner(
        IDiscoveryRunner inner,
        DiscoverySessionFactory sessionFactory,
        IDiscoverySessionSink sessionSink)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
        _sessionSink = sessionSink ?? throw new ArgumentNullException(nameof(sessionSink));
    }

    public async Task<DiscoveryOrchestrationResult> ExecuteAsync(
        DiscoveryOrchestrationRequest? request,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTimeOffset.UtcNow;

        var result = await _inner.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

        var endTime = DateTimeOffset.UtcNow;

        var session = _sessionFactory.Create(new DiscoverySessionFactoryRequest
        {
            DiscoveryOrchestrationResult = result,
            CorrelationId = request?.CorrelationId,
            StartTime = startTime,
            EndTime = endTime
        });

        _sessionSink.Publish(session);

        return result;
    }
}
