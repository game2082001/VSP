namespace VSP.Player.Entities;

public sealed class EncodedPacketReceivedEventArgs : EventArgs
{
    public required EncodedFrame Packet { get; init; }
}
