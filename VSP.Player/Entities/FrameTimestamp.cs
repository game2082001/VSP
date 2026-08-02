namespace VSP.Player.Entities;

/// <summary>
/// DTS (decode order) and PTS (presentation order) diverge whenever B-frames are present,
/// which is routine in H.264/H.265.
/// </summary>
public readonly struct FrameTimestamp
{
    public required TimeSpan DecodeTimestamp { get; init; }

    public required TimeSpan PresentationTimestamp { get; init; }
}
