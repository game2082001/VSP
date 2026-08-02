namespace VSP.Player.Interfaces;

public interface IStreamingFrameConsumer<TFrame> : IFrameConsumer<TFrame>
{
    bool IsActive { get; }

    void Start();

    void Stop();
}
