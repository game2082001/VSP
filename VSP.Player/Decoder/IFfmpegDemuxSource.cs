using FFmpeg.AutoGen.Abstractions;

namespace VSP.Player.Decoder;

/// <summary>
/// Internal seam shared by every FFmpeg-backed <see cref="Interfaces.IMediaSession"/>
/// implementation (<see cref="RtspMediaSession"/>, <see cref="RecordedFileMediaSession"/>) so
/// <see cref="FfmpegVideoDecoder"/> can decode from either without depending on a specific
/// concrete session type. Internal (not part of the public ADR-002 contracts) so no FFmpeg type
/// ever crosses out of VSP.Player's public surface.
/// </summary>
internal interface IFfmpegDemuxSource
{
    unsafe AVCodecParameters* GetVideoCodecParameters();

    unsafe AVRational GetVideoStreamTimeBase();
}
