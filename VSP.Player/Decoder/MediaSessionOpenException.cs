using VSP.Player.Entities;

namespace VSP.Player.Decoder;

internal sealed class MediaSessionOpenException : Exception
{
    public MediaError Error { get; }

    public MediaSessionOpenException(MediaError error)
        : base(error.Message)
    {
        Error = error;
    }
}
