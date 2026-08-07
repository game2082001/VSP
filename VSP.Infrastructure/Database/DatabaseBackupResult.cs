namespace VSP.Infrastructure.Database;

/// <summary>
/// The smallest explicit result shape for <see cref="DatabaseBackupService.Backup"/> -- carries
/// the original <see cref="Exception"/> on failure rather than discarding it behind a bare bool
/// (Epic-017, same convention as <see cref="DatabaseInitializationResult"/>).
/// </summary>
public sealed class DatabaseBackupResult
{
    public bool Success { get; }

    public Exception? Exception { get; }

    private DatabaseBackupResult(bool success, Exception? exception)
    {
        Success = success;
        Exception = exception;
    }

    public static DatabaseBackupResult Ok() => new(success: true, exception: null);

    public static DatabaseBackupResult Failed(Exception exception) => new(success: false, exception);
}
