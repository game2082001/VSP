using VSP.Device.Discovery.AutoDiscovery;
using VSP.Device.Drivers;
using VSP.Device.Drivers.Abstractions;
using VSP.Device.Drivers.Capabilities;
using VSP.Device.Drivers.Selection;
using VSP.Device.Drivers.Settings;
using VSP.Domain.Entities;
using VSP.Domain.Enums;
using Xunit;
using EntityCamera = VSP.Domain.Entities.Camera;

namespace VSP.Tests.Drivers;

public class DriverSelectionTests
{
    [Fact]
    public void AutoDiscoveryCandidateEvidenceMapper_MapsCandidateSummaryFields()
    {
        var mapper = new AutoDiscoveryCandidateEvidenceMapper();
        var candidate = new AutoDiscoveryCandidate
        {
            Sources = [AutoDiscoverySource.ONVIF, AutoDiscoverySource.RTSP],
            Host = "192.168.1.10",
            Port = 554,
            Endpoint = "rtsp://192.168.1.10/stream1",
            Manufacturer = "Contoso",
            Model = "Model A",
            Name = "Lobby Camera",
            ProbeClassification = "Success"
        };

        var evidence = mapper.Map(candidate);

        Assert.Contains(evidence.Evidence, item => item.Kind == DriverCompatibilityEvidenceKind.Host);
        Assert.Contains(evidence.Evidence, item => item.Kind == DriverCompatibilityEvidenceKind.Port);
        Assert.Contains(evidence.Evidence, item => item.Kind == DriverCompatibilityEvidenceKind.Endpoint);
        Assert.Contains(evidence.Evidence, item => item.Kind == DriverCompatibilityEvidenceKind.ManufacturerHint);
        Assert.Contains(evidence.Evidence, item => item.Kind == DriverCompatibilityEvidenceKind.ModelHint);
        Assert.Contains(evidence.Evidence, item => item.Kind == DriverCompatibilityEvidenceKind.OnvifDiscovery);
        Assert.Contains(evidence.Evidence, item => item.Kind == DriverCompatibilityEvidenceKind.RtspEndpointProbe);
    }

    [Fact]
    public void AutoDiscoveryCandidateEvidenceMapper_DoesNotTreatNetworkScanAsProtocolEvidence()
    {
        var mapper = new AutoDiscoveryCandidateEvidenceMapper();
        var candidate = new AutoDiscoveryCandidate
        {
            Sources = [AutoDiscoverySource.NetworkScan],
            Host = "192.168.1.10",
            Port = 554,
            Reachability = "Reachable"
        };

        var evidence = mapper.Map(candidate);

        Assert.Contains(evidence.Evidence, item => item.Kind == DriverCompatibilityEvidenceKind.Host);
        Assert.Contains(evidence.Evidence, item => item.Kind == DriverCompatibilityEvidenceKind.Port);
        Assert.DoesNotContain(evidence.Evidence, item => item.Kind == DriverCompatibilityEvidenceKind.OnvifDiscovery);
        Assert.DoesNotContain(evidence.Evidence, item => item.Kind == DriverCompatibilityEvidenceKind.RtspEndpointProbe);
    }

    [Fact]
    public void DriverSelectionService_ReturnsCompatibleWhenRequiredEvidenceIsSatisfied()
    {
        var service = new DriverSelectionService();
        var descriptor = CreateDescriptor(
            "generic.rtsp",
            DeviceConnectionType.RTSP,
            new DriverCompatibilityCapability(
                requiredEvidence:
                [
                    new DriverCompatibilityEvidenceRequirement(DriverCompatibilityEvidenceKind.RtspEndpointProbe)
                ],
                optionalEvidence:
                [
                    new DriverCompatibilityEvidenceRequirement(DriverCompatibilityEvidenceKind.Endpoint)
                ],
                requiredSettings:
                [
                    DriverSettingKey.RtspUrl
                ]));

        var result = service.Evaluate(new DriverSelectionRequest
        {
            Evidence = new DriverEvidenceCollection(
            [
                new DriverEvidence(DriverCompatibilityEvidenceKind.RtspEndpointProbe),
                new DriverEvidence(DriverCompatibilityEvidenceKind.Endpoint)
            ]),
            DriverDescriptors = [descriptor]
        });

        var compatibility = Assert.Single(result.CompatibilityResults);
        Assert.Equal(DriverCompatibilityStatus.Compatible, compatibility.Status);
        Assert.Contains(
            compatibility.RequiredEvidenceEvaluations,
            evaluation => evaluation.Kind == DriverCompatibilityEvidenceKind.RtspEndpointProbe
                && evaluation.Status == DriverEvidenceEvaluationStatus.Satisfied);
        Assert.Contains(
            compatibility.OptionalEvidenceEvaluations,
            evaluation => evaluation.Kind == DriverCompatibilityEvidenceKind.Endpoint
                && evaluation.Status == DriverEvidenceEvaluationStatus.Satisfied);
        Assert.Contains(DriverSettingKey.RtspUrl, compatibility.RequiredSettings);
        Assert.Contains(compatibility.MatchReasons, reason => reason.Code == "Compatible");
        Assert.Empty(compatibility.RejectionReasons);
    }

    [Fact]
    public void DriverSelectionService_MissingRequiredEvidenceMakesDriverIncompatible()
    {
        var service = new DriverSelectionService();
        var descriptor = CreateDescriptor(
            "generic.onvif",
            DeviceConnectionType.ONVIF,
            new DriverCompatibilityCapability(
                requiredEvidence:
                [
                    new DriverCompatibilityEvidenceRequirement(DriverCompatibilityEvidenceKind.OnvifDiscovery)
                ]));

        var result = service.Evaluate(new DriverSelectionRequest
        {
            Evidence = new DriverEvidenceCollection(),
            DriverDescriptors = [descriptor]
        });

        var compatibility = Assert.Single(result.CompatibilityResults);
        Assert.Equal(DriverCompatibilityStatus.InsufficientEvidence, compatibility.Status);
        Assert.Contains(
            compatibility.RequiredEvidenceEvaluations,
            evaluation => evaluation.Kind == DriverCompatibilityEvidenceKind.OnvifDiscovery
                && evaluation.Status == DriverEvidenceEvaluationStatus.Missing);
        Assert.Contains(compatibility.RejectionReasons, reason => reason.Code == "RequiredEvidenceMissing");
    }

    [Fact]
    public void DriverSelectionService_OptionalEvidenceDoesNotAffectCompatibility()
    {
        var service = new DriverSelectionService();
        var descriptor = CreateDescriptor(
            "generic.rtsp",
            DeviceConnectionType.RTSP,
            new DriverCompatibilityCapability(
                requiredEvidence:
                [
                    new DriverCompatibilityEvidenceRequirement(DriverCompatibilityEvidenceKind.RtspEndpointProbe)
                ],
                optionalEvidence:
                [
                    new DriverCompatibilityEvidenceRequirement(DriverCompatibilityEvidenceKind.Endpoint)
                ]));

        var result = service.Evaluate(new DriverSelectionRequest
        {
            Evidence = new DriverEvidenceCollection(
            [
                new DriverEvidence(DriverCompatibilityEvidenceKind.RtspEndpointProbe)
            ]),
            DriverDescriptors = [descriptor]
        });

        var compatibility = Assert.Single(result.CompatibilityResults);
        Assert.Equal(DriverCompatibilityStatus.Compatible, compatibility.Status);
        Assert.Contains(
            compatibility.OptionalEvidenceEvaluations,
            evaluation => evaluation.Kind == DriverCompatibilityEvidenceKind.Endpoint
                && evaluation.Status == DriverEvidenceEvaluationStatus.Missing);
        Assert.Empty(compatibility.RejectionReasons);
    }

    [Fact]
    public void DriverSelectionService_UnsupportedEvidenceConflictRejectsDriver()
    {
        var service = new DriverSelectionService();
        var descriptor = CreateDescriptor(
            "generic.rtsp",
            DeviceConnectionType.RTSP,
            new DriverCompatibilityCapability(
                requiredEvidence:
                [
                    new DriverCompatibilityEvidenceRequirement(DriverCompatibilityEvidenceKind.RtspEndpointProbe)
                ],
                unsupportedEvidence:
                [
                    new DriverCompatibilityEvidenceRequirement(DriverCompatibilityEvidenceKind.OnvifDiscovery)
                ]));

        var result = service.Evaluate(new DriverSelectionRequest
        {
            Evidence = new DriverEvidenceCollection(
            [
                new DriverEvidence(DriverCompatibilityEvidenceKind.RtspEndpointProbe),
                new DriverEvidence(DriverCompatibilityEvidenceKind.OnvifDiscovery)
            ]),
            DriverDescriptors = [descriptor]
        });

        var compatibility = Assert.Single(result.CompatibilityResults);
        Assert.Equal(DriverCompatibilityStatus.Rejected, compatibility.Status);
        Assert.Contains(
            compatibility.UnsupportedEvidenceEvaluations,
            evaluation => evaluation.Kind == DriverCompatibilityEvidenceKind.OnvifDiscovery
                && evaluation.Status == DriverEvidenceEvaluationStatus.Conflict);
        Assert.Contains(compatibility.RejectionReasons, reason => reason.Code == "UnsupportedEvidenceConflict");
    }

    [Fact]
    public void DriverSelectionService_ReturnsAllCompatibleDriversWithoutRanking()
    {
        var service = new DriverSelectionService();
        var firstDescriptor = CreateDescriptor(
            "first.driver",
            DeviceConnectionType.RTSP,
            new DriverCompatibilityCapability(
                requiredEvidence:
                [
                    new DriverCompatibilityEvidenceRequirement(DriverCompatibilityEvidenceKind.Endpoint)
                ]));
        var secondDescriptor = CreateDescriptor(
            "second.driver",
            DeviceConnectionType.ONVIF,
            new DriverCompatibilityCapability(
                requiredEvidence:
                [
                    new DriverCompatibilityEvidenceRequirement(DriverCompatibilityEvidenceKind.Endpoint)
                ]));

        var result = service.Evaluate(new DriverSelectionRequest
        {
            Evidence = new DriverEvidenceCollection(
            [
                new DriverEvidence(DriverCompatibilityEvidenceKind.Endpoint)
            ]),
            DriverDescriptors = [firstDescriptor, secondDescriptor]
        });

        Assert.Equal(2, result.CompatibilityResults.Count);
        Assert.All(result.CompatibilityResults, compatibility =>
            Assert.Equal(DriverCompatibilityStatus.Compatible, compatibility.Status));
        Assert.Equal(["first.driver", "second.driver"], result.CompatibilityResults.Select(item => item.DriverId));
    }

    [Fact]
    public void DriverSelectionService_NoMatchReturnsExplainableResult()
    {
        var service = new DriverSelectionService();
        var descriptor = CreateDescriptor(
            "generic.onvif",
            DeviceConnectionType.ONVIF,
            new DriverCompatibilityCapability(
                requiredEvidence:
                [
                    new DriverCompatibilityEvidenceRequirement(DriverCompatibilityEvidenceKind.OnvifDiscovery)
                ]));

        var result = service.Evaluate(new DriverSelectionRequest
        {
            Evidence = new DriverEvidenceCollection(
            [
                new DriverEvidence(DriverCompatibilityEvidenceKind.RtspEndpointProbe)
            ]),
            DriverDescriptors = [descriptor]
        });

        var compatibility = Assert.Single(result.CompatibilityResults);
        Assert.Equal(DriverCompatibilityStatus.InsufficientEvidence, compatibility.Status);
        Assert.Contains(result.Reasons, reason => reason.Code == "NoCompatibleDriver");
        Assert.Contains(compatibility.RejectionReasons, reason => reason.Code == "RequiredEvidenceMissing");
    }

    [Fact]
    public void DriverSelectionService_DoesNotInvokeDriverFactory()
    {
        var factoryCallCount = 0;
        var descriptor = new DriverDescriptor(
            "test.driver",
            "Test Driver",
            DeviceConnectionType.RTSP,
            () =>
            {
                factoryCallCount++;
                return new TestCameraDriver();
            },
            compatibilityCapability: new DriverCompatibilityCapability(
                requiredEvidence:
                [
                    new DriverCompatibilityEvidenceRequirement(DriverCompatibilityEvidenceKind.RtspEndpointProbe)
                ]));
        var service = new DriverSelectionService();

        service.Evaluate(new DriverSelectionRequest
        {
            Evidence = new DriverEvidenceCollection(
            [
                new DriverEvidence(DriverCompatibilityEvidenceKind.RtspEndpointProbe)
            ]),
            DriverDescriptors = [descriptor]
        });

        Assert.Equal(0, factoryCallCount);
    }

    private static DriverDescriptor CreateDescriptor(
        string driverId,
        DeviceConnectionType connectionType,
        DriverCompatibilityCapability compatibilityCapability)
    {
        return new DriverDescriptor(
            driverId,
            driverId,
            connectionType,
            static () => new TestCameraDriver(),
            compatibilityCapability: compatibilityCapability);
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
