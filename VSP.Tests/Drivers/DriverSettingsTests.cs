using VSP.Device.Drivers;
using VSP.Device.Drivers.Plugins;
using VSP.Device.Drivers.Settings;
using VSP.Domain.Enums;
using Xunit;

namespace VSP.Tests.Drivers;

public class DriverSettingsTests
{
    [Fact]
    public void DriverSettingDefinition_RequiresNonEmptyDisplayName()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new DriverSettingDefinition(DriverSettingKey.HttpPort, ""));

        Assert.Equal("displayName", exception.ParamName);
    }

    [Fact]
    public void DriverSettingsDefinition_CopiesIncomingCollection()
    {
        var source = new List<DriverSettingDefinition>
        {
            new(DriverSettingKey.HttpPort, "HTTP Port", defaultValue: 80)
        };

        var definition = new DriverSettingsDefinition(source);
        source.Add(new DriverSettingDefinition(DriverSettingKey.RtspPort, "RTSP Port", defaultValue: 554));

        Assert.Single(definition.Settings);
    }

    [Fact]
    public void DriverSettingsDefinition_RejectsNullEntries()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DriverSettingsDefinition(
                new DriverSettingDefinition?[]
                {
                    new(DriverSettingKey.HttpPort, "HTTP Port"),
                    null
                }!));

        Assert.Equal("Driver settings must not contain null entries.", exception.Message);
    }

    [Fact]
    public void DriverDescriptor_CanCarrySettingsDefinition()
    {
        var settingsDefinition = new DriverSettingsDefinition(
        [
            new DriverSettingDefinition(DriverSettingKey.HttpPort, "HTTP Port", defaultValue: 80)
        ]);

        var descriptor = new DriverDescriptor(
            "test.driver",
            "Test Driver",
            DeviceConnectionType.RTSP,
            static () => new TestCameraDriver(),
            settingsDefinition);

        Assert.Same(settingsDefinition, descriptor.SettingsDefinition);
    }

    [Fact]
    public void BuiltInHikvisionDriver_ExposesConservativeSettingsDefinition()
    {
        var descriptor = GetBuiltInDescriptor("hikvision.isapi");

        AssertSettings(
            descriptor.SettingsDefinition,
            (DriverSettingKey.HttpPort, "HTTP Port", false, 80, false),
            (DriverSettingKey.SdkPort, "SDK Port", false, 8000, false),
            (DriverSettingKey.Username, "Username", false, null, false),
            (DriverSettingKey.Password, "Password", false, null, true));
    }

    [Fact]
    public void BuiltInDahuaDriver_ExposesConservativeSettingsDefinition()
    {
        var descriptor = GetBuiltInDescriptor("dahua.netsdk");

        AssertSettings(
            descriptor.SettingsDefinition,
            (DriverSettingKey.HttpPort, "HTTP Port", false, 80, false),
            (DriverSettingKey.SdkPort, "SDK Port", false, 8000, false),
            (DriverSettingKey.Username, "Username", false, null, false),
            (DriverSettingKey.Password, "Password", false, null, true));
    }

    [Fact]
    public void BuiltInOnvifDriver_ExposesConservativeSettingsDefinition()
    {
        var descriptor = GetBuiltInDescriptor("generic.onvif");

        AssertSettings(
            descriptor.SettingsDefinition,
            (DriverSettingKey.HttpPort, "HTTP Port", false, 80, false),
            (DriverSettingKey.Username, "Username", false, null, false),
            (DriverSettingKey.Password, "Password", false, null, true));
    }

    [Fact]
    public void BuiltInRtspDriver_RequiresOnlyRtspUrl()
    {
        var descriptor = GetBuiltInDescriptor("generic.rtsp");

        AssertSettings(
            descriptor.SettingsDefinition,
            (DriverSettingKey.RtspPort, "RTSP Port", false, 554, false),
            (DriverSettingKey.Username, "Username", false, null, false),
            (DriverSettingKey.Password, "Password", false, null, true),
            (DriverSettingKey.RtspUrl, "RTSP URL", true, string.Empty, false));
    }

    private static DriverDescriptor GetBuiltInDescriptor(string driverId)
    {
        var plugin = new BuiltInCameraDriverPlugin();

        return Assert.Single(plugin.GetDriverDescriptors(), descriptor => descriptor.DriverId == driverId);
    }

    private static void AssertSettings(
        DriverSettingsDefinition? definition,
        params (DriverSettingKey Key, string DisplayName, bool IsRequired, object? DefaultValue, bool IsSensitive)[] expected)
    {
        Assert.NotNull(definition);
        Assert.Equal(expected.Length, definition!.Settings.Count);

        for (var i = 0; i < expected.Length; i++)
        {
            var actual = definition.Settings[i];
            var item = expected[i];

            Assert.Equal(item.Key, actual.Key);
            Assert.Equal(item.DisplayName, actual.DisplayName);
            Assert.Equal(item.IsRequired, actual.IsRequired);
            Assert.Equal(item.DefaultValue, actual.DefaultValue);
            Assert.Equal(item.IsSensitive, actual.IsSensitive);
        }
    }

    private sealed class TestCameraDriver : VSP.Device.Drivers.Abstractions.ICameraDriver
    {
        public string DriverId => "test.driver";

        public string DisplayName => "Test Driver";

        public VSP.Domain.Entities.DeviceCapability Capability { get; } = new();

        public bool TestConnection(VSP.Domain.Entities.Camera camera) => false;

        public bool StartLive(VSP.Domain.Entities.Camera camera) => false;

        public bool StopLive(VSP.Domain.Entities.Camera camera) => false;

        public bool Snapshot(VSP.Domain.Entities.Camera camera) => false;
    }
}
