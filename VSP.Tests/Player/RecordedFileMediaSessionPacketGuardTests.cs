using VSP.Player.Decoder;
using VSP.Player.Entities;
using Xunit;

namespace VSP.Tests.Player;

/// <summary>
/// SEC-3 (Task 3A) coverage for RecordedFileMediaSession's per-instance packet-size guard wiring
/// -- mirrors RtspMediaSessionPacketGuardTests.cs. RecordedFileMediaSession is a separate
/// implementation of IMediaSession (deliberately not sharing a base class with RtspMediaSession,
/// per its own class-level doc comment), so its own _consecutiveRejectedPackets wiring is a
/// distinct piece of code that needs its own proof, not merely inferred from RtspMediaSession's.
/// </summary>
public class RecordedFileMediaSessionPacketGuardTests
{
    [Fact]
    public void EvaluateAndTrackPacketSize_AcceptsInBoundSize_WithoutFault()
    {
        using var session = new RecordedFileMediaSession("C:\\fake\\path.mp4");

        var decision = session.EvaluateAndTrackPacketSize(1024, out var error);

        Assert.Equal(PacketSizeDecision.Accept, decision);
        Assert.Null(error);
    }

    [Fact]
    public void EvaluateAndTrackPacketSize_DropsOversizedPacket_BelowEscalationThreshold()
    {
        using var session = new RecordedFileMediaSession("C:\\fake\\path.mp4");

        var decision = session.EvaluateAndTrackPacketSize(EncodedPacketGuard.MaxEncodedPacketSizeBytes + 1, out var error);

        Assert.Equal(PacketSizeDecision.Drop, decision);
        Assert.Null(error);
    }

    [Fact]
    public void EvaluateAndTrackPacketSize_DropsNegativeSizePacket_BelowEscalationThreshold()
    {
        using var session = new RecordedFileMediaSession("C:\\fake\\path.mp4");

        var decision = session.EvaluateAndTrackPacketSize(-1, out var error);

        Assert.Equal(PacketSizeDecision.Drop, decision);
        Assert.Null(error);
    }

    [Fact]
    public void EvaluateAndTrackPacketSize_FaultsOnceConsecutiveRejectionsReachThreshold()
    {
        using var session = new RecordedFileMediaSession("C:\\fake\\path.mp4");
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
    public void EvaluateAndTrackPacketSize_AcceptedPacketResetsConsecutiveRejectionStreak()
    {
        using var session = new RecordedFileMediaSession("C:\\fake\\path.mp4");

        for (var i = 0; i < EncodedPacketGuard.MaxConsecutiveRejectedPackets - 1; i++)
        {
            session.EvaluateAndTrackPacketSize(-1, out _);
        }

        var resetDecision = session.EvaluateAndTrackPacketSize(1024, out var resetError);
        Assert.Equal(PacketSizeDecision.Accept, resetDecision);
        Assert.Null(resetError);

        var decision = PacketSizeDecision.Accept;
        for (var i = 0; i < EncodedPacketGuard.MaxConsecutiveRejectedPackets - 1; i++)
        {
            decision = session.EvaluateAndTrackPacketSize(-1, out var error);
            Assert.Null(error);
        }

        Assert.Equal(PacketSizeDecision.Drop, decision);
    }
}
