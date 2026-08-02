using VSP.Player.Entities;

namespace VSP.Player.Interfaces;

/// <summary>
/// Attaches at the encoded tier, per ADR-002 -- independent of any <see cref="IFrameRenderer"/>
/// subscription. Implementations mux the already-encoded stream (stream-copy); no decode or
/// re-encode happens here.
/// </summary>
public interface IRecordingSession : IStreamingFrameConsumer<EncodedFrame>, IDisposable
{
    IRecordingMode Mode { get; }

    Task StartAsync(string filePath, CancellationToken cancellationToken);

    Task StopAsync();
}
