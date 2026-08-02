using System.Windows.Media;
using VSP.Player.Entities;

namespace VSP.Player.Interfaces;

public interface IFrameRenderer : IStreamingFrameConsumer<DecodedFrame>, IDisposable
{
    ImageSource? CurrentFrameSource { get; }

    event EventHandler? FrameRendered;
}
