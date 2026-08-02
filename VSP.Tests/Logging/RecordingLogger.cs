using VSP.Core.Logging;

namespace VSP.Tests.Logging;

/// <summary>Shared test double for asserting what a component logged via AppLog, without asserting exact message text.</summary>
public sealed class RecordingLogger : ILogger
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Calls { get; } = [];

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        Calls.Add((level, message, exception));
    }
}
