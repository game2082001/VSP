using VSP.Device.Interfaces;
using VSP.Device.Registration;
using VSP.Domain.Enums;
using Xunit;
using EntityCamera = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Registration;

public class DeviceRegistrationServiceTests
{
    [Fact]
    public void Register_WithNullRequest_ReturnsRejectedResult()
    {
        var service = new DeviceRegistrationService(new FakeCameraRepository());

        var result = service.Register(null);

        Assert.Equal(DeviceRegistrationStatus.Rejected, result.Status);
        Assert.Contains(result.Reasons, reason => reason.Code == "MissingRequest");
    }

    [Fact]
    public void Register_WithMissingCamera_ReturnsRejectedResult()
    {
        var service = new DeviceRegistrationService(new FakeCameraRepository());

        var result = service.Register(new DeviceRegistrationRequest());

        Assert.Equal(DeviceRegistrationStatus.Rejected, result.Status);
        Assert.Contains(result.Reasons, reason => reason.Code == "MissingCamera");
    }

    [Fact]
    public void Register_DetectsDuplicateName()
    {
        var repository = new FakeCameraRepository([CreateCamera(name: "Lobby")]);
        var service = new DeviceRegistrationService(repository);

        var result = service.Register(CreateRequest(CreateCamera(name: "lobby")));

        Assert.Equal(DeviceRegistrationStatus.Rejected, result.Status);
        Assert.Contains(result.DuplicateResults, duplicate => duplicate.Code == "DuplicateName");
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public void Register_DetectsDuplicateIpAddress()
    {
        var repository = new FakeCameraRepository([CreateCamera(ipAddress: "192.168.1.10")]);
        var service = new DeviceRegistrationService(repository);

        var result = service.Register(CreateRequest(CreateCamera(ipAddress: "192.168.1.10")));

        Assert.Equal(DeviceRegistrationStatus.Rejected, result.Status);
        Assert.Contains(result.DuplicateResults, duplicate => duplicate.Code == "DuplicateIpAddress");
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public void Register_DetectsDuplicateRtspUrlWhenNonEmpty()
    {
        var repository = new FakeCameraRepository([CreateCamera(rtspUrl: "rtsp://192.168.1.10/stream1")]);
        var service = new DeviceRegistrationService(repository);

        var result = service.Register(CreateRequest(CreateCamera(
            ipAddress: "192.168.1.11",
            rtspUrl: "RTSP://192.168.1.10/stream1")));

        Assert.Equal(DeviceRegistrationStatus.Rejected, result.Status);
        Assert.Contains(result.DuplicateResults, duplicate => duplicate.Code == "DuplicateRtspUrl");
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public void Register_DoesNotTreatEmptyRtspUrlAsDuplicate()
    {
        var repository = new FakeCameraRepository([CreateCamera(rtspUrl: "")]);
        var service = new DeviceRegistrationService(repository);

        var result = service.Register(CreateRequest(CreateCamera(
            name: "Back Door",
            ipAddress: "192.168.1.11",
            rtspUrl: "")));

        Assert.Equal(DeviceRegistrationStatus.Registered, result.Status);
        Assert.Equal(1, repository.AddCallCount);
    }

    [Fact]
    public void Register_WithRejectPolicyRejectsDuplicateWithoutRepositoryWrite()
    {
        var repository = new FakeCameraRepository([CreateCamera(name: "Lobby")]);
        var service = new DeviceRegistrationService(repository);

        var result = service.Register(CreateRequest(
            CreateCamera(name: "Lobby"),
            DuplicatePolicy.Reject));

        Assert.Equal(DeviceRegistrationStatus.Rejected, result.Status);
        Assert.Contains(result.Reasons, reason => reason.Code == "DuplicateName");
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public void Register_WithSkipPolicySkipsDuplicateWithoutRepositoryWrite()
    {
        var repository = new FakeCameraRepository([CreateCamera(name: "Lobby")]);
        var service = new DeviceRegistrationService(repository);

        var result = service.Register(CreateRequest(
            CreateCamera(name: "Lobby"),
            DuplicatePolicy.Skip));

        Assert.Equal(DeviceRegistrationStatus.SkippedDuplicate, result.Status);
        Assert.Contains(result.Reasons, reason => reason.Code == "SkippedDuplicate");
        Assert.Equal(0, repository.AddCallCount);
    }

    [Fact]
    public void Register_WithValidCameraWritesRepositoryAndReturnsRegistered()
    {
        var camera = CreateCamera();
        var repository = new FakeCameraRepository();
        var service = new DeviceRegistrationService(repository);

        var result = service.Register(CreateRequest(camera));

        Assert.Equal(DeviceRegistrationStatus.Registered, result.Status);
        Assert.Equal(camera.Id, result.RegisteredCameraId);
        Assert.Contains(result.Reasons, reason => reason.Code == "Registered");
        Assert.Equal(1, repository.AddCallCount);
        Assert.Same(camera, Assert.Single(repository.AddedCameras));
    }

    [Fact]
    public void Register_WhenRepositoryFails_ReturnsFailedResult()
    {
        var repository = new FakeCameraRepository
        {
            ThrowOnAdd = true
        };
        var service = new DeviceRegistrationService(repository);

        var result = service.Register(CreateRequest(CreateCamera()));

        Assert.Equal(DeviceRegistrationStatus.Failed, result.Status);
        Assert.Contains(result.Reasons, reason => reason.Code == "RepositoryError");
    }

    [Fact]
    public void Register_DoesNotCreateOrModifyCamera()
    {
        var camera = CreateCamera();
        var originalId = camera.Id;
        var originalCreateTime = camera.CreateTime;
        var originalLastModifyTime = camera.LastModifyTime;
        var service = new DeviceRegistrationService(new FakeCameraRepository());

        service.Register(CreateRequest(camera));

        Assert.Equal(originalId, camera.Id);
        Assert.Equal(originalCreateTime, camera.CreateTime);
        Assert.Equal(originalLastModifyTime, camera.LastModifyTime);
    }

    [Fact]
    public void DeviceRegistrationService_DependsOnlyOnICameraRepository()
    {
        var constructor = Assert.Single(typeof(DeviceRegistrationService).GetConstructors());
        var parameter = Assert.Single(constructor.GetParameters());

        Assert.Equal(typeof(ICameraRepository), parameter.ParameterType);
    }

    [Fact]
    public void DeviceRegistrationService_HasNoSqliteDiscoveryDriverSelectionOrFactoryDependency()
    {
        var exposedTypeNames = typeof(DeviceRegistrationService)
            .GetConstructors()
            .SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType.FullName ?? string.Empty))
            .Concat(typeof(DeviceRegistrationService).GetMethods().Select(method => method.ReturnType.FullName ?? string.Empty));

        Assert.DoesNotContain(exposedTypeNames, name => name.Contains("SQLite"));
        Assert.DoesNotContain(exposedTypeNames, name => name.Contains("Discovery"));
        Assert.DoesNotContain(exposedTypeNames, name => name.Contains("Drivers.Selection"));
        Assert.DoesNotContain(exposedTypeNames, name => name.Contains("Cameras.Factory"));
    }

    private static DeviceRegistrationRequest CreateRequest(
        EntityCamera camera,
        DuplicatePolicy duplicatePolicy = DuplicatePolicy.Reject)
    {
        return new DeviceRegistrationRequest
        {
            Camera = camera,
            Source = RegistrationSource.Discovery,
            DuplicatePolicy = duplicatePolicy
        };
    }

    private static EntityCamera CreateCamera(
        string name = "Lobby",
        string ipAddress = "192.168.1.10",
        string rtspUrl = "rtsp://192.168.1.10/stream1")
    {
        return new EntityCamera
        {
            Name = name,
            IpAddress = ipAddress,
            Brand = CameraBrand.RTSP,
            ConnectionType = DeviceConnectionType.RTSP,
            RtspUrl = rtspUrl,
            CreateTime = new DateTime(2026, 7, 19, 10, 0, 0),
            LastModifyTime = new DateTime(2026, 7, 19, 10, 0, 0)
        };
    }

    private sealed class FakeCameraRepository : ICameraRepository
    {
        private readonly List<EntityCamera> _cameras;

        public FakeCameraRepository(IEnumerable<EntityCamera>? cameras = null)
        {
            _cameras = cameras?.ToList() ?? [];
        }

        public int AddCallCount { get; private set; }

        public bool ThrowOnAdd { get; init; }

        public IReadOnlyList<EntityCamera> AddedCameras { get; private set; } = Array.Empty<EntityCamera>();

        public IEnumerable<EntityCamera> GetAll()
        {
            return _cameras;
        }

        public EntityCamera? GetById(Guid id)
        {
            return _cameras.FirstOrDefault(camera => camera.Id == id);
        }

        public void Add(EntityCamera camera)
        {
            AddCallCount++;

            if (ThrowOnAdd)
            {
                throw new InvalidOperationException("Repository failure.");
            }

            _cameras.Add(camera);
            AddedCameras = AddedCameras.Concat([camera]).ToArray();
        }

        public void Update(EntityCamera camera)
        {
        }

        public void Delete(Guid id)
        {
        }
    }
}
