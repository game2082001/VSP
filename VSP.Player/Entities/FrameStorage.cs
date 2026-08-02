namespace VSP.Player.Entities;

/// <summary>
/// Distinguishes CPU-resident decoded frame data from a future GPU-resident representation,
/// so consuming code never needs to know which decoder implementation produced a frame.
/// </summary>
public enum FrameStorage
{
    Cpu,
    Gpu
}
