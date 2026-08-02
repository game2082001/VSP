namespace VSP.Player.Entities;

public sealed class FrameDroppedEventArgs : EventArgs
{
    public required int DroppedCount { get; init; }
}
