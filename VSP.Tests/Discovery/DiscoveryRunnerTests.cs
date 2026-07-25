using VSP.Device.Cameras.Factory;
using VSP.Device.Discovery.AutoDiscovery;
using VSP.Device.Discovery.Execution;
using VSP.Device.Discovery.Orchestration;
using VSP.Device.Drivers;
using VSP.Device.Drivers.Capabilities;
using VSP.Device.Drivers.Selection;
using VSP.Device.Interfaces;
using VSP.Device.Registration;
using VSP.Domain.Enums;
using Xunit;
using EntityCamera = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Discovery;

public class DiscoveryRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_WithSingleCompatibleDriver_ReturnsCanonicalOrchestrationResult()
    {
        var repository = new FakeCameraRepository();
        var approvalPolicy = new CapturingApprovalPolicy();
        var runner = CreateRunner(repository, approvalPolicy, CreateHostDriver("generic.host"));

        var result = await runner.ExecuteAsync(new DiscoveryOrchestrationRequest
        {
            Candidates = [CreateCandidate()]
        });

        Assert.IsType<DiscoveryOrchestrationResult>(result);
        Assert.Equal(DiscoveryOrchestrationStatus.Completed, result.Status);
        Assert.Equal(1, result.Summary.Registered);
        Assert.Equal(1, repository.AddCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullRequest_ReturnsInvalidRequest()
    {
        var runner = CreateRunner(new FakeCameraRepository(), new CapturingApprovalPolicy());

        var result = await runner.ExecuteAsync(null);

        Assert.Equal(DiscoveryOrchestrationStatus.InvalidRequest, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutCorrelationId_DoesNotGenerateOne()
    {
        var approvalPolicy = new CapturingApprovalPolicy();
        var runner = CreateRunner(new FakeCameraRepository(), approvalPolicy, CreateHostDriver("generic.host"));

        await runner.ExecuteAsync(new DiscoveryOrchestrationRequest
        {
            Candidates = [CreateCandidate()]
        });

        Assert.Null(approvalPolicy.CapturedCorrelationId);
    }

    [Fact]
    public async Task ExecuteAsync_WithCorrelationId_PassesItThroughUnchanged()
    {
        var approvalPolicy = new CapturingApprovalPolicy();
        var runner = CreateRunner(new FakeCameraRepository(), approvalPolicy, CreateHostDriver("generic.host"));

        await runner.ExecuteAsync(new DiscoveryOrchestrationRequest
        {
            Candidates = [CreateCandidate()],
            CorrelationId = "correlation-abc"
        });

        Assert.Equal("correlation-abc", approvalPolicy.CapturedCorrelationId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ReturnsCancelled()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var runner = CreateRunner(new FakeCameraRepository(), new CapturingApprovalPolicy(), CreateHostDriver("generic.host"));

        var result = await runner.ExecuteAsync(
            new DiscoveryOrchestrationRequest
            {
                Candidates = [CreateCandidate()]
            },
            cancellationTokenSource.Token);

        Assert.Equal(DiscoveryOrchestrationStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_IsStateless_ProducesIndependentResultsAcrossCalls()
    {
        var repository = new FakeCameraRepository();
        var runner = CreateRunner(repository, new CapturingApprovalPolicy(), CreateHostDriver("generic.host"));

        var firstResult = await runner.ExecuteAsync(new DiscoveryOrchestrationRequest
        {
            Candidates = [CreateCandidate("host:192.168.1.10", "192.168.1.10", "Lobby")],
            CorrelationId = "first"
        });

        var secondResult = await runner.ExecuteAsync(new DiscoveryOrchestrationRequest
        {
            Candidates = [CreateCandidate("host:192.168.1.11", "192.168.1.11", "Warehouse")],
            CorrelationId = "second"
        });

        Assert.Equal(DiscoveryOrchestrationStatus.Completed, firstResult.Status);
        Assert.Equal(DiscoveryOrchestrationStatus.Completed, secondResult.Status);
        Assert.Equal(2, repository.AddCallCount);
    }

    [Fact]
    public void Constructor_WithNullOrchestrator_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new DiscoveryRunner(null!));
    }

    private static DiscoveryRunner CreateRunner(
        ICameraRepository cameraRepository,
        IDriverApprovalPolicy approvalPolicy,
        params DriverDescriptor[] descriptors)
    {
        var registry = new DriverRegistry();
        foreach (var descriptor in descriptors)
        {
            registry.Register(descriptor);
        }

        var orchestrator = new DiscoveryOrchestrator(
            new AutoDiscoveryCoordinator(
                new ThrowingOnvifDiscoveryService(),
                new ThrowingRtspEndpointProbeService(),
                new ThrowingNetworkScanService()),
            new AutoDiscoveryCandidateEvidenceMapper(),
            new DriverSelectionService(),
            registry,
            approvalPolicy,
            new CameraFactory(),
            new DeviceRegistrationService(cameraRepository));

        return new DiscoveryRunner(orchestrator);
    }

    private static DriverDescriptor CreateHostDriver(
        string driverId,
        DeviceConnectionType connectionType = DeviceConnectionType.RTSP)
    {
        return new DriverDescriptor(
            driverId,
            driverId,
            connectionType,
            static () => throw new InvalidOperationException("Driver factory must not be invoked by orchestration."),
            compatibilityCapability: new DriverCompatibilityCapability(
                requiredEvidence:
                [
                    new DriverCompatibilityEvidenceRequirement(DriverCompatibilityEvidenceKind.Host)
                ]));
    }

    private static AutoDiscoveryCandidate CreateCandidate(int? port = 554)
    {
        return CreateCandidate("host:192.168.1.10", "192.168.1.10", "Lobby", port);
    }

    private static AutoDiscoveryCandidate CreateCandidate(
        string candidateKey,
        string host,
        string name,
        int? port = 554)
    {
        return new AutoDiscoveryCandidate
        {
            CandidateKey = candidateKey,
            Sources = [AutoDiscoverySource.NetworkScan],
            Host = host,
            Port = port,
            Name = name,
            Model = "Model-A",
            Location = "Entrance"
        };
    }

    private sealed class CapturingApprovalPolicy : IDriverApprovalPolicy
    {
        private readonly AutoApproveSingleCompatiblePolicy _inner = new();

        public string? CapturedCorrelationId { get; private set; }

        public DriverApprovalResult Evaluate(DriverApprovalRequest request)
        {
            CapturedCorrelationId = request.CorrelationId;
            return _inner.Evaluate(request);
        }
    }

    private sealed class FakeCameraRepository : ICameraRepository
    {
        public IReadOnlyList<EntityCamera> ExistingCameras { get; init; } = Array.Empty<EntityCamera>();

        public int AddCallCount { get; private set; }

        public IEnumerable<EntityCamera> GetAll()
        {
            return ExistingCameras;
        }

        public EntityCamera? GetById(Guid id)
        {
            return ExistingCameras.FirstOrDefault(camera => camera.Id == id);
        }

        public void Add(EntityCamera camera)
        {
            AddCallCount++;
        }

        public void Update(EntityCamera camera)
        {
        }

        public void Delete(Guid id)
        {
        }
    }

    private sealed class ThrowingOnvifDiscoveryService : IOnvifDiscoveryService
    {
        public Task<IReadOnlyList<VSP.Device.Discovery.Onvif.OnvifDiscoveryResult>> DiscoverAsync(
            VSP.Device.Discovery.Onvif.OnvifDiscoveryRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Discovery should not be called for direct candidate tests.");
        }
    }

    private sealed class ThrowingRtspEndpointProbeService : IRtspEndpointProbeService
    {
        public Task<VSP.Device.Discovery.Rtsp.RtspEndpointProbeResult> ProbeAsync(
            VSP.Device.Discovery.Rtsp.RtspEndpointProbeRequest? request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Discovery should not be called for direct candidate tests.");
        }
    }

    private sealed class ThrowingNetworkScanService : INetworkScanService
    {
        public Task<IReadOnlyList<VSP.Device.Discovery.NetworkScan.NetworkScanResult>> ScanAsync(
            VSP.Device.Discovery.NetworkScan.NetworkScanRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Discovery should not be called for direct candidate tests.");
        }
    }
}
