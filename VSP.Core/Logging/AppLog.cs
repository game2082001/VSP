namespace VSP.Core.Logging;

/// <summary>
/// Static, hand-wired gateway to the process-wide <see cref="ILogger"/> -- matches the
/// codebase's existing no-DI-container convention (compare <c>RecordingPathProvider</c>).
/// Defaults to a no-op logger until <see cref="Initialize"/> is called, so any call site
/// reachable before startup wiring runs never throws or writes to disk unexpectedly.
/// </summary>
public static class AppLog
{
    private static ILogger _logger = NoOpLogger.Instance;

    public static void Initialize(ILogger logger) => _logger = logger;

    public static void Debug(string message, Exception? exception = null) => _logger.Log(LogLevel.Debug, message, exception);

    public static void Info(string message, Exception? exception = null) => _logger.Log(LogLevel.Info, message, exception);

    public static void Warning(string message, Exception? exception = null) => _logger.Log(LogLevel.Warning, message, exception);

    public static void Error(string message, Exception? exception = null) => _logger.Log(LogLevel.Error, message, exception);

    public static void Fatal(string message, Exception? exception = null) => _logger.Log(LogLevel.Fatal, message, exception);

    private sealed class NoOpLogger : ILogger
    {
        public static readonly NoOpLogger Instance = new();

        public void Log(LogLevel level, string message, Exception? exception = null)
        {
        }
    }
}
