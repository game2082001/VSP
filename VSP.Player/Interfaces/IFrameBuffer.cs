using VSP.Player.Entities;

namespace VSP.Player.Interfaces;

public interface IFrameBuffer<TFrame>
{
    BufferPolicy Policy { get; }

    void Enqueue(TFrame frame);

    bool TryDequeue(out TFrame? frame);

    int Count { get; }

    event EventHandler<FrameDroppedEventArgs>? FrameDropped;
}
