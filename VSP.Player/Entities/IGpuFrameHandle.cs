namespace VSP.Player.Entities;

/// <summary>
/// Opaque marker for a GPU-resident decoded frame. Concrete handles (a D3D11 texture, a DXGI
/// surface, etc.) are defined by whichever future hardware-accelerated decoder implementation
/// produces them — this contract intentionally carries no library-specific type, so
/// <see cref="DecodedFrame"/> does not need to change when hardware decode is implemented.
/// Not produced by the v1 software decoder.
/// </summary>
public interface IGpuFrameHandle : IDisposable
{
}
