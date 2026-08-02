namespace VSP.Player.Entities;

/// <summary>
/// Normalized error surface for the media pipeline. FFmpeg return codes and native error
/// strings are translated into this type at the VSP.Player boundary; no library-specific
/// error type or raw code is ever surfaced to <c>IMediaController</c> consumers.
/// </summary>
public sealed class MediaError
{
    public required MediaErrorCategory Category { get; init; }

    public required string Message { get; init; }

    public Exception? Exception { get; init; }

    public override string ToString() => $"[{Category}] {Message}";
}
