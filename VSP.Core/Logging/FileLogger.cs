using System.Globalization;

namespace VSP.Core.Logging;

/// <summary>
/// The one production <see cref="ILogger"/> implementation for VSP -- appends fixed-format
/// plain-text lines to a log file that rolls to a new file (named <c>YYYY-MM-DD.log</c>) at
/// the start of each calendar day, and purges files older than <see cref="DefaultRetentionDays"/>.
/// No external logging framework, no structured/JSON output, no database or network sink --
/// per the approved Epic-014 scope.
/// </summary>
public sealed class FileLogger : ILogger
{
    public const int DefaultRetentionDays = 30;

    private const string FileNameFormat = "yyyy-MM-dd";
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff";

    private readonly string _logsDirectory;
    private readonly Func<DateTime> _clock;
    private readonly object _writeLock = new();

    public FileLogger(string logsDirectory) : this(logsDirectory, static () => DateTime.Now)
    {
    }

    /// <summary>Test seam: lets tests control "today" without waiting for a real day boundary.</summary>
    internal FileLogger(string logsDirectory, Func<DateTime> clock)
    {
        _logsDirectory = logsDirectory;
        _clock = clock;
        Directory.CreateDirectory(_logsDirectory);
    }

    public void Log(LogLevel level, string message, Exception? exception = null)
    {
        var now = _clock();
        var line = $"{now.ToString(TimestampFormat, CultureInfo.InvariantCulture)} | {level.ToString().ToUpperInvariant(),-7} | {message}";
        if (exception is not null)
        {
            line += Environment.NewLine + "    " + exception;
        }

        var filePath = GetLogFilePath(now);
        var bytes = System.Text.Encoding.UTF8.GetBytes(line + Environment.NewLine);

        lock (_writeLock)
        {
            // Explicit FileStream + Flush(true) rather than File.AppendAllText: a crash log must
            // be on physical disk before this call returns, not merely handed to an OS write
            // buffer that a process about to Environment.Exit (OnUnhandledException) or genuinely
            // crash might not get to persist. Per Product Owner instruction: crash logs must not
            // be buffered.
            using var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush(flushToDisk: true);
        }
    }

    /// <summary>The path of the file today's <see cref="Log"/> calls are (or would be) written to.</summary>
    public string GetCurrentLogFilePath() => GetLogFilePath(_clock());

    private string GetLogFilePath(DateTime when) =>
        Path.Combine(_logsDirectory, when.ToString(FileNameFormat, CultureInfo.InvariantCulture) + ".log");

    /// <summary>Deletes any <c>YYYY-MM-DD.log</c> file older than <paramref name="retentionDays"/>. Returns the number of files deleted.</summary>
    public int PurgeOldFiles(int retentionDays = DefaultRetentionDays)
    {
        var cutoff = _clock().Date.AddDays(-retentionDays);
        var deleted = 0;

        foreach (var file in Directory.EnumerateFiles(_logsDirectory, "*.log"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!DateTime.TryParseExact(name, FileNameFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fileDate)
                || fileDate >= cutoff)
            {
                continue;
            }

            try
            {
                File.Delete(file);
                deleted++;
            }
            catch (IOException)
            {
                // A file locked by another process is left for the next purge -- best-effort,
                // same convention as RecordingPathProvider's malformed-config fallback.
            }
        }

        return deleted;
    }
}
