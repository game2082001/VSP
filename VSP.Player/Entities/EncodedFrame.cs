namespace VSP.Player.Entities;

/// <summary>
/// A neutral, VSP-owned encoded packet. No FFmpeg type (e.g. AVPacket) crosses this boundary.
/// </summary>
public sealed class EncodedFrame
{
    public required byte[] Data { get; init; }

    public required FrameTimestamp Timestamp { get; init; }

    public required bool IsKeyFrame { get; init; }
}
