using System.Runtime.InteropServices;
using FFmpeg.AutoGen.Bindings.DynamicallyLinked;

namespace VSP.Player.Decoder;

/// <summary>
/// One-time initialization of the FFmpeg native libraries bundled by DevEnvy.FFmpeg.Binaries.LGPL.
///
/// Uses <c>FFmpeg.AutoGen.Bindings.DynamicallyLinked</c> (compile-time DllImport against the
/// bundled DLLs' exact filenames, e.g. "avutil-60") rather than
/// <c>FFmpeg.AutoGen.Bindings.DynamicallyLoaded</c> (runtime <c>Marshal.GetDelegateForFunctionPointer</c>
/// resolution). The DynamicallyLoaded path was verified broken in this environment — every
/// function call, including the simplest possible (`avutil_version()`, zero arguments), throws
/// `NotSupportedException` from inside its generated resolver, independent of .NET version
/// (reproduced identically on net8.0 and net10.0), DLL search path, and library version
/// alignment. DynamicallyLinked bypasses that code path entirely and was confirmed working via
/// an isolated repro before adopting it here.
///
/// Uses <c>AddDllDirectory</c>/<c>SetDefaultDllDirectories</c>, not <c>SetDllDirectory</c>
/// (Epic-012 product validation): P/Invoke targets are resolved lazily, on first actual call, not
/// at the time this method runs -- so whatever directory <c>SetDllDirectory</c> set can be
/// silently replaced (it is process-global and non-additive, already-known debt) by anything else
/// in the process touching the DLL search state before that first real call happens. This was
/// confirmed live: <c>avformat_network_init()</c> (previously called here) hung indefinitely the
/// first time it ran inside the real VSP.UI.exe WPF process specifically -- never in the
/// automated test host or an isolated repro process. After removing that unnecessary call (see
/// below), the *next* first-use call (<c>avformat_alloc_context()</c>, now triggered later, from
/// Play rather than from app startup) instead failed fast with
/// <c>DllNotFoundException: avformat-62 ... (ERROR_MOD_NOT_FOUND)</c>, despite the directory
/// existing and <c>SetDllDirectory</c> itself having returned success moments earlier -- direct
/// evidence the search path set at init time was gone by the time it was actually needed.
/// <c>AddDllDirectory</c> is additive (paired with <c>SetDefaultDllDirectories</c> to opt into the
/// search order that consults it) and isn't vulnerable to being cleared this way.
///
/// Also no longer calls <c>avformat_network_init()</c>: FFmpeg's own documentation describes it as
/// optional network-library global init, kept only for older OpenSSL/GnuTLS thread-safety needs;
/// modern FFmpeg (this bundle) registers protocols and initializes network state on demand
/// without it, and this codebase never uses TLS-based camera protocols (RTSP here is always plain
/// TCP). Dropping it removes an unnecessary call without removing any capability this codebase
/// actually exercises.
/// </summary>
internal static class FfmpegNativeLibraryLoader
{
    private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;

    private static readonly object Gate = new();
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (Gate)
        {
            if (_initialized)
            {
                return;
            }

            var librariesPath = System.IO.Path.Combine(AppContext.BaseDirectory, "ffmpeg", "win-x64");

            // The bundled DLLs import each other (e.g. avformat -> avutil); this directory must
            // be on the search path for the OS loader to resolve those dependencies. Additive and
            // process-durable, unlike SetDllDirectory -- see the class-level remarks above.
            SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
            AddDllDirectory(librariesPath);

            _initialized = true;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetDefaultDllDirectories(uint directoryFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr AddDllDirectory(string newDirectory);
}
