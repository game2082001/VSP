using VSP.Core.Logging;
using Xunit;

namespace VSP.Tests.Logging;

public class FileLoggerTests : IDisposable
{
    private readonly string _logsDirectory =
        Path.Combine(Path.GetTempPath(), $"vsp-file-logger-test-{Guid.NewGuid():N}");

    private DateTime _now = new(2026, 8, 1, 10, 0, 0);

    private FileLogger CreateLogger() => new(_logsDirectory, () => _now);

    [Fact]
    public void Log_WritesTodaysFileWithFixedFormat()
    {
        var logger = CreateLogger();

        logger.Log(LogLevel.Info, "camera connected");

        var filePath = Path.Combine(_logsDirectory, "2026-08-01.log");
        Assert.True(File.Exists(filePath));

        var line = File.ReadAllText(filePath);
        Assert.Contains("2026-08-01 10:00:00.000", line);
        Assert.Contains("INFO", line);
        Assert.Contains("camera connected", line);
    }

    [Fact]
    public void Log_WithException_AppendsExceptionDetails()
    {
        var logger = CreateLogger();
        var exception = new InvalidOperationException("boom");

        logger.Log(LogLevel.Error, "operation failed", exception);

        var filePath = Path.Combine(_logsDirectory, "2026-08-01.log");
        var content = File.ReadAllText(filePath);

        Assert.Contains("ERROR", content);
        Assert.Contains("operation failed", content);
        Assert.Contains("InvalidOperationException", content);
        Assert.Contains("boom", content);
    }

    [Fact]
    public void Log_MultipleCallsSameDay_AppendsToSameFile()
    {
        var logger = CreateLogger();

        logger.Log(LogLevel.Debug, "first");
        logger.Log(LogLevel.Warning, "second");

        var files = Directory.GetFiles(_logsDirectory, "*.log");
        Assert.Single(files);

        var lines = File.ReadAllLines(files[0]);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public void Log_WhenDayAdvances_WritesToNewFile()
    {
        var logger = CreateLogger();

        logger.Log(LogLevel.Info, "day one");
        _now = _now.AddDays(1);
        logger.Log(LogLevel.Info, "day two");

        Assert.True(File.Exists(Path.Combine(_logsDirectory, "2026-08-01.log")));
        Assert.True(File.Exists(Path.Combine(_logsDirectory, "2026-08-02.log")));
    }

    [Fact]
    public void PurgeOldFiles_DeletesOnlyFilesOlderThanRetention()
    {
        Directory.CreateDirectory(_logsDirectory);
        File.WriteAllText(Path.Combine(_logsDirectory, "2026-06-01.log"), "old");
        File.WriteAllText(Path.Combine(_logsDirectory, "2026-07-15.log"), "recent");
        File.WriteAllText(Path.Combine(_logsDirectory, "not-a-date.log"), "ignored");

        var logger = CreateLogger();
        var deleted = logger.PurgeOldFiles(retentionDays: 30);

        Assert.Equal(1, deleted);
        Assert.False(File.Exists(Path.Combine(_logsDirectory, "2026-06-01.log")));
        Assert.True(File.Exists(Path.Combine(_logsDirectory, "2026-07-15.log")));
        Assert.True(File.Exists(Path.Combine(_logsDirectory, "not-a-date.log")));
    }

    [Fact]
    public void PurgeOldFiles_NoOldFiles_ReturnsZero()
    {
        var logger = CreateLogger();
        logger.Log(LogLevel.Info, "fresh");

        var deleted = logger.PurgeOldFiles(retentionDays: FileLogger.DefaultRetentionDays);

        Assert.Equal(0, deleted);
    }

    [Fact]
    public void Log_ConcurrentWrites_DoesNotLoseOrCorruptLines()
    {
        var logger = CreateLogger();
        const int threadCount = 8;
        const int linesPerThread = 50;

        Parallel.For(0, threadCount, i =>
        {
            for (var j = 0; j < linesPerThread; j++)
            {
                logger.Log(LogLevel.Debug, $"thread {i} line {j}");
            }
        });

        var filePath = Path.Combine(_logsDirectory, "2026-08-01.log");
        var lines = File.ReadAllLines(filePath);

        Assert.Equal(threadCount * linesPerThread, lines.Length);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_logsDirectory))
            {
                Directory.Delete(_logsDirectory, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
