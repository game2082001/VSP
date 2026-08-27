using System.IO;

namespace VSP.Player.Recording;

internal sealed class RecordingFileUsageTracker
{
    private readonly object _gate = new();
    private readonly Dictionary<string, int> _activeUsages = new(StringComparer.OrdinalIgnoreCase);

    public static RecordingFileUsageTracker Shared { get; } = new();

    public IDisposable Register(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var normalizedPath = Path.GetFullPath(filePath);
        lock (_gate)
        {
            _activeUsages.TryGetValue(normalizedPath, out var count);
            _activeUsages[normalizedPath] = count + 1;
        }

        return new UsageLease(this, normalizedPath);
    }

    public bool IsInUse(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        string normalizedPath;
        try
        {
            normalizedPath = Path.GetFullPath(filePath);
        }
        catch
        {
            return false;
        }

        lock (_gate)
        {
            return _activeUsages.ContainsKey(normalizedPath);
        }
    }

    internal int ActiveUsageCount(string filePath)
    {
        var normalizedPath = Path.GetFullPath(filePath);
        lock (_gate)
        {
            return _activeUsages.TryGetValue(normalizedPath, out var count) ? count : 0;
        }
    }

    public RecordingFileDeleteAttempt TryDeleteIfNotInUse(string filePath, Action<string> deleteFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(deleteFile);

        var normalizedPath = Path.GetFullPath(filePath);
        lock (_gate)
        {
            if (_activeUsages.ContainsKey(normalizedPath))
            {
                return RecordingFileDeleteAttempt.SkippedInUse;
            }

            deleteFile(normalizedPath);
            return RecordingFileDeleteAttempt.Deleted;
        }
    }

    private void Release(string normalizedPath)
    {
        lock (_gate)
        {
            if (!_activeUsages.TryGetValue(normalizedPath, out var count))
            {
                return;
            }

            if (count <= 1)
            {
                _activeUsages.Remove(normalizedPath);
                return;
            }

            _activeUsages[normalizedPath] = count - 1;
        }
    }

    private sealed class UsageLease : IDisposable
    {
        private RecordingFileUsageTracker? _owner;
        private readonly string _normalizedPath;

        public UsageLease(RecordingFileUsageTracker owner, string normalizedPath)
        {
            _owner = owner;
            _normalizedPath = normalizedPath;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            owner?.Release(_normalizedPath);
        }
    }
}

internal enum RecordingFileDeleteAttempt
{
    Deleted,
    SkippedInUse
}
