namespace VSP.Player.Entities;

/// <summary>
/// A neutral, VSP-owned decoded frame. No FFmpeg type (e.g. AVFrame) crosses this boundary.
/// Represents either CPU-resident pixel data (the only case the v1 software decoder produces)
/// or a GPU-resident handle (reserved for a future hardware-accelerated decoder), without the
/// consumer needing to know which.
/// </summary>
public sealed class DecodedFrame : IDisposable
{
    public required FrameStorage Storage { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    public required FramePixelFormat PixelFormat { get; init; }

    public required FrameTimestamp Timestamp { get; init; }

    /// <summary>Populated when <see cref="Storage"/> is <see cref="FrameStorage.Cpu"/>.</summary>
    public byte[]? PixelBuffer { get; init; }

    /// <summary>Row stride in bytes. Populated when <see cref="Storage"/> is <see cref="FrameStorage.Cpu"/>.</summary>
    public int Stride { get; init; }

    /// <summary>Populated when <see cref="Storage"/> is <see cref="FrameStorage.Gpu"/>. Never set by the v1 decoder.</summary>
    public IGpuFrameHandle? GpuHandle { get; init; }

    public void Dispose()
    {
        GpuHandle?.Dispose();
    }
}
