namespace VSP.Player.Interfaces;

public interface IFrameConsumer<TFrame>
{
    void OnFrame(TFrame frame);
}
