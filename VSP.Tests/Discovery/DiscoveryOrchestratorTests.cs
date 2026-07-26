using VSP.Device.Cameras.Factory;
using VSP.Device.Discovery.AutoDiscovery;
using VSP.Device.Discovery.NetworkScan;
using VSP.Device.Discovery.Onvif;
using VSP.Device.Discovery.Orchestration;
using VSP.Device.Discovery.Rtsp;
using VSP.Device.Discovery.Sessions;
using VSP.Device.Drivers;
using VSP.Device.Drivers.Capabilities;
using VSP.Device.Drivers.Selection;
using VSP.Device.Interfaces;
using VSP.Device.Registration;
using VSP.Domain.Enums;
using Xunit;
using EntityCamera = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Discovery;

public class DiscoveryOrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_WithSingleCompatibleDriver_RegistersCamera()
    {
        var repository = new FakeCameraRepository();
        var orchestrator = CreateOrchestrator(repository, CreateHostDriver("generic.host"));

        var result = await orchestrator.ExecuteAsync(new DiscoveryOrchestrationRequest
        {
            Candidates = [CreateCandidate()]
        });

        Assert.Equal(DiscoveryOrchestrationStatus.Completed, result.Status);
        Assert.Equal(1, result.Summary.TotalCandidates);
        Assert.Equal(1, result.Summary.Approved);
        Assert.Equal(1, result.Summary.Registered);
        Assert.Equal(CandidateOrchestrationStatus.Registered, result.CandidateResults.Single().Status);
        Assert.Equal(1, repository.AddCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoCompatibleDriver_ReturnsNoCompatibleDriver()
    {
        var orchestrator = CreateOrchestrator(new FakeCameraRepository());

        var result = await orchestrator.ExecuteAsync(new DiscoveryOrchestrationRequest
        {
            Candidates = [CreateCandidate()]
        });

        var candidateResult = result.CandidateResults.Single();
        Assert.Equal(DiscoveryOrchestrationStatus.CompletedWithErrors, result.Status);
        Assert.Equal(CandidateOrchestrationStatus.NoCompatibleDriver, candidateResult.Status);
        Assert.Equal(0, result.Summary.Registered);
        Assert.Equal(1, result.Summary.Rejected);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleCompatibleDrivers_ReturnsAwaitingApproval()
    {
        var repository = new FakeCameraRepository();
        var orchestrator = CreateOrchestrator(
            repository,
            CreateHostDriver("generic.host.one"),
            CreateHostDriver("generic.host.two", DeviceConnectionType.ONVIF));

        var result = await orchestrator.ExecuteAsync(new DiscoveryOrchestrationRequest
        {
            Candidates = [CreateCandidate()]
        });

        var candidateResult = result.CandidateResults.Single();
        Assert.Equal(DiscoveryOrchestrationStatus.CompletedWithErrors, result.Status);
        Assert.Equal(CandidateOrchestrationStatus.AwaitingApproval, candidateResult.Status);
        Assert.Equal(1, result.Summary.AwaitingApproval);
        Assert.Equal(0, repository.AddCallCount);
        Assert.Null(candidateResult.CameraFactoryResult);
        Assert.Null(candidateResult.RegistrationResult);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCameraFactoryRejects_ReturnsCameraFactoryRejected()
    {
        var repository = new FakeCameraRepository();
        var orchestrator = CreateOrchestrator(repository, CreateHostDriver("generic.host"));

        var result = await orchestrator.ExecuteAsync(new DiscoveryOrchestrationRequest
        {
            Candidates =
            [
                CreateCandidate(port: 70000)
            ]
        });

        var candidateResult = result.CandidateResults.Single();
        Assert.Equal(DiscoveryOrchestrationStatus.CompletedWithErrors, result.Status);
        Assert.Equal(CandidateOrchestrationStatus.CameraFactoryRejected, candidateResult.Status);
        Assert.Equal(1, result.Summary.Approved);
        Assert.Equal(0, repository.AddCallCount);
        Assert.Contains(candidateResult.Reasons, reason => reason.Code == "InvalidPort");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRegistrationRejects_ReturnsRegistrationRejected()
    {
        var repository = new FakeCameraRepository
        {
            ExistingCameras =
            [
                new EntityCamera
                {
                    Name = "Lobby",
                    IpAddress = "192.168.1.10"
                }
            ]
        };
        var orchestrator = CreateOrchestrator(repository, CreateHostDriver("generic.host"));

        var result = await orchestrator.ExecuteAsync(new DiscoveryOrchestrationRequest
        {
            Candidates = [CreateCandidate()]
        });

        var candidateResult = result.CandidateResults.Single();
        Assert.Equal(DiscoveryOrchestrationStatus.CompletedWithErrors, result.Status);
        Assert.Equal(CandidateOrchestrationStatus.RegistrationRejected, candidateResult.Status);
        Assert.Equal(0, repository.AddCallCount);
        Assert.Contains(candidateResult.Reasons, reason => reason.Code == "DuplicateName");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDuplicatePolicyIsSkip_ReturnsDuplicateSkipped()
    {
        var repository = new FakeCameraRepository
        {
            ExistingCameras =
            [
                new EntityCamera
                {
                    Name = "Lobby",
                    IpAddress = "192.168.1.10"
                }
            ]
        };
        var orchestrator = CreateOrchestrator(repository, CreateHostDriver("generic.host"));

        var result = await orchestrator.ExecuteAsync(new DiscoveryOrchestrationRequest
        {
            Candidates = [CreateCandidate()],
            DuplicatePolicy = DuplicatePolicy.Skip
        });

        var candidateResult = result.CandidateResults.Single();
        Assert.Equal(DiscoveryOrchestrationStatus.CompletedWithErrors, result.Status);
        Assert.Equal(CandidateOrchestrationStatus.DuplicateSkipped, candidateResult.Status);
        Assert.Equal(1, result.Summary.DuplicateSkipped);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_ReturnsCancelled()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var orchestrator = CreateOrchestrator(new FakeCameraRepository(), CreateHostDriver("generic.host"));

        var result = await orchestrator.ExecuteAsync(
            new DiscoveryOrchestrationRequest
            {
                Candidates = [CreateCandidate()]
            },
            cancellationTokenSource.Token);

        Assert.Equal(DiscoveryOrchestrationStatus.Cancelled, result.Status);
        Assert.Empty(result.CandidateResults);
        Assert.Contains(result.Reasons, reason => reason.Code == "Cancelled");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledAfterFirstCandidate_PreservesCompletedCandidateResult()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var repository = new FakeCameraRepository
        {
            OnAdd = () => cancellationTokenSource.Cancel()
        };
        var orchestrator = CreateOrchestrator(repository, CreateHostDriver("generic.host"));

        var result = await orchestrator.ExecuteAsync(
            new DiscoveryOrchestrationRequest
            {
                Candidates =
                [
                    CreateCandidate("host:192.168.1.10", "192.168.1.10", "Lobby"),
                    CreateCandidate("host:192.168.1.11", "192.168.1.11", "Warehouse"),
                    CreateCandidate("host:192.168.1.12", "192.168.1.12", "Gate")
                ]
            },
            cancellationTokenSource.Token);

        Assert.Equal(DiscoveryOrchestrationStatus.Cancelled, result.Status);
        var candidateResult = Assert.Single(result.CandidateResults);
        Assert.Equal("host:192.168.1.10", candidateResult.Candidate?.CandidateKey);
        Assert.Equal(CandidateOrchestrationStatus.Registered, candidateResult.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledAfterFirstCandidate_CreatesSummaryFromCompletedCandidateResults()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var repository = new FakeCameraRepository
        {
            OnAdd = () => cancellationTokenSource.Cancel()
        };
        var orchestrator = CreateOrchestrator(repository, CreateHostDriver("generic.host"));

        var result = await orchestrator.ExecuteAsync(
            new DiscoveryOrchestrationRequest
            {
                Candidates =
                [
                    CreateCandidate("host:192.168.1.10", "192.168.1.10", "Lobby"),
                    CreateCandidate("host:192.168.1.11", "192.168.1.11", "Warehouse")
                ]
            },
            cancellationTokenSource.Token);

        Assert.Single(result.CandidateResults);
        Assert.Equal(1, result.Summary.TotalCandidates);
        Assert.Equal(1, result.Summary.Registered);
        Assert.Equal(1, result.Summary.Approved);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelledAfterFirstCandidate_DoesNotCreateInFlightOrUnstartedCandidateResults()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var repository = new FakeCameraRepository
        {
            OnAdd = () => cancellationTokenSource.Cancel()
        };
        var orchestrator = CreateOrchestrator(repository, CreateHostDriver("generic.host"));

        var result = await orchestrator.ExecuteAsync(
            new DiscoveryOrchestrationRequest
            {
                Candidates =
                [
                    CreateCandidate("host:192.168.1.10", "192.168.1.10", "Lobby"),
                    CreateCandidate("host:192.168.1.11", "192.168.1.11", "Warehouse"),
                    CreateCandidate("host:192.168.1.12", "192.168.1.12", "Gate")
                ]
            },
            cancellationTokenSource.Token);

        Assert.DoesNotContain(result.CandidateResults, candidateResult =>
            candidateResult.Candidate?.CandidateKey == "host:192.168.1.11");
        Assert.DoesNotContain(result.CandidateResults, candidateResult =>
            candidateResult.Candidate?.CandidateKey == "host:192.168.1.12");
    }

    [Fact]
    public async Task ExecuteAsync_WhenOperationTimeoutOccurs_DoesNotReturnCancelled()
    {
        var orchestrator = CreateOrchestratorWithDiscoveryServices(
            new EmptyOnvifDiscoveryService(),
            new TimeoutRtspEndpointProbeService(),
            new EmptyNetworkScanService(),
            new FakeCameraRepository());

        var result = await orchestrator.ExecuteAsync(new DiscoveryOrchestrationRequest
        {
            DiscoveryRequest = new AutoDiscoveryRequest
            {
                EnableOnvifDiscovery = false,
                EnableNetworkScan = false,
                EnableRtspEndpointProbe = true,
                RtspEndpointProbeRequests =
                [
                    new RtspEndpointProbeRequest
                    {
                        RtspUri = "rtsp://192.168.1.50/stream"
                    }
                ]
            }
        });

        Assert.NotEqual(DiscoveryOrchestrationStatus.Cancelled, result.Status);
        var candidateResult = Assert.Single(result.CandidateResults);
        Assert.Equal("Timeout", candidateResult.Candidate?.ProbeClassification);
    }

    [Fact]
    public async Task DiscoverCandidatesAsync_WithSingleCompatibleDriver_ReturnsApprovedWithoutTouchingRepository()
    {
        var repository = new FakeCameraRepository();
        var orchestrator = CreateOrchestrator(repository, CreateHostDriver("generic.host"));

        var results = await orchestrator.DiscoverCandidatesAsync(new DiscoveryOrchestrationRequest
        {
            Candidates = [CreateCandidate()]
        });

        var candidateResult = Assert.Single(results);
        Assert.Equal(CandidateOrchestrationStatus.Approved, candidateResult.Status);
        Assert.NotNull(candidateResult.DriverApprovalResult?.ApprovedDriver);
        Assert.Null(candidateResult.CameraFactoryResult);
        Assert.Null(candidateResult.RegistrationResult);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task DiscoverCandidatesAsync_WithMultipleCompatibleDrivers_ReturnsAwaitingApprovalWithCompatibleDriverList()
    {
        var orchestrator = CreateOrchestrator(
            new FakeCameraRepository(),
            CreateHostDriver("generic.host.one"),
            CreateHostDriver("generic.host.two", DeviceConnectionType.ONVIF));

        var results = await orchestrator.DiscoverCandidatesAsync(new DiscoveryOrchestrationRequest
        {
            Candidates = [CreateCandidate()]
        });

        var candidateResult = Assert.Single(results);
        Assert.Equal(CandidateOrchestrationStatus.AwaitingApproval, candidateResult.Status);
        Assert.Equal(2, candidateResult.DriverApprovalResult?.CompatibleDrivers.Count);
        Assert.Null(candidateResult.CameraFactoryResult);
    }

    [Fact]
    public async Task DiscoverCandidatesAsync_WithNoCompatibleDriver_ReturnsNoCompatibleDriver()
    {
        var orchestrator = CreateOrchestrator(new FakeCameraRepository());

        var results = await orchestrator.DiscoverCandidatesAsync(new DiscoveryOrchestrationRequest
        {
            Candidates = [CreateCandidate()]
        });

        var candidateResult = Assert.Single(results);
        Assert.Equal(CandidateOrchestrationStatus.NoCompatibleDriver, candidateResult.Status);
    }

    [Fact]
    public async Task DiscoverCandidatesAsync_WithInvalidRequest_ThrowsArgumentException()
    {
        var orchestrator = CreateOrchestrator(new FakeCameraRepository(), CreateHostDriver("generic.host"));

        await Assert.ThrowsAsync<ArgumentException>(
            () => orchestrator.DiscoverCandidatesAsync(new DiscoveryOrchestrationRequest()));
    }

    [Fact]
    public async Task RegisterCandidate_WithApprovedDriverAndNameOverride_RegistersCameraUsingOverride()
    {
        var repository = new FakeCameraRepository();
        var orchestrator = CreateOrchestrator(repository, CreateHostDriver("generic.host"));
        var candidate = CreateCandidate();

        var evaluated = await orchestrator.DiscoverCandidatesAsync(new DiscoveryOrchestrationRequest
        {
            Candidates = [candidate]
        });
        var approvedDriver = evaluated.Single().DriverApprovalResult!.ApprovedDriver!;

        var result = orchestrator.RegisterCandidate(
            candidate,
            approvedDriver,
            new DiscoveryOrchestrationRequest { DuplicatePolicy = DuplicatePolicy.Reject },
            nameOverride: "Front Entrance Camera");

        Assert.Equal(CandidateOrchestrationStatus.Registered, result.Status);
        Assert.Equal(1, repository.AddCallCount);
        Assert.Equal("Front Entrance Camera", repository.LastAddedCamera?.Name);
    }

    [Fact]
    public async Task RegisterCandidate_WithoutNameOverride_FallsBackToCandidateDerivedName()
    {
        var repository = new FakeCameraRepository();
        var orchestrator = CreateOrchestrator(repository, CreateHostDriver("generic.host"));
        var candidate = CreateCandidate();

        var evaluated = await orchestrator.DiscoverCandidatesAsync(new DiscoveryOrchestrationRequest
        {
            Candidates = [candidate]
        });
        var approvedDriver = evaluated.Single().DriverApprovalResult!.ApprovedDriver!;

        var result = orchestrator.RegisterCandidate(
            candidate,
            approvedDriver,
            new DiscoveryOrchestrationRequest { DuplicatePolicy = DuplicatePolicy.Reject });

        Assert.Equal(CandidateOrchestrationStatus.Registered, result.Status);
        Assert.Equal(candidate.Name, repository.LastAddedCamera?.Name);
    }

    [Fact]
    public async Task RegisterCandidate_WhenDuplicateAndPolicyIsSkip_DoesNotWriteRepository()
    {
        var repository = new FakeCameraRepository
        {
            ExistingCameras = [new EntityCamera { Name = "Lobby", IpAddress = "192.168.1.10" }]
        };
        var orchestrator = CreateOrchestrator(repository, CreateHostDriver("generic.host"));
        var candidate = CreateCandidate();

        var evaluated = await orchestrator.DiscoverCandidatesAsync(new DiscoveryOrchestrationRequest
        {
            Candidates = [candidate]
        });
        var approvedDriver = evaluated.Single().DriverApprovalResult!.ApprovedDriver!;

        var result = orchestrator.RegisterCandidate(
            candidate,
            approvedDriver,
            new DiscoveryOrchestrationRequest { DuplicatePolicy = DuplicatePolicy.Skip });

        Assert.Equal(CandidateOrchestrationStatus.DuplicateSkipped, result.Status);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task DiscoverThenRegisterCandidate_ProducesSameOutcomeAsExecuteAsync_ForSingleCompatibleDriver()
    {
        var repositoryForExecute = new FakeCameraRepository();
        var orchestratorForExecute = CreateOrchestrator(repositoryForExecute, CreateHostDriver("generic.host"));
        var candidate = CreateCandidate();

        var executeResult = await orchestratorForExecute.ExecuteAsync(new DiscoveryOrchestrationRequest
        {
            Candidates = [candidate]
        });

        var repositoryForTwoPhase = new FakeCameraRepository();
        var orchestratorForTwoPhase = CreateOrchestrator(repositoryForTwoPhase, CreateHostDriver("generic.host"));
        var evaluated = await orchestratorForTwoPhase.DiscoverCandidatesAsync(new DiscoveryOrchestrationRequest
        {
            Candidates = [candidate]
        });
        var approvedDriver = evaluated.Single().DriverApprovalResult!.ApprovedDriver!;
        var registerResult = orchestratorForTwoPhase.RegisterCandidate(
            candidate,
            approvedDriver,
            new DiscoveryOrchestrationRequest());

        Assert.Equal(CandidateOrchestrationStatus.Registered, executeResult.CandidateResults.Single().Status);
        Assert.Equal(CandidateOrchestrationStatus.Registered, registerResult.Status);
        Assert.Equal(1, repositoryForExecute.AddCallCount);
        Assert.Equal(1, repositoryForTwoPhase.AddCallCount);
    }

    [Fact]
    public async Task SessionFactory_MapsCancelledResultToCancelledSession()
    {
        var factory = new DiscoverySessionFactory();
        var result = new DiscoveryOrchestrationResult
        {
            Status = DiscoveryOrchestrationStatus.Cancelled,
            CandidateResults =
            [
                new CandidateOrchestrationResult
                {
                    Candidate = CreateCandidate(),
                    Status = CandidateOrchestrationStatus.Registered
                }
            ],
            Summary = new DiscoveryOrchestrationSummary
            {
                TotalCandidates = 1,
                Registered = 1
            },
            Reasons =
            [
                new DiscoveryOrchestrationReason("Cancelled", "Discovery orchestration was cancelled by the caller.")
            ]
        };

        var session = factory.Create(new DiscoverySessionFactoryRequest
        {
            DiscoveryOrchestrationResult = result,
            StartTime = new DateTimeOffset(2026, 7, 20, 10, 0, 0, TimeSpan.Zero),
            EndTime = new DateTimeOffset(2026, 7, 20, 10, 0, 1, TimeSpan.Zero)
        });

        Assert.Equal(DiscoverySessionStatus.Cancelled, session.Status);
        Assert.Same(result.Summary, session.Snapshot.Summary);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequestHasBothDiscoveryRequestAndCandidates_ReturnsInvalidRequest()
    {
        var orchestrator = CreateOrchestrator(new FakeCameraRepository(), CreateHostDriver("generic.host"));

        var result = await orchestrator.ExecuteAsync(new DiscoveryOrchestrationRequest
        {
            DiscoveryRequest = new AutoDiscoveryRequest(),
            Candidates = [CreateCandidate()]
        });

        Assert.Equal(DiscoveryOrchestrationStatus.InvalidRequest, result.Status);
        Assert.Contains(result.Reasons, reason => reason.Code == "InvalidRequest");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRequestHasNeitherDiscoveryRequestNorCandidates_ReturnsInvalidRequest()
    {
        var orchestrator = CreateOrchestrator(new FakeCameraRepository(), CreateHostDriver("generic.host"));

        var result = await orchestrator.ExecuteAsync(new DiscoveryOrchestrationRequest());

        Assert.Equal(DiscoveryOrchestrationStatus.InvalidRequest, result.Status);
        Assert.Contains(result.Reasons, reason => reason.Code == "InvalidRequest");
    }

    private static DiscoveryOrchestrator CreateOrchestrator(
        ICameraRepository cameraRepository,
        params DriverDescriptor[] descriptors)
    {
        var registry = new DriverRegistry();
        foreach (var descriptor in descriptors)
        {
            registry.Register(descriptor);
        }

        return new DiscoveryOrchestrator(
            new AutoDiscoveryCoordinator(
                new ThrowingOnvifDiscoveryService(),
                new ThrowingRtspEndpointProbeService(),
                new ThrowingNetworkScanService()),
            new AutoDiscoveryCandidateEvidenceMapper(),
            new DriverSelectionService(),
            registry,
            new AutoApproveSingleCompatiblePolicy(),
            new CameraFactory(),
            new DeviceRegistrationService(cameraRepository));
    }

    private static DiscoveryOrchestrator CreateOrchestratorWithDiscoveryServices(
        IOnvifDiscoveryService onvifDiscoveryService,
        IRtspEndpointProbeService rtspEndpointProbeService,
        INetworkScanService networkScanService,
        ICameraRepository cameraRepository,
        params DriverDescriptor[] descriptors)
    {
        var registry = new DriverRegistry();
        foreach (var descriptor in descriptors)
        {
            registry.Register(descriptor);
        }

        return new DiscoveryOrchestrator(
            new AutoDiscoveryCoordinator(
                onvifDiscoveryService,
                rtspEndpointProbeService,
                networkScanService),
            new AutoDiscoveryCandidateEvidenceMapper(),
            new DriverSelectionService(),
            registry,
            new AutoApproveSingleCompatiblePolicy(),
            new CameraFactory(),
            new DeviceRegistrationService(cameraRepository));
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

    private sealed class FakeCameraRepository : ICameraRepository
    {
        public IReadOnlyList<EntityCamera> ExistingCameras { get; init; } = Array.Empty<EntityCamera>();

        public Action? OnAdd { get; init; }

        public int AddCallCount { get; private set; }

        public EntityCamera? LastAddedCamera { get; private set; }

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
            LastAddedCamera = camera;
            OnAdd?.Invoke();
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
        public Task<IReadOnlyList<OnvifDiscoveryResult>> DiscoverAsync(
            OnvifDiscoveryRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Discovery should not be called for direct candidate tests.");
        }
    }

    private sealed class ThrowingRtspEndpointProbeService : IRtspEndpointProbeService
    {
        public Task<RtspEndpointProbeResult> ProbeAsync(
            RtspEndpointProbeRequest? request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Discovery should not be called for direct candidate tests.");
        }
    }

    private sealed class ThrowingNetworkScanService : INetworkScanService
    {
        public Task<IReadOnlyList<NetworkScanResult>> ScanAsync(
            NetworkScanRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Discovery should not be called for direct candidate tests.");
        }
    }

    private sealed class EmptyOnvifDiscoveryService : IOnvifDiscoveryService
    {
        public Task<IReadOnlyList<OnvifDiscoveryResult>> DiscoverAsync(
            OnvifDiscoveryRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<OnvifDiscoveryResult>>(Array.Empty<OnvifDiscoveryResult>());
        }
    }

    private sealed class TimeoutRtspEndpointProbeService : IRtspEndpointProbeService
    {
        public Task<RtspEndpointProbeResult> ProbeAsync(
            RtspEndpointProbeRequest? request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RtspEndpointProbeResult
            {
                NormalizedEndpointUri = request?.RtspUri ?? "rtsp://192.168.1.50/stream",
                Host = "192.168.1.50",
                Port = 554,
                ResponseClassification = RtspProbeResponseClassification.Timeout,
                TimeoutOccurred = true,
                ErrorCategory = nameof(OperationCanceledException),
                ErrorMessage = "The RTSP probe timed out."
            });
        }
    }

    private sealed class EmptyNetworkScanService : INetworkScanService
    {
        public Task<IReadOnlyList<NetworkScanResult>> ScanAsync(
            NetworkScanRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<NetworkScanResult>>(Array.Empty<NetworkScanResult>());
        }
    }
}
