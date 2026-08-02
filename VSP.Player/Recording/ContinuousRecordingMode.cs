using VSP.Player.Entities;
using VSP.Player.Interfaces;

namespace VSP.Player.Recording;

/// <summary>Always records while active -- no schedule/motion trigger. Epic-011's only mode.</summary>
public sealed class ContinuousRecordingMode : IRecordingMode
{
    public string ModeName => "Continuous";

    public bool ShouldRecord(RecordingModeContext context) => true;
}
