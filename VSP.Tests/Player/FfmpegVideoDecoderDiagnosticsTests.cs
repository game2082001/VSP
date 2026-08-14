using VSP.Player.Decoder;
using Xunit;

namespace VSP.Tests.Player;

/// <summary>
/// Task-AI00B Phase 5: covers the one piece of the new BGRA conversion diagnostics that is a
/// pure function (<see cref="FfmpegVideoDecoder.SampleBufferHasNonZeroContent"/>). The rest of
/// the diagnostics (sws_scale return capture, format-drift counter) live inside
/// <c>ConvertToBgra32</c>, which requires a live native decode session and is exercised by
/// real-device validation instead (see Phase 5 completion report).
///
/// Task-AI00B Phase 12A: also covers <see cref="FfmpegVideoDecoder.IsDiagnosticsIntervalElapsed"/>,
/// the pure time-gate extracted so the periodic decode-summary cadence (now ~5 minutes, replacing
/// the old every-50-decode-attempts cadence) is unit-testable without a live native decode session.
/// </summary>
public class FfmpegVideoDecoderDiagnosticsTests
{
    [Fact]
    public void SampleBufferHasNonZeroContent_AllZeroBuffer_ReturnsFalse()
    {
        var stride = 16;
        var height = 4;
        var buffer = new byte[stride * height];

        var result = FfmpegVideoDecoder.SampleBufferHasNonZeroContent(buffer, stride, height);

        Assert.False(result);
    }

    [Fact]
    public void SampleBufferHasNonZeroContent_NonZeroAtSampledCorner_ReturnsTrue()
    {
        var stride = 16;
        var height = 4;
        var buffer = new byte[stride * height];
        buffer[0] = 0xFF;

        var result = FfmpegVideoDecoder.SampleBufferHasNonZeroContent(buffer, stride, height);

        Assert.True(result);
    }

    [Fact]
    public void SampleBufferHasNonZeroContent_NonZeroAtInteriorSamplePoint_ReturnsTrue()
    {
        // 5 wide x 3 tall: the fixed sample grid's fractional rows/columns land on row 1, col 2
        // for these dimensions, so this exercises an interior (non-corner) grid point.
        var stride = 5;
        var height = 3;
        var buffer = new byte[stride * height];
        buffer[(1 * stride) + 2] = 0x10;

        var result = FfmpegVideoDecoder.SampleBufferHasNonZeroContent(buffer, stride, height);

        Assert.True(result);
    }

    [Fact]
    public void SampleBufferHasNonZeroContent_NonZeroByteNotOnSampleGrid_ReturnsFalse()
    {
        // A large frame where only a single byte far from every fixed sample point is non-zero:
        // the bounded sample must not find it, by design (it is a coarse indicator, not a scan).
        var stride = 4000;
        var height = 1000;
        var buffer = new byte[stride * height];
        buffer[(10 * stride) + 10] = 0x10;

        var result = FfmpegVideoDecoder.SampleBufferHasNonZeroContent(buffer, stride, height);

        Assert.False(result);
    }

    [Fact]
    public void SampleBufferHasNonZeroContent_EmptyBuffer_ReturnsFalse()
    {
        var result = FfmpegVideoDecoder.SampleBufferHasNonZeroContent([], stride: 0, height: 0);

        Assert.False(result);
    }

    [Fact]
    public void SampleBufferHasNonZeroContent_InvalidStrideOrHeight_ReturnsFalse()
    {
        var buffer = new byte[16];

        Assert.False(FfmpegVideoDecoder.SampleBufferHasNonZeroContent(buffer, stride: 0, height: 4));
        Assert.False(FfmpegVideoDecoder.SampleBufferHasNonZeroContent(buffer, stride: 4, height: 0));
        Assert.False(FfmpegVideoDecoder.SampleBufferHasNonZeroContent(buffer, stride: -4, height: 4));
    }

    [Fact]
    public void IsDiagnosticsIntervalElapsed_BeforeIntervalElapses_ReturnsFalse()
    {
        var result = FfmpegVideoDecoder.IsDiagnosticsIntervalElapsed(
            lastLoggedTickCount: 0, nowTickCount: 1000, intervalMilliseconds: 5000);

        Assert.False(result);
    }

    [Fact]
    public void IsDiagnosticsIntervalElapsed_ExactlyAtInterval_ReturnsTrue()
    {
        var result = FfmpegVideoDecoder.IsDiagnosticsIntervalElapsed(
            lastLoggedTickCount: 0, nowTickCount: 5000, intervalMilliseconds: 5000);

        Assert.True(result);
    }

    [Fact]
    public void IsDiagnosticsIntervalElapsed_WellPastInterval_ReturnsTrue()
    {
        var result = FfmpegVideoDecoder.IsDiagnosticsIntervalElapsed(
            lastLoggedTickCount: 0, nowTickCount: 999_999, intervalMilliseconds: 5000);

        Assert.True(result);
    }

    [Fact]
    public void IsDiagnosticsIntervalElapsed_OneMillisecondBeforeInterval_ReturnsFalse()
    {
        var result = FfmpegVideoDecoder.IsDiagnosticsIntervalElapsed(
            lastLoggedTickCount: 0, nowTickCount: 4999, intervalMilliseconds: 5000);

        Assert.False(result);
    }

    [Fact]
    public void IsDiagnosticsIntervalElapsed_FiftyDecodeAttemptsWorthOfElapsedTimeAtVideoFramerate_StillReturnsFalse()
    {
        // Regression guard for the actual defect this phase fixes: the old cadence fired every
        // 50 decode attempts, which at a typical 25fps Live View stream is only ~2 seconds. The
        // new 5-minute-scale gate must not fire on anything close to that short a span.
        var twoSecondsInMilliseconds = 2000;

        var result = FfmpegVideoDecoder.IsDiagnosticsIntervalElapsed(
            lastLoggedTickCount: 0,
            nowTickCount: twoSecondsInMilliseconds,
            intervalMilliseconds: FfmpegVideoDecoderProductionIntervalMilliseconds);

        Assert.False(result);
    }

    [Fact]
    public void IsDiagnosticsIntervalElapsed_ProductionIntervalIsApproximatelyFiveMinutes()
    {
        // Pins the actual production constant (via a duplicated literal, since the field is
        // private) so an accidental future edit back toward a short cadence fails this test.
        Assert.Equal(TimeSpan.FromMinutes(5), TimeSpan.FromMilliseconds(FfmpegVideoDecoderProductionIntervalMilliseconds));
    }

    private const long FfmpegVideoDecoderProductionIntervalMilliseconds = 5 * 60 * 1000;
}
