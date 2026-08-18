using VSP.Core.Logging;
using VSP.Player.Entities;

namespace VSP.Player.Decoder;

/// <summary>
/// SEC-3 (Task 3A): shared consecutive-rejection streak, rate-limited logging, and fault decision
/// for an encoded packet's size, extracted from <see cref="RtspMediaSession"/> and
/// <see cref="RecordedFileMediaSession"/> (Ultra Review finding on commit 683d8a0 -- the two
/// classes carried byte-for-byte identical tracking state/logic). See <see cref="EncodedPacketGuard"/>
/// for the bound/threshold rationale this tracker applies.
///
/// Only ever touched from within its owning session's single ReadLoop thread (production) or by a
/// focused test driving a freshly-constructed, never-opened session directly -- an instance never
/// needs its own lock, exactly as the fields it replaces never did.
/// </summary>
internal sealed class PacketRejectionTracker
{
    private const long RejectedPacketLogIntervalMilliseconds = 5 * 60 * 1000;

    private readonly string _logContextPrefix;

    private int _consecutiveRejectedPackets;
    private long _lastRejectedPacketLogTickCount = long.MinValue;

    public PacketRejectionTracker(string logContextPrefix)
    {
        _logContextPrefix = logContextPrefix;
    }

    /// <summary>
    /// The pure EncodedPacketGuard decision, applied to and tracked against this instance's
    /// consecutive-rejection streak. faultError is non-null if and only if the decision is Fault.
    /// </summary>
    public PacketSizeDecision EvaluateAndTrack(int packetSize, out MediaError? faultError)
    {
        faultError = null;

        if (EncodedPacketGuard.IsPacketSizeAcceptable(packetSize))
        {
            _consecutiveRejectedPackets = 0;
            return PacketSizeDecision.Accept;
        }

        _consecutiveRejectedPackets++;
        LogRejectedPacketIfDue(packetSize, _consecutiveRejectedPackets);

        if (!EncodedPacketGuard.ShouldEscalateToFault(_consecutiveRejectedPackets))
        {
            return PacketSizeDecision.Drop;
        }

        faultError = new MediaError
        {
            Category = MediaErrorCategory.Protocol,
            Message = $"Rejected {_consecutiveRejectedPackets} consecutive encoded packets outside the accepted " +
                      $"size bound (0..{EncodedPacketGuard.MaxEncodedPacketSizeBytes} bytes); treating the session as faulted."
        };
        return PacketSizeDecision.Fault;
    }

    /// <summary>
    /// Rate-limited (never per-packet-flood) diagnostic for rejected encoded packets: always logs
    /// the first rejection and the escalating one, otherwise at most once per
    /// RejectedPacketLogIntervalMilliseconds -- closing the gap an attacker alternating one good
    /// packet with several bad ones would otherwise exploit to log unboundedly while never
    /// reaching the consecutive-rejection escalation threshold. Never logs a credential or URI --
    /// only the packet size and rejection count.
    /// </summary>
    private void LogRejectedPacketIfDue(int size, int consecutiveCount)
    {
        var now = Environment.TickCount64;
        var mustLog = _lastRejectedPacketLogTickCount == long.MinValue
            || FfmpegVideoDecoder.IsDiagnosticsIntervalElapsed(_lastRejectedPacketLogTickCount, now, RejectedPacketLogIntervalMilliseconds)
            || EncodedPacketGuard.ShouldEscalateToFault(consecutiveCount);

        if (!mustLog)
        {
            return;
        }

        _lastRejectedPacketLogTickCount = now;
        AppLog.Warning(
            $"{_logContextPrefix}: rejected an encoded packet with size={size} bytes (consecutive rejections={consecutiveCount}); " +
            $"accepted range is 0..{EncodedPacketGuard.MaxEncodedPacketSizeBytes} bytes.");
    }
}
