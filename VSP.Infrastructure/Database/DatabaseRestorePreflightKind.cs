namespace VSP.Infrastructure.Database;

public enum DatabaseRestorePreflightKind
{
    Unknown = 0,
    LegacyPlaintext = 1,
    CurrentUserProtectedCredentials = 2
}
