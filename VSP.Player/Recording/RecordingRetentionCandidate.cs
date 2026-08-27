namespace VSP.Player.Recording;

internal sealed record RecordingRetentionCandidate(
    string FilePath,
    Guid CameraId,
    DateTime RecordedAt);
