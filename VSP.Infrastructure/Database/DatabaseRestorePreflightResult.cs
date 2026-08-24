namespace VSP.Infrastructure.Database;

public sealed class DatabaseRestorePreflightResult
{
    private DatabaseRestorePreflightResult(
        bool success,
        bool canInstall,
        bool requiresConfigOnlyRestore,
        DatabaseRestorePreflightKind kind,
        string? failureMessage)
    {
        Success = success;
        CanInstall = canInstall;
        RequiresConfigOnlyRestore = requiresConfigOnlyRestore;
        Kind = kind;
        FailureMessage = failureMessage;
    }

    public bool Success { get; }

    public bool CanInstall { get; }

    public bool RequiresConfigOnlyRestore { get; }

    public DatabaseRestorePreflightKind Kind { get; }

    public string? FailureMessage { get; }

    public static DatabaseRestorePreflightResult LegacyPlaintext() =>
        new(
            success: true,
            canInstall: true,
            requiresConfigOnlyRestore: false,
            kind: DatabaseRestorePreflightKind.LegacyPlaintext,
            failureMessage: null);

    public static DatabaseRestorePreflightResult CurrentUserProtectedCredentials() =>
        new(
            success: true,
            canInstall: false,
            requiresConfigOnlyRestore: true,
            kind: DatabaseRestorePreflightKind.CurrentUserProtectedCredentials,
            failureMessage: null);

    public static DatabaseRestorePreflightResult ValidationFailed(string message) =>
        new(
            success: false,
            canInstall: false,
            requiresConfigOnlyRestore: false,
            kind: DatabaseRestorePreflightKind.Unknown,
            failureMessage: message);
}
