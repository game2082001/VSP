namespace VSP.Player.Entities;

public sealed class MediaControllerStateChangedEventArgs : EventArgs
{
    public required MediaControllerState State { get; init; }

    public MediaError? Error { get; init; }
}
