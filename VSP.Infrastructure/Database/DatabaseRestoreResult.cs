namespace VSP.Infrastructure.Database;

/// <summary>
/// The smallest explicit result shape for <see cref="DatabaseRestoreService"/> -- carries a
/// user-facing <see cref="FailureMessage"/> for both a plain validation rejection (Epic-017
/// §3.7, no exception involved) and an I/O failure during installation (§3.4), plus the
/// original <see cref="Exception"/> when one exists, rather than discarding it behind a bare
/// bool (same convention as <see cref="DatabaseInitializationResult"/>).
/// </summary>
public sealed class DatabaseRestoreResult
{
    public bool Success { get; }

    public string? FailureMessage { get; }

    public Exception? Exception { get; }

    private DatabaseRestoreResult(bool success, string? failureMessage, Exception? exception)
    {
        Success = success;
        FailureMessage = failureMessage;
        Exception = exception;
    }

    public static DatabaseRestoreResult Ok() => new(success: true, failureMessage: null, exception: null);

    public static DatabaseRestoreResult ValidationFailed(string message) =>
        new(success: false, failureMessage: message, exception: null);

    public static DatabaseRestoreResult Failed(string message, Exception exception) =>
        new(success: false, failureMessage: message, exception: exception);
}
