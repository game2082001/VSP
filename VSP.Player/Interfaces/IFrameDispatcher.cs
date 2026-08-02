using VSP.Player.Entities;

namespace VSP.Player.Interfaces;

public interface IFrameDispatcher<TFrame>
{
    IDisposable Subscribe(IFrameConsumer<TFrame> consumer, BufferPolicy policy);

    void Dispatch(TFrame frame);

    IDispatcherMetrics Metrics { get; }
}
