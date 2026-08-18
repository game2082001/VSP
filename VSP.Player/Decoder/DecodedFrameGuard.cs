namespace VSP.Player.Decoder;

/// <summary>
/// SEC-3 (Task 3B): pure, native-free decision/calculation for whether a decoded frame's
/// reported dimensions are safe to size a managed BGRA buffer from. Kept as plain <c>int</c> in,
/// <c>long</c> out (no FFmpeg-adjacent type) so it is directly unit-testable without a native
/// decode session, mirroring EncodedPacketGuard's (Task 3A) pattern of extracting decision logic
/// into pure statics -- and, further back, FfmpegVideoDecoder's own existing
/// <c>IsDiagnosticsIntervalElapsed</c>/<c>SampleBufferHasNonZeroContent</c> precedent.
///
/// Distinct from Task 3A's 64 MiB encoded-packet ceiling on purpose: encoded (compressed) packet
/// size and decoded (raw) BGRA frame size are different concepts with different derivations. A
/// decoded BGRA frame's byte count is an *exact, deterministic* function of width and height
/// (width * height * 4 -- no compression-ratio estimation or uncertainty is involved at all,
/// unlike Task 3A's compressed-packet estimate), so this bound can be derived directly rather
/// than estimated.
///
/// Bound rationale: MaxDecodedWidth/MaxDecodedHeight are set to exactly 8K UHD (7680x4320) --
/// the largest resolution VSP's current stated scope needs to support (see the encoded-packet
/// bound's identical reference point in EncodedPacketGuard), anchored to a real, industry-
/// standard resolution rather than an arbitrary round number. MaxDecodedBgraBytes is the exact,
/// deterministic byte cost at that ceiling: 7680 * 4320 * 4 = 132,710,400 bytes (~126.56 MiB).
/// This is a conservative operational safety ceiling used to bound managed allocation while
/// remaining substantially above expected surveillance-camera decoded frame sizes -- it is NOT a
/// claimed codec/hardware theorem, and should be revisited if VSP ever needs to support a
/// resolution beyond 8K.
///
/// Root cause this closes: the original code computed <c>stride = width * 4</c> and
/// <c>buffer = new byte[stride * height]</c> using unchecked <c>int</c> arithmetic on a
/// native-reported, stream-controlled width/height. For sufficiently large adversarial values,
/// <c>width * 4</c> alone can overflow Int32 (e.g. width &gt;= 536,870,912) and wrap to a small
/// or negative value; if the resulting (wrapped) stride were then used to size an undersized
/// managed buffer while the native swscale conversion still writes data sized by the *true*
/// width/height, this is a credible mechanism for a native heap buffer overflow -- not merely an
/// oversized-allocation/DoS concern (that class of risk is Task 3A's framing; this is a distinct
/// and more severe mechanism specific to the decoded-frame path). Evaluating width/height with
/// checked <c>long</c> arithmetic, and rejecting anything outside the ceiling *before* the
/// existing int-based stride/allocation code ever runs, closes this: by the time the original
/// code path executes, width/height are already guaranteed within [1, 7680] x [1, 4320], values
/// far too small for int-multiplication to ever overflow.
/// </summary>
internal static class DecodedFrameGuard
{
    /// <summary>8K UHD width -- the largest resolution VSP's current stated scope needs to support.</summary>
    public const int MaxDecodedWidth = 7680;

    /// <summary>8K UHD height.</summary>
    public const int MaxDecodedHeight = 4320;

    /// <summary>
    /// The exact BGRA byte cost of a MaxDecodedWidth x MaxDecodedHeight frame (132,710,400 bytes,
    /// ~126.56 MiB) -- deterministic, not estimated, since raw pixel data has no compression-ratio
    /// uncertainty. Computed as a checked long product to make the derivation itself verifiable
    /// rather than merely asserted.
    /// </summary>
    public const long MaxDecodedBgraBytes = checked((long)MaxDecodedWidth * MaxDecodedHeight * 4);

    /// <summary>
    /// True (with <paramref name="bgraByteCount"/> set to the exact BGRA byte count) if and only
    /// if both dimensions are positive and within [1, MaxDecodedWidth] x [1, MaxDecodedHeight].
    /// Never throws regardless of input, including <see cref="int.MaxValue"/> for both
    /// dimensions simultaneously -- the dimension-cap check runs, and rejects, before any
    /// multiplication is attempted, so the checked-arithmetic block below can never actually
    /// overflow in practice; it exists as an explicit, tested belt-and-suspenders guard against
    /// ever silently relying on wrapping behavior, per the "do not rely on int overflow
    /// wrapping" requirement, rather than because it is reachable given the caps above.
    /// </summary>
    public static bool TryEvaluate(int width, int height, out long bgraByteCount)
    {
        bgraByteCount = 0;

        if (width <= 0 || height <= 0)
        {
            return false;
        }

        if (width > MaxDecodedWidth || height > MaxDecodedHeight)
        {
            return false;
        }

        try
        {
            checked
            {
                long stride = (long)width * 4;
                bgraByteCount = stride * height;
            }
        }
        catch (OverflowException)
        {
            bgraByteCount = 0;
            return false;
        }

        return bgraByteCount <= MaxDecodedBgraBytes;
    }
}

/// <summary>
/// SEC-3 (Task 3B): thrown by FfmpegVideoDecoder when a decoded frame's dimensions are rejected
/// by DecodedFrameGuard, before any managed allocation or swscale destination use is attempted.
/// Deliberately a distinct, named type (rather than reusing the sibling "failed to create swscale
/// conversion context" InvalidOperationException) so a caller/test can distinguish "this frame's
/// dimensions were rejected" from other decode failures, while still being an
/// InvalidOperationException so it is caught by MediaController.HandlePacketReceived's existing
/// generic `catch (Exception)` exactly like every other decode-path failure already is -- no
/// change to that existing lifecycle/error-handling shape was needed or made.
/// </summary>
internal sealed class DecodedFrameDimensionException : InvalidOperationException
{
    public DecodedFrameDimensionException(string message) : base(message)
    {
    }
}
