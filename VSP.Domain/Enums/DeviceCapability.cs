namespace VSP.Domain.Entities;

public class DeviceCapability
{
    public bool SupportsLiveView { get; set; }
    public bool SupportsPlayback { get; set; }
    public bool SupportsPTZ { get; set; }
    public bool SupportsAudio { get; set; }
    public bool SupportsEvent { get; set; }
    public bool SupportsSnapshot { get; set; }
    public bool SupportsDiscovery { get; set; }
}