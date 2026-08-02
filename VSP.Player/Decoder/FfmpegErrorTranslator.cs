using FFmpeg.AutoGen.Bindings.DynamicallyLinked;
using VSP.Player.Entities;

namespace VSP.Player.Decoder;

/// <summary>
/// Translates FFmpeg result codes to the normalized <see cref="MediaError"/> type at the
/// VSP.Player boundary, so no raw FFmpeg code/type crosses out of the Decoder namespace.
/// </summary>
internal static unsafe class FfmpegErrorTranslator
{
    public static MediaError FromResult(MediaErrorCategory category, int errorCode, string context)
    {
        const int bufferSize = 1024;
        var buffer = stackalloc byte[bufferSize];
        DynamicallyLinkedBindings.av_strerror(errorCode, buffer, bufferSize);
        var nativeMessage = System.Runtime.InteropServices.Marshal.PtrToStringAnsi((IntPtr)buffer);

        return new MediaError
        {
            Category = category,
            Message = string.IsNullOrEmpty(nativeMessage)
                ? $"{context} (FFmpeg error code {errorCode})"
                : $"{context}: {nativeMessage} (code {errorCode})"
        };
    }
}
