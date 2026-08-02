namespace VSP.Player.Entities;

/// <summary>
/// Input to <see cref="Interfaces.IRecordingMode.ShouldRecord"/>, per ADR-002's Recorder mode
/// abstraction. Continuous mode ignores this entirely; a future Scheduled mode is the reason
/// this is a context object rather than no parameter at all.
/// </summary>
public sealed class RecordingModeContext
{
    public required DateTimeOffset Timestamp { get; init; }
}
