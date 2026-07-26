using VSP.Device.Cameras.Factory;
using VSP.Device.Discovery.AutoDiscovery;
using VSP.Device.Discovery.NetworkScan;
using VSP.Device.Discovery.Onvif;
using VSP.Device.Discovery.Orchestration;
using VSP.Device.Discovery.Rtsp;
using VSP.Device.Drivers;
using VSP.Device.Drivers.Selection;
using VSP.Device.Interfaces;
using VSP.Device.Registration;

namespace VSP.Device.Discovery.Workspace;

/// <summary>
/// Hand-wires <see cref="DiscoveryOrchestrator"/> for real use, following the same manual
/// composition pattern used elsewhere in the app (see <see cref="DriverFactory"/>). No DI
/// container is introduced. <see cref="DiscoveryOrchestrator"/> itself remains the single
/// orchestration pipeline for both fully-automatic (<c>ExecuteAsync</c>) and interactive,
/// two-phase (<c>DiscoverCandidatesAsync</c> / <c>RegisterCandidate</c>) use.
/// </summary>
public static class CameraDiscoveryOrchestratorFactory
{
    public static DiscoveryOrchestrator CreateDefault(ICameraRepository cameraRepository)
    {
        ArgumentNullException.ThrowIfNull(cameraRepository);

        var coordinator = new AutoDiscoveryCoordinator(
            new OnvifDiscoveryService(new UdpWsDiscoveryTransport()),
            new RtspEndpointProbeService(new TcpRtspProbeTransport()),
            new NetworkScanService(new TcpNetworkReachabilityProbe()));

        return new DiscoveryOrchestrator(
            coordinator,
            new AutoDiscoveryCandidateEvidenceMapper(),
            new DriverSelectionService(),
            DriverRegistry.CreateDefault(),
            new AutoApproveSingleCompatiblePolicy(),
            new CameraFactory(),
            new DeviceRegistrationService(cameraRepository));
    }
}
