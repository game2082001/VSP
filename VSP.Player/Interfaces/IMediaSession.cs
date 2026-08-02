using VSP.Player.Entities;

namespace VSP.Player.Interfaces;

/// <summary>
/// A generic video source. Live RTSP is the v1 implementation; RecordedFile/Remote/Synthetic
/// are accommodated by <see cref="VideoSourceKind"/> without a second pipeline being required.
/// </summary>
public interface IMediaSession : IDisposable
{
    VideoSourceKind Kind { get; }

    MediaSessionState State { get; }

    Task OpenAsync(CancellationToken cancellationToken);

    Task CloseAsync();

    event EventHandler<MediaSessionStateChangedEventArgs>? StateChanged;

    event EventHandler<EncodedPacketReceivedEventArgs>? PacketReceived;
}
