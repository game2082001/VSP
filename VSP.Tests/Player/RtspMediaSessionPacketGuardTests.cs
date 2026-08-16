using VSP.Player.Decoder;
using VSP.Player.Entities;
using Xunit;

namespace VSP.Tests.Player;

/// <summary>
/// SEC-3 (Task 3A) coverage for RtspMediaSession's per-instance packet-size guard wiring --
/// EncodedPacketGuardTests.cs covers the pure decision logic in isolation; these tests prove
/// that logic is correctly wired to real instance state (the consecutive-rejection streak) on
/// the actual class ReadLoop uses, without needing a native session to be open (the guard runs
/// before any native call, so a freshly-constructed, never-opened session is sufficient and
/// genuinely real -- not a fake/mock of RtspMediaSession).
/// </summary>
public class RtspMediaSessionPacketGuardTests
{
    [Fact]
    public void EvaluateAndTrackPacketSize_AcceptsInBoundSize_WithoutFault()
    {
        using var session = new RtspMediaSession("rtsp://fake/stream");

        var decision = session.EvaluateAndTrackPacketSize(1024, out var error);

        Assert.Equal(PacketSizeDecision.Accept, decision);
        Assert.Null(error);
    }

    [Fact]
    public void EvaluateAndTrackPacketSize_DropsOversizedPacket_BelowEscalationThreshold()
    {
        using var session = new RtspMediaSession("rtsp://fake/stream");

        var decision = session.EvaluateAndTrackPacketSize(EncodedPacketGuard.MaxEncodedPacketSizeBytes + 1, out var error);

        Assert.Equal(PacketSizeDecision.Drop, decision);
        Assert.Null(error);
    }

    [Fact]
    public void EvaluateAndTrackPacketSize_DropsNegativeSizePacket_BelowEscalationThreshold()
    {
        using var session = new RtspMediaSession("rtsp://fake/stream");

        var decision = session.EvaluateAndTrackPacketSize(-1, out var error);

        Assert.Equal(PacketSizeDecision.Drop, decision);
        Assert.Null(error);
    }

    [Fact]
    public void EvaluateAndTrackPacketSize_FaultsOnceConsecutiveRejectionsReachThreshold()
    {
        using var session = new RtspMediaSession("rtsp://fake/stream");
        var decision = PacketSizeDecision.Accept;
        MediaError? error = null;

        for (var i = 0; i < EncodedPacketGuard.MaxConsecutiveRejectedPackets; i++)
        {
            decision = session.EvaluateAndTrackPacketSize(-1, out error);
        }

        Assert.Equal(PacketSizeDecision.Fault, decision);
        Assert.NotNull(error);
        Assert.Equal(MediaErrorCategory.Protocol, error!.Category);
    }

    [Fact]
    public void EvaluateAndTrackPacketSize_DoesNotFault_BeforeThresholdReached()
    {
        using var session = new RtspMediaSession("rtsp://fake/stream");
        var decision = PacketSizeDecision.Accept;

        for (var i = 0; i < EncodedPacketGuard.MaxConsecutiveRejectedPackets - 1; i++)
        {
            decision = session.EvaluateAndTrackPacketSize(-1, out var error);
            Assert.Null(error);
        }

        Assert.Equal(PacketSizeDecision.Drop, decision);
    }

    [Fact]
    public void EvaluateAndTrackPacketSize_AcceptedPacketResetsConsecutiveRejectionStreak()
    {
        using var session = new RtspMediaSession("rtsp://fake/stream");

        for (var i = 0; i < EncodedPacketGuard.MaxConsecutiveRejectedPackets - 1; i++)
        {
            session.EvaluateAndTrackPacketSize(-1, out _);
        }

        // One good packet in between must clear the streak -- an isolated near-threshold run of
        // bad packets, interrupted by a good one, must never fault the session.
        var resetDecision = session.EvaluateAndTrackPacketSize(1024, out var resetError);
        Assert.Equal(PacketSizeDecision.Accept, resetDecision);
        Assert.Null(resetError);

        // Repeating the same near-threshold run afterward must still require the full threshold
        // again, proving the earlier good packet genuinely cleared the counter rather than
        // merely pausing it.
        var decision = PacketSizeDecision.Accept;
        for (var i = 0; i < EncodedPacketGuard.MaxConsecutiveRejectedPackets - 1; i++)
        {
            decision = session.EvaluateAndTrackPacketSize(-1, out var error);
            Assert.Null(error);
        }

        Assert.Equal(PacketSizeDecision.Drop, decision);
    }
}
