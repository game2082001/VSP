namespace VSP.Infrastructure.Database;

internal enum CameraCredentialMigrationState
{
    SourceMissing,
    NotStarted,
    ReadyForActivation,
    SourceChangedSinceStaging,
    InvalidStaging,
    SourceAlreadyProtected,
    UnsupportedSource
}

internal enum CameraCredentialMigrationOutcome
{
    Staged,
    AlreadyStaged
}
