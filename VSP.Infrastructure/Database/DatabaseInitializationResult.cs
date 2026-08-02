namespace VSP.Infrastructure.Database;

/// <summary>
/// The smallest explicit result shape for <see cref="DatabaseInitializer.Initialize"/> --
/// carries the original <see cref="Exception"/> on failure rather than discarding it behind a
/// bare bool. Not a generic Result framework; scoped to this one caller.
/// </summary>
public sealed class DatabaseInitializationResult
{
    public bool Success { get; }

    public Exception? Exception { get; }

    private DatabaseInitializationResult(bool success, Exception? exception)
    {
        Success = success;
        Exception = exception;
    }

    public static DatabaseInitializationResult Ok() => new(success: true, exception: null);

    public static DatabaseInitializationResult Failed(Exception exception) => new(success: false, exception);
}
