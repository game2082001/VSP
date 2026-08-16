namespace VSP.Player.Decoder;

/// <summary>
/// SEC-3 (Task 3A): pure, native-free decision logic for whether an encoded packet's reported
/// size is safe to allocate a managed copy of, and whether a run of rejections is long enough to
/// treat as a genuine stream failure rather than an isolated bad packet. Kept as plain <c>int</c>
/// in/out (no FFmpeg-adjacent type) so it is directly unit-testable without a native session,
/// mirroring the existing <c>FfmpegVideoDecoder.IsDiagnosticsIntervalElapsed</c>/
/// <c>SampleBufferHasNonZeroContent</c> pattern of extracting decision logic into pure statics.
///
/// Bound rationale (64 MiB): sized for the worst realistic single-frame case among supported
/// codecs -- MJPEG, since every frame is independently intra-coded with no inter-frame
/// prediction, making it larger per frame than an equivalently-dimensioned H.264/H.265 keyframe.
/// At a deliberately generous 8K UHD ceiling (7680x4320 = 33,177,600 px, well above the actual
/// IP-camera/NVR market, which is overwhelmingly 4K/8MP or below), the uncompressed 24-bit
/// reference size is ~99.5 MB; even a pessimistic 2:1 JPEG compression floor (real MJPEG
/// typically achieves far better) yields ~49.75 MB for a single worst-case 8K frame. 64 MiB
/// (67,108,864 bytes) adds ~30% headroom above that pessimistic floor -- roughly 30x a typical
/// real-world 4K MJPEG keyframe (~2 MB) -- while remaining orders of magnitude below the packet
/// sizes that would actually threaten process memory.
///
/// Escalation threshold (5 consecutive rejections): a single transient/corrupted packet is
/// dropped and the stream continues uninterrupted (Task 3A's required A/B distinction -- see
/// <see cref="Decision"/>); a short but unbroken run of rejections is treated as a genuine,
/// non-transient failure warranting a full reconnect cycle instead of silently discarding data
/// indefinitely. Any single accepted packet resets the streak to zero.
/// </summary>
internal static class EncodedPacketGuard
{
    public const int MaxEncodedPacketSizeBytes = 64 * 1024 * 1024;

    public const int MaxConsecutiveRejectedPackets = 5;

    /// <summary>
    /// True for any size in [0, MaxEncodedPacketSizeBytes]. A single int comparison against a
    /// fixed constant well within int range -- no multiplication of two variable values occurs
    /// here, so there is no integer-overflow surface in this specific check (unlike a
    /// width*height frame-dimension check, which is Task 3B's separate scope).
    /// </summary>
    public static bool IsPacketSizeAcceptable(int packetSize) =>
        packetSize >= 0 && packetSize <= MaxEncodedPacketSizeBytes;

    public static bool ShouldEscalateToFault(int consecutiveRejectedCount) =>
        consecutiveRejectedCount >= MaxConsecutiveRejectedPackets;
}

/// <summary>
/// The three-way outcome of evaluating one encoded packet's size (SEC-3 Task 3A), shared by
/// <see cref="RtspMediaSession"/> and <see cref="RecordedFileMediaSession"/>. Deliberately three
/// states, not a bool: Drop and Fault both skip the managed allocation, but only Fault should end
/// an otherwise-healthy session.
/// </summary>
internal enum PacketSizeDecision
{
    /// <summary>Size is within bounds; proceed with the normal allocate-and-dispatch path.</summary>
    Accept,

    /// <summary>Size is out of bounds but the rejection streak has not reached the escalation
    /// threshold; skip this packet only, keep the session open, keep reading.</summary>
    Drop,

    /// <summary>The rejection streak reached the escalation threshold; treat as a genuine
    /// processing failure and fault the session via the existing Faulted/reconnect path.</summary>
    Fault
}
