using VSP.Device.Drivers;
using VSP.Device.Drivers.Abstractions;
using VSP.Device.Drivers.RTSP;
using VSP.Domain.Entities;
using VSP.Domain.Enums;
using Xunit;
using EntityCamera = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Drivers;

public class DriverRegistryTests
{
    [Fact]
    public void DriverDescriptor_RequiresNonEmptyDriverId()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new DriverDescriptor("", "Test Driver", DeviceConnectionType.RTSP, static () => new TestCameraDriver()));

        Assert.Equal("driverId", exception.ParamName);
    }

    [Fact]
    public void DriverDescriptor_RequiresNonEmptyDisplayName()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new DriverDescriptor("test.driver", "", DeviceConnectionType.RTSP, static () => new TestCameraDriver()));

        Assert.Equal("displayName", exception.ParamName);
    }

    [Fact]
    public void DriverDescriptor_RequiresFactory()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new DriverDescriptor("test.driver", "Test Driver", DeviceConnectionType.RTSP, null!));
    }

    [Fact]
    public void Register_AddsDescriptorToRegistry()
    {
        var registry = new DriverRegistry();
        var descriptor = CreateDescriptor("test.driver", "Test Driver", DeviceConnectionType.RTSP);

        registry.Register(descriptor);

        var registered = Assert.Single(registry.GetAll());
        Assert.Same(descriptor, registered);
    }

    [Fact]
    public void Register_RejectsDuplicateDriverId()
    {
        var registry = new DriverRegistry();
        registry.Register(CreateDescriptor("test.driver", "First Driver", DeviceConnectionType.RTSP));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.Register(CreateDescriptor("test.driver", "Second Driver", DeviceConnectionType.ONVIF)));

        Assert.Equal("DriverId 'test.driver' is already registered.", exception.Message);
    }

    [Fact]
    public void Register_RejectsDuplicateConnectionType()
    {
        var registry = new DriverRegistry();
        registry.Register(CreateDescriptor("first.driver", "First Driver", DeviceConnectionType.RTSP));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.Register(CreateDescriptor("second.driver", "Second Driver", DeviceConnectionType.RTSP)));

        Assert.Equal("DeviceConnectionType 'RTSP' is already registered.", exception.Message);
    }

    [Fact]
    public void GetByDriverId_ReturnsNullForUnknownDriver()
    {
        var registry = new DriverRegistry();

        var result = registry.GetByDriverId("missing.driver");

        Assert.Null(result);
    }

    [Fact]
    public void GetByConnectionType_ReturnsNullForUnknownConnectionType()
    {
        var registry = new DriverRegistry();

        var result = registry.GetByConnectionType(DeviceConnectionType.AxisVAPIX);

        Assert.Null(result);
    }

    [Fact]
    public void CreateCameraDriver_ReturnsNullWhenConnectionTypeIsNotRegistered()
    {
        var registry = new DriverRegistry();

        var driver = registry.CreateCameraDriver(DeviceConnectionType.AxisVAPIX);

        Assert.Null(driver);
    }

    [Fact]
    public void CreateCameraDriver_CreatesDriverFromRegisteredDescriptor()
    {
        var registry = new DriverRegistry();
        registry.Register(CreateDescriptor("test.driver", "Test Driver", DeviceConnectionType.RTSP));

        var driver = registry.CreateCameraDriver(DeviceConnectionType.RTSP);

        Assert.NotNull(driver);
        Assert.IsType<TestCameraDriver>(driver);
    }

    [Fact]
    public void CreateDefault_RegistersBuiltInDrivers()
    {
        var registry = DriverRegistry.CreateDefault();

        Assert.NotNull(registry.GetByDriverId("hikvision.isapi"));
        Assert.NotNull(registry.GetByDriverId("dahua.netsdk"));
        Assert.NotNull(registry.GetByDriverId("generic.onvif"));
        Assert.NotNull(registry.GetByDriverId("generic.rtsp"));
        Assert.Equal(4, registry.GetAll().Count);
    }

    [Fact]
    public void DriverFactory_UsesRegisteredDriverWhenAvailable()
    {
        var driver = DriverFactory.CreateCameraDriver(DeviceConnectionType.ONVIF);

        Assert.NotNull(driver);
        Assert.Equal("generic.onvif", driver.DriverId);
    }

    [Fact]
    public void DriverFactory_PreservesRtspFallbackForUnknownConnectionType()
    {
        var driver = DriverFactory.CreateCameraDriver(DeviceConnectionType.AxisVAPIX);

        Assert.IsType<RtspCameraDriver>(driver);
        Assert.Equal("generic.rtsp", driver.DriverId);
    }

    private static DriverDescriptor CreateDescriptor(
        string driverId,
        string displayName,
        DeviceConnectionType connectionType)
    {
        return new DriverDescriptor(driverId, displayName, connectionType, static () => new TestCameraDriver());
    }

    private sealed class TestCameraDriver : ICameraDriver
    {
        public string DriverId => "test.driver";

        public string DisplayName => "Test Driver";

        public DeviceCapability Capability { get; } = new();

        public bool TestConnection(EntityCamera camera) => false;

        public bool StartLive(EntityCamera camera) => false;

        public bool StopLive(EntityCamera camera) => false;

        public bool Snapshot(EntityCamera camera) => false;

        public DeviceInformation? GetDeviceInformation(EntityCamera camera) => null;
    }
}
