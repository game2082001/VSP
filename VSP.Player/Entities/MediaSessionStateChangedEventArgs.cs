namespace VSP.Player.Entities;

public sealed class MediaSessionStateChangedEventArgs : EventArgs
{
    public required MediaSessionState State { get; init; }

    public MediaError? Error { get; init; }
}
