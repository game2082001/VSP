using VSP.Device.Drivers;
using VSP.Device.Drivers.Abstractions;
using VSP.Device.Drivers.Plugins;
using VSP.Domain.Entities;
using VSP.Domain.Enums;
using Xunit;
using EntityCamera = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Drivers;

public class DriverPluginTests
{
    [Fact]
    public void RegisterPlugin_RegistersAllPluginDescriptors()
    {
        var registry = new DriverRegistry();

        registry.RegisterPlugin(new TestDriverPlugin(
            "test.plugin",
            "Test Plugin",
            CreateDescriptor("test.rtsp", "RTSP Driver", DeviceConnectionType.RTSP),
            CreateDescriptor("test.onvif", "ONVIF Driver", DeviceConnectionType.ONVIF)));

        Assert.Equal(2, registry.GetAll().Count);
        Assert.NotNull(registry.GetByDriverId("test.rtsp"));
        Assert.NotNull(registry.GetByDriverId("test.onvif"));
    }

    [Fact]
    public void RegisterPlugin_IsAtomicWhenPluginContainsDuplicateDriverId()
    {
        var registry = new DriverRegistry();
        registry.Register(CreateDescriptor("existing.driver", "Existing Driver", DeviceConnectionType.AxisVAPIX));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterPlugin(new TestDriverPlugin(
                "test.plugin",
                "Test Plugin",
                CreateDescriptor("test.driver", "First Driver", DeviceConnectionType.RTSP),
                CreateDescriptor("test.driver", "Second Driver", DeviceConnectionType.ONVIF))));

        Assert.Equal("Plugin 'test.plugin' contains duplicate DriverId 'test.driver'.", exception.Message);
        Assert.Single(registry.GetAll());
        Assert.Null(registry.GetByDriverId("test.driver"));
    }

    [Fact]
    public void RegisterPlugin_IsAtomicWhenPluginContainsDuplicateConnectionType()
    {
        var registry = new DriverRegistry();
        registry.Register(CreateDescriptor("existing.driver", "Existing Driver", DeviceConnectionType.AxisVAPIX));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterPlugin(new TestDriverPlugin(
                "test.plugin",
                "Test Plugin",
                CreateDescriptor("first.driver", "First Driver", DeviceConnectionType.RTSP),
                CreateDescriptor("second.driver", "Second Driver", DeviceConnectionType.RTSP))));

        Assert.Equal("Plugin 'test.plugin' contains duplicate DeviceConnectionType 'RTSP'.", exception.Message);
        Assert.Single(registry.GetAll());
        Assert.Null(registry.GetByDriverId("first.driver"));
        Assert.Null(registry.GetByDriverId("second.driver"));
    }

    [Fact]
    public void RegisterPlugin_IsAtomicWhenRegistryConflictOccurs()
    {
        var registry = new DriverRegistry();
        registry.Register(CreateDescriptor("existing.driver", "Existing Driver", DeviceConnectionType.AxisVAPIX));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            registry.RegisterPlugin(new TestDriverPlugin(
                "test.plugin",
                "Test Plugin",
                CreateDescriptor("new.driver", "New Driver", DeviceConnectionType.RTSP),
                CreateDescriptor("existing.driver", "Conflicting Driver", DeviceConnectionType.ONVIF))));

        Assert.Equal("DriverId 'existing.driver' is already registered.", exception.Message);
        Assert.Single(registry.GetAll());
        Assert.Null(registry.GetByDriverId("new.driver"));
    }

    [Fact]
    public void CreateDefault_RegistersBuiltInPluginDescriptors()
    {
        var plugin = new BuiltInCameraDriverPlugin();
        var registry = DriverRegistry.CreateDefault();

        Assert.Equal(4, plugin.GetDriverDescriptors().Count);
        Assert.Equal(4, registry.GetAll().Count);
        Assert.NotNull(registry.GetByDriverId("hikvision.isapi"));
        Assert.NotNull(registry.GetByDriverId("dahua.netsdk"));
        Assert.NotNull(registry.GetByDriverId("generic.onvif"));
        Assert.NotNull(registry.GetByDriverId("generic.rtsp"));
    }

    private static DriverDescriptor CreateDescriptor(
        string driverId,
        string displayName,
        DeviceConnectionType connectionType)
    {
        return new DriverDescriptor(driverId, displayName, connectionType, static () => new TestCameraDriver());
    }

    private sealed class TestDriverPlugin(
        string pluginId,
        string displayName,
        params DriverDescriptor[] descriptors) : IDriverPlugin
    {
        public string PluginId => pluginId;

        public string DisplayName => displayName;

        public IReadOnlyList<DriverDescriptor> GetDriverDescriptors()
        {
            return descriptors;
        }
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
    }
}
