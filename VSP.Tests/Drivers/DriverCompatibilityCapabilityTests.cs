using VSP.Device.Drivers;
using VSP.Device.Drivers.Abstractions;
using VSP.Device.Drivers.Capabilities;
using VSP.Device.Drivers.Plugins;
using VSP.Device.Drivers.Settings;
using VSP.Domain.Entities;
using VSP.Domain.Enums;
using Xunit;
using EntityCamera = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Drivers;

public class DriverCompatibilityCapabilityTests
{
    [Fact]
    public void DriverCompatibilityCapability_CopiesIncomingCollections()
    {
        var requiredEvidence = new List<DriverCompatibilityEvidenceRequirement>
        {
            new(DriverCompatibilityEvidenceKind.RtspEndpointProbe)
        };

        var capability = new DriverCompatibilityCapability(requiredEvidence: requiredEvidence);
        requiredEvidence.Add(new DriverCompatibilityEvidenceRequirement(DriverCompatibilityEvidenceKind.Host));

        Assert.Single(capability.RequiredEvidence);
    }

    [Fact]
    public void DriverCompatibilityCapability_RejectsNullEvidenceEntries()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new DriverCompatibilityCapability(
                requiredEvidence:
                [
                    new DriverCompatibilityEvidenceRequirement(DriverCompatibilityEvidenceKind.Endpoint),
                    null!
                ]));

        Assert.Equal("requiredEvidence must not contain null entries.", exception.Message);
    }

    [Fact]
    public void DriverCompatibilityEvidenceRequirement_RejectsEmptyQualifier()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new DriverCompatibilityEvidenceRequirement(DriverCompatibilityEvidenceKind.VendorHint, ""));

        Assert.Equal("qualifier", exception.ParamName);
    }

    [Fact]
    public void DriverCompatibilityEvidenceRequirement_AllowsExtensibleVendorQualifier()
    {
        var requirement = new DriverCompatibilityEvidenceRequirement(
            DriverCompatibilityEvidenceKind.VendorHint,
            "vendor:any");

        Assert.Equal(DriverCompatibilityEvidenceKind.VendorHint, requirement.Kind);
        Assert.Equal("vendor:any", requirement.Qualifier);
    }

    [Fact]
    public void DriverDescriptor_CanCarryCompatibilityCapability()
    {
        var capability = new DriverCompatibilityCapability(
            requiredEvidence:
            [
                new DriverCompatibilityEvidenceRequirement(DriverCompatibilityEvidenceKind.OnvifDiscovery)
            ],
            requiredSettings:
            [
                DriverSettingKey.HttpPort
            ]);

        var descriptor = new DriverDescriptor(
            "test.driver",
            "Test Driver",
            DeviceConnectionType.ONVIF,
            static () => new TestCameraDriver(),
            compatibilityCapability: capability);

        Assert.Same(capability, descriptor.CompatibilityCapability);
    }

    [Fact]
    public void BuiltInOnvifDriver_ExposesMinimalCompatibilityCapability()
    {
        var descriptor = GetBuiltInDescriptor("generic.onvif");

        Assert.NotNull(descriptor.CompatibilityCapability);
        Assert.Contains(
            descriptor.CompatibilityCapability!.RequiredEvidence,
            requirement => requirement.Kind == DriverCompatibilityEvidenceKind.OnvifDiscovery);
        Assert.Contains(DriverSettingKey.HttpPort, descriptor.CompatibilityCapability.RequiredSettings);
    }

    [Fact]
    public void BuiltInRtspDriver_ExposesMinimalCompatibilityCapability()
    {
        var descriptor = GetBuiltInDescriptor("generic.rtsp");

        Assert.NotNull(descriptor.CompatibilityCapability);
        Assert.Contains(
            descriptor.CompatibilityCapability!.RequiredEvidence,
            requirement => requirement.Kind == DriverCompatibilityEvidenceKind.RtspEndpointProbe);
        Assert.Contains(DriverSettingKey.RtspUrl, descriptor.CompatibilityCapability.RequiredSettings);
    }

    private static DriverDescriptor GetBuiltInDescriptor(string driverId)
    {
        var plugin = new BuiltInCameraDriverPlugin();

        return Assert.Single(plugin.GetDriverDescriptors(), descriptor => descriptor.DriverId == driverId);
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
