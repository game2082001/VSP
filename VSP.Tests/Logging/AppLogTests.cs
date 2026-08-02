using VSP.Core.Logging;
using Xunit;

namespace VSP.Tests.Logging;

[Collection("AppLog")]
public class AppLogTests
{
    [Fact]
    public void EachLevelMethod_DelegatesToConfiguredLogger()
    {
        var recorder = new RecordingLogger();
        AppLog.Initialize(recorder);
        var exception = new InvalidOperationException("boom");

        AppLog.Debug("debug message");
        AppLog.Info("info message");
        AppLog.Warning("warning message");
        AppLog.Error("error message", exception);
        AppLog.Fatal("fatal message", exception);

        Assert.Equal(5, recorder.Calls.Count);
        Assert.Equal(LogLevel.Debug, recorder.Calls[0].Level);
        Assert.Equal("debug message", recorder.Calls[0].Message);
        Assert.Null(recorder.Calls[0].Exception);
        Assert.Equal(LogLevel.Info, recorder.Calls[1].Level);
        Assert.Equal(LogLevel.Warning, recorder.Calls[2].Level);
        Assert.Equal(LogLevel.Error, recorder.Calls[3].Level);
        Assert.Same(exception, recorder.Calls[3].Exception);
        Assert.Equal(LogLevel.Fatal, recorder.Calls[4].Level);
        Assert.Same(exception, recorder.Calls[4].Exception);
    }

    [Fact]
    public void Initialize_SwitchesTarget_PreviousLoggerStopsReceivingCalls()
    {
        var first = new RecordingLogger();
        var second = new RecordingLogger();

        AppLog.Initialize(first);
        AppLog.Info("goes to first");
        AppLog.Initialize(second);
        AppLog.Info("goes to second");

        Assert.Single(first.Calls);
        Assert.Single(second.Calls);
    }
}
