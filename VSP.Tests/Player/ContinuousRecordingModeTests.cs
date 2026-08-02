using VSP.Player.Entities;
using VSP.Player.Recording;
using Xunit;

namespace VSP.Tests.Player;

public class ContinuousRecordingModeTests
{
    [Fact]
    public void ShouldRecord_AlwaysReturnsTrue()
    {
        var mode = new ContinuousRecordingMode();

        Assert.True(mode.ShouldRecord(new RecordingModeContext { Timestamp = DateTimeOffset.UtcNow }));
        Assert.True(mode.ShouldRecord(new RecordingModeContext { Timestamp = DateTimeOffset.MinValue }));
    }

    [Fact]
    public void ModeName_IsContinuous()
    {
        Assert.Equal("Continuous", new ContinuousRecordingMode().ModeName);
    }
}
