using VSP.Player.Decoder;
using Xunit;

namespace VSP.Tests.Player;

public class EncodedPacketGuardTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1024)]
    [InlineData(EncodedPacketGuard.MaxEncodedPacketSizeBytes)]
    public void IsPacketSizeAcceptable_ReturnsTrue_ForZeroThroughBound(int size)
    {
        Assert.True(EncodedPacketGuard.IsPacketSizeAcceptable(size));
    }

    [Fact]
    public void IsPacketSizeAcceptable_ReturnsFalse_OneByteOverBound()
    {
        Assert.False(EncodedPacketGuard.IsPacketSizeAcceptable(EncodedPacketGuard.MaxEncodedPacketSizeBytes + 1));
    }

    [Fact]
    public void IsPacketSizeAcceptable_ReturnsFalse_ForExtremelyLargePositiveSize()
    {
        Assert.False(EncodedPacketGuard.IsPacketSizeAcceptable(int.MaxValue));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-1024)]
    [InlineData(int.MinValue)]
    public void IsPacketSizeAcceptable_ReturnsFalse_ForNegativeSize(int size)
    {
        Assert.False(EncodedPacketGuard.IsPacketSizeAcceptable(size));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(EncodedPacketGuard.MaxConsecutiveRejectedPackets - 1, false)]
    [InlineData(EncodedPacketGuard.MaxConsecutiveRejectedPackets, true)]
    [InlineData(EncodedPacketGuard.MaxConsecutiveRejectedPackets + 1, true)]
    public void ShouldEscalateToFault_ReturnsExpectedResult(int consecutiveRejectedCount, bool expected)
    {
        Assert.Equal(expected, EncodedPacketGuard.ShouldEscalateToFault(consecutiveRejectedCount));
    }
}
