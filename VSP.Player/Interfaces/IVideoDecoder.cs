using VSP.Player.Entities;

namespace VSP.Player.Interfaces;

public interface IVideoDecoder : IDisposable
{
    /// <summary>Returns null when the packet produced no displayable frame (e.g. still buffering).</summary>
    DecodedFrame? Decode(EncodedFrame encodedFrame);

    void Reset();
}
