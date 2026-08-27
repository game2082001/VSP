namespace VSP.Player.Recording;

internal sealed record RecordingRetentionResult(
    int CandidatesScanned,
    int DeletedFiles,
    int SkippedFiles,
    int FailedFiles)
{
    public static RecordingRetentionResult Empty { get; } = new(0, 0, 0, 0);
}
