using VSP.Player.Decoder;
using Xunit;

namespace VSP.Tests.Player;

public class DecodedFrameGuardTests
{
    [Fact]
    public void TryEvaluate_Normal1920x1080_Accepted()
    {
        var accepted = DecodedFrameGuard.TryEvaluate(1920, 1080, out var bgraByteCount);

        Assert.True(accepted);
        Assert.Equal(1920L * 1080 * 4, bgraByteCount);
    }

    [Fact]
    public void TryEvaluate_Normal3840x2160_Accepted()
    {
        var accepted = DecodedFrameGuard.TryEvaluate(3840, 2160, out var bgraByteCount);

        Assert.True(accepted);
        Assert.Equal(3840L * 2160 * 4, bgraByteCount);
    }

    [Fact]
    public void TryEvaluate_ExistingRealCameraResolution4512x1728_Accepted()
    {
        var accepted = DecodedFrameGuard.TryEvaluate(4512, 1728, out var bgraByteCount);

        Assert.True(accepted);
        Assert.Equal(4512L * 1728 * 4, bgraByteCount);
    }

    [Fact]
    public void TryEvaluate_8K7680x4320_AcceptedAtCeiling()
    {
        var accepted = DecodedFrameGuard.TryEvaluate(7680, 4320, out var bgraByteCount);

        Assert.True(accepted);
        Assert.Equal(7680L * 4320 * 4, bgraByteCount);
        Assert.Equal(DecodedFrameGuard.MaxDecodedBgraBytes, bgraByteCount);
    }

    [Fact]
    public void TryEvaluate_ZeroWidth_Rejected()
    {
        var accepted = DecodedFrameGuard.TryEvaluate(0, 1080, out var bgraByteCount);

        Assert.False(accepted);
        Assert.Equal(0, bgraByteCount);
    }

    [Fact]
    public void TryEvaluate_ZeroHeight_Rejected()
    {
        var accepted = DecodedFrameGuard.TryEvaluate(1920, 0, out var bgraByteCount);

        Assert.False(accepted);
        Assert.Equal(0, bgraByteCount);
    }

    [Fact]
    public void TryEvaluate_NegativeWidth_Rejected()
    {
        var accepted = DecodedFrameGuard.TryEvaluate(-1920, 1080, out var bgraByteCount);

        Assert.False(accepted);
        Assert.Equal(0, bgraByteCount);
    }

    [Fact]
    public void TryEvaluate_NegativeHeight_Rejected()
    {
        var accepted = DecodedFrameGuard.TryEvaluate(1920, -1080, out var bgraByteCount);

        Assert.False(accepted);
        Assert.Equal(0, bgraByteCount);
    }

    [Fact]
    public void TryEvaluate_WidthCausingInt32MultiplyByFourOverflow_RejectedWithoutThrowing()
    {
        // 600,000,000 * 4 = 2,400,000,000, which overflows System.Int32 (max ~2.147B) if computed
        // in unchecked int arithmetic -- the original vulnerable shape. Also, on its own, this
        // width is already far above MaxDecodedWidth, so it is rejected by the dimension cap
        // before any multiplication is attempted at all. The assertion here is that evaluating
        // it is safe (no exception escapes) and it is rejected.
        var accepted = DecodedFrameGuard.TryEvaluate(600_000_000, 1080, out var bgraByteCount);

        Assert.False(accepted);
        Assert.Equal(0, bgraByteCount);
    }

    [Fact]
    public void TryEvaluate_DimensionsCausingStrideTimesHeightOverflow_RejectedWithoutThrowing()
    {
        // width at the accepted ceiling, height far beyond it -- stride*height would be an
        // enormous product if ever computed; must be rejected by the height cap first.
        var accepted = DecodedFrameGuard.TryEvaluate(DecodedFrameGuard.MaxDecodedWidth, 600_000_000, out var bgraByteCount);

        Assert.False(accepted);
        Assert.Equal(0, bgraByteCount);
    }

    [Fact]
    public void TryEvaluate_ExtremeInt32MaxValueDimensions_RejectedWithoutThrowing()
    {
        // The most adversarial int input possible for both dimensions simultaneously -- proves
        // the checked-long-arithmetic path never lets an unhandled OverflowException escape,
        // regardless of how the guard internally computes things.
        var accepted = DecodedFrameGuard.TryEvaluate(int.MaxValue, int.MaxValue, out var bgraByteCount);

        Assert.False(accepted);
        Assert.Equal(0, bgraByteCount);
    }

    [Fact]
    public void TryEvaluate_JustBelowAcceptedCeiling_Accepted()
    {
        var accepted = DecodedFrameGuard.TryEvaluate(
            DecodedFrameGuard.MaxDecodedWidth - 1,
            DecodedFrameGuard.MaxDecodedHeight,
            out var bgraByteCount);

        Assert.True(accepted);
        Assert.True(bgraByteCount < DecodedFrameGuard.MaxDecodedBgraBytes);
    }

    [Fact]
    public void TryEvaluate_AtAcceptedCeiling_Accepted()
    {
        var accepted = DecodedFrameGuard.TryEvaluate(
            DecodedFrameGuard.MaxDecodedWidth,
            DecodedFrameGuard.MaxDecodedHeight,
            out var bgraByteCount);

        Assert.True(accepted);
        Assert.Equal(DecodedFrameGuard.MaxDecodedBgraBytes, bgraByteCount);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(0, 1)]
    public void TryEvaluate_OneDimensionAboveAcceptedCeiling_Rejected(int widthOverflow, int heightOverflow)
    {
        var accepted = DecodedFrameGuard.TryEvaluate(
            DecodedFrameGuard.MaxDecodedWidth + widthOverflow,
            DecodedFrameGuard.MaxDecodedHeight + heightOverflow,
            out var bgraByteCount);

        Assert.False(accepted);
        Assert.Equal(0, bgraByteCount);
    }
}
