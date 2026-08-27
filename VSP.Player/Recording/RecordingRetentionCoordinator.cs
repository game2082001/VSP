using VSP.Core.Logging;

namespace VSP.Player.Recording;

public sealed class RecordingRetentionCoordinator : IDisposable
{
    private static readonly TimeSpan DefaultPeriodicInterval = TimeSpan.FromDays(1);

    private readonly Func<RecordingRetentionSettings> _loadSettings;
    private readonly RecordingRetentionService _retentionService;
    private readonly TimeSpan _periodicInterval;
    private readonly Func<Func<Task>, Task> _schedule;
    private readonly object _gate = new();
    private int _running;
    private bool _disposed;
    private Timer? _periodicTimer;

    public RecordingRetentionCoordinator(Func<RecordingRetentionSettings> loadSettings)
        : this(
            loadSettings,
            new RecordingRetentionService(),
            DefaultPeriodicInterval,
            static work => Task.Run(work))
    {
    }

    internal RecordingRetentionCoordinator(
        Func<RecordingRetentionSettings> loadSettings,
        RecordingRetentionService retentionService,
        TimeSpan periodicInterval,
        Func<Func<Task>, Task>? schedule = null)
    {
        _loadSettings = loadSettings ?? throw new ArgumentNullException(nameof(loadSettings));
        _retentionService = retentionService ?? throw new ArgumentNullException(nameof(retentionService));
        _periodicInterval = periodicInterval;
        _schedule = schedule ?? (static work => Task.Run(work));
    }

    public void Start()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _periodicTimer ??= new Timer(
                static state => _ = ((RecordingRetentionCoordinator)state!).RunRootAsync("periodic"),
                this,
                _periodicInterval,
                _periodicInterval);
        }

        _ = RunRootAsync("startup");
    }

    public void TriggerRecordingCompleted(Guid cameraId)
    {
        _ = RunCameraAsync("recording completion", cameraId);
    }

    internal Task RunStartupAsync() => RunRootAsync("startup");

    internal Task RunPeriodicAsync() => RunRootAsync("periodic");

    internal Task RunRecordingCompletedAsync(Guid cameraId) => RunCameraAsync("recording completion", cameraId);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _periodicTimer?.Dispose();
            _periodicTimer = null;
        }
    }

    private Task RunRootAsync(string trigger)
    {
        if (IsDisposed())
        {
            return Task.CompletedTask;
        }

        return Schedule(() => RunSingleFlightAsync(trigger, cameraId: null));
    }

    private Task RunCameraAsync(string trigger, Guid cameraId)
    {
        if (IsDisposed())
        {
            return Task.CompletedTask;
        }

        return Schedule(() => RunSingleFlightAsync(trigger, cameraId));
    }

    private Task Schedule(Func<Task> work)
    {
        try
        {
            return _schedule(work);
        }
        catch (Exception ex)
        {
            AppLog.Warning($"Recording retention scheduling failed: {ex.GetType().Name}.");
            return Task.CompletedTask;
        }
    }

    private Task RunSingleFlightAsync(string trigger, Guid? cameraId)
    {
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            AppLog.Info($"Recording retention skipped overlapping {trigger} trigger.");
            return Task.CompletedTask;
        }

        try
        {
            if (IsDisposed())
            {
                return Task.CompletedTask;
            }

            var settings = _loadSettings();
            var result = cameraId.HasValue
                ? _retentionService.RunForCamera(settings.RecordingRoot, cameraId.Value, settings.RetentionDays)
                : _retentionService.Run(settings.RecordingRoot, settings.RetentionDays);

            AppLog.Info(
                $"Recording retention {trigger} completed: " +
                $"scanned={result.CandidatesScanned}, deleted={result.DeletedFiles}, skipped={result.SkippedFiles}, failed={result.FailedFiles}.");
        }
        catch (Exception ex)
        {
            AppLog.Warning($"Recording retention {trigger} failed: {ex.GetType().Name}.");
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }

        return Task.CompletedTask;
    }

    private bool IsDisposed()
    {
        lock (_gate)
        {
            return _disposed;
        }
    }
}
