using VSP.Player.Entities;

namespace VSP.Player.Interfaces;

/// <summary>
/// A recording mode strategy, per ADR-002: <see cref="IRecordingSession"/> delegates to this
/// rather than hardcoding one behavior. Continuous is the only implementation in Epic-011.
/// </summary>
public interface IRecordingMode
{
    string ModeName { get; }

    bool ShouldRecord(RecordingModeContext context);
}
