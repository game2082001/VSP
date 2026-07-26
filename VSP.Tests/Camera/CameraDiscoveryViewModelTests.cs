using VSP.Device.Cameras.Factory;
using VSP.Device.Discovery.AutoDiscovery;
using VSP.Device.Discovery.NetworkScan;
using VSP.Device.Discovery.Onvif;
using VSP.Device.Discovery.Orchestration;
using VSP.Device.Discovery.Rtsp;
using VSP.Device.Drivers;
using VSP.Device.Drivers.Capabilities;
using VSP.Device.Drivers.Selection;
using VSP.Device.Interfaces;
using VSP.Device.Registration;
using VSP.Domain.Enums;
using VSP.UI.ViewModels;
using Xunit;
using EntityCamera = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Camera;

public class CameraDiscoveryViewModelTests
{
    [Fact]
    public async Task ScanAsync_WithOnvifResult_PopulatesCandidateWithPreselectedApprovedDriver()
    {
        var repository = new FakeCameraRepository();
        var onvifService = new FakeOnvifDiscoveryService
        {
            Results = [new OnvifDiscoveryResult { IpAddress = "192.168.1.10", Name = "Lobby Camera" }]
        };
        var viewModel = CreateViewModel(repository, onvifService, CreateHostDriver("generic.onvif"));

        await viewModel.ScanAsync();

        var candidate = Assert.Single(viewModel.Candidates);
        Assert.Equal("Lobby Camera", candidate.Name);
        Assert.True(candidate.CanRegister);
        Assert.NotNull(candidate.SelectedDriver);
        Assert.Contains("candidate(s) found", viewModel.StatusMessage);
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task ScanAsync_WithNoResults_ReportsNoCandidatesFound()
    {
        var viewModel = CreateViewModel(new FakeCameraRepository(), new FakeOnvifDiscoveryService());

        await viewModel.ScanAsync();

        Assert.Empty(viewModel.Candidates);
        Assert.Equal("Scan complete. No candidates found.", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RegisterSelectedAsync_WithNoSelectedCandidates_DoesNotTouchRepository()
    {
        var repository = new FakeCameraRepository();
        var viewModel = CreateViewModel(repository, new FakeOnvifDiscoveryService
        {
            Results = [new OnvifDiscoveryResult { IpAddress = "192.168.1.10", Name = "Lobby Camera" }]
        }, CreateHostDriver("generic.onvif"));
        await viewModel.ScanAsync();

        await viewModel.RegisterSelectedAsync();

        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public async Task RegisterSelectedAsync_WithSelectedCompatibleCandidate_RegistersEditedNameAndMarksRegistered()
    {
        var repository = new FakeCameraRepository();
        var viewModel = CreateViewModel(repository, new FakeOnvifDiscoveryService
        {
            Results = [new OnvifDiscoveryResult { IpAddress = "192.168.1.10", Name = "Lobby Camera" }]
        }, CreateHostDriver("generic.onvif"));
        await viewModel.ScanAsync();

        var candidate = viewModel.Candidates.Single();
        candidate.Name = "Front Entrance Camera";
        candidate.IsSelected = true;

        await viewModel.RegisterSelectedAsync();

        Assert.Equal(1, repository.AddCallCount);
        Assert.Equal("Front Entrance Camera", repository.LastAddedCamera?.Name);
        Assert.True(candidate.IsRegistered);
        Assert.False(candidate.IsSelected);
        Assert.Equal("Registered 1 of 1 selected candidate(s).", viewModel.StatusMessage);
    }

    [Fact]
    public async Task RegisterSelectedAsync_WhenCandidateHasNoChosenDriver_SkipsWithoutTouchingRepository()
    {
        var repository = new FakeCameraRepository();
        var viewModel = CreateViewModel(
            repository,
            new FakeOnvifDiscoveryService
            {
                Results = [new OnvifDiscoveryResult { IpAddress = "192.168.1.10", Name = "Lobby Camera" }]
            },
            CreateHostDriver("generic.one"),
            CreateHostDriver("generic.two", DeviceConnectionType.ONVIF));
        await viewModel.ScanAsync();

        var candidate = viewModel.Candidates.Single();
        Assert.True(candidate.DriverOptions.Count > 1);
        Assert.Null(candidate.SelectedDriver);
        candidate.IsSelected = true;

        await viewModel.RegisterSelectedAsync();

        Assert.Equal(0, repository.AddCallCount);
        Assert.False(candidate.IsRegistered);
    }

    private static CameraDiscoveryViewModel CreateViewModel(
        ICameraRepository cameraRepository,
        IOnvifDiscoveryService onvifDiscoveryService,
        params DriverDescriptor[] descriptors)
    {
        var registry = new DriverRegistry();
        foreach (var descriptor in descriptors)
        {
            registry.Register(descriptor);
        }

        var orchestrator = new DiscoveryOrchestrator(
            new AutoDiscoveryCoordinator(
                onvifDiscoveryService,
                new EmptyRtspEndpointProbeService(),
                new EmptyNetworkScanService()),
            new AutoDiscoveryCandidateEvidenceMapper(),
            new DriverSelectionService(),
            registry,
            new AutoApproveSingleCompatiblePolicy(),
            new CameraFactory(),
            new DeviceRegistrationService(cameraRepository));

        return new CameraDiscoveryViewModel(orchestrator)
        {
            EnableOnvifDiscovery = true
        };
    }

    private static DriverDescriptor CreateHostDriver(
        string driverId,
        DeviceConnectionType connectionType = DeviceConnectionType.RTSP)
    {
        return new DriverDescriptor(
            driverId,
            driverId,
            connectionType,
            static () => throw new InvalidOperationException("Driver factory must not be invoked."),
            compatibilityCapability: new DriverCompatibilityCapability(
                requiredEvidence:
                [
                    new DriverCompatibilityEvidenceRequirement(DriverCompatibilityEvidenceKind.Host)
                ]));
    }

    private sealed class FakeOnvifDiscoveryService : IOnvifDiscoveryService
    {
        public IReadOnlyList<OnvifDiscoveryResult> Results { get; init; } = Array.Empty<OnvifDiscoveryResult>();

        public Task<IReadOnlyList<OnvifDiscoveryResult>> DiscoverAsync(
            OnvifDiscoveryRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Results);
        }
    }

    private sealed class EmptyRtspEndpointProbeService : IRtspEndpointProbeService
    {
        public Task<RtspEndpointProbeResult> ProbeAsync(
            RtspEndpointProbeRequest? request = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new RtspEndpointProbeResult
            {
                NormalizedEndpointUri = "rtsp://unused/",
                Host = "unused",
                Port = 554,
                ResponseClassification = RtspProbeResponseClassification.ConnectionFailed
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

    private sealed class FakeCameraRepository : ICameraRepository
    {
        private readonly List<EntityCamera> _cameras = [];

        public int AddCallCount { get; private set; }

        public EntityCamera? LastAddedCamera { get; private set; }

        public IEnumerable<EntityCamera> GetAll() => _cameras;

        public EntityCamera? GetById(Guid id) => _cameras.FirstOrDefault(camera => camera.Id == id);

        public void Add(EntityCamera camera)
        {
            AddCallCount++;
            LastAddedCamera = camera;
            _cameras.Add(camera);
        }

        public void Update(EntityCamera camera)
        {
        }

        public void Delete(Guid id)
        {
        }
    }
}
