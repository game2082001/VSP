# Third-Party Notices

This file records third-party binaries redistributed with VSP that are not already
documented by their own NuGet package metadata, along with the exact source, version,
and integrity information needed to reproduce or audit them.

---

## MinGW-w64 runtime DLLs (win-x64)

`VSP.Player/Native/mingw-w64/win-x64/` bundles three MinGW-w64 (GCC) runtime DLLs required
by the win-x64 FFmpeg shared libraries redistributed via the `DevEnvy.FFmpeg.Binaries.LGPL`
NuGet package (`VSP.Player.csproj`). Those FFmpeg binaries (`avcodec-62.dll`, `avutil-60.dll`,
`avfilter-11.dll`) are themselves built with MinGW-w64 GCC and import `libstdc++-6.dll` at
load time (confirmed via PE import-table inspection); `libstdc++-6.dll` in turn depends on
`libgcc_s_seh-1.dll` and `libwinpthread-1.dll`. The FFmpeg package does not ship these three
runtime DLLs, so they are packaged here from an independent, traceable MinGW-w64 distribution
and copied alongside the FFmpeg DLLs into `ffmpeg/win-x64/` at build and publish time (see
`VSP.Player.csproj`).

All three files were downloaded from the official MSYS2 `mingw64` package repository
(`https://repo.msys2.org/mingw/mingw64/`), a standard, publicly traceable MinGW-w64 toolchain
distribution. Each package's SHA-256 was verified against the published MSYS2 repository
database (`mingw64.db`), and each package's detached GPG signature was verified against the
official `msys2-keyring` package (signed by MSYS2 packager Christoph Reiter, key fingerprint
`5F94 4B02 7F7F E209 1985 AA2E FA11 531A A0AA 7F57`) before extraction. Nothing was copied from
Git for Windows' bundled MinGW runtime, Anaconda, or any other locally installed application.

### libstdc++-6.dll

- Source distribution: MSYS2 `mingw64` repository
- Package: `mingw-w64-x86_64-gcc-libs`
- Package URL: `https://repo.msys2.org/mingw/mingw64/mingw-w64-x86_64-gcc-libs-16.1.0-5-any.pkg.tar.zst`
- Package version: `16.1.0-5` (GCC 16.1.0, mingw-w64 packaging revision 5)
- Architecture: x86_64 (win-x64)
- Package SHA-256: `aa560f5438c35b71c3e7b24fd5becbca028f70c5b4d1f1697a86ff80fec947da`
- Extracted file SHA-256 (`libstdc++-6.dll`): `3529d11c422b2aaf0bbad7221bf62ba7b5a39f854e89a015bb58b6b55a677da1`
- License: GPL-3.0-or-later WITH GCC Runtime Library Exception 3.1 (see below)
- Upstream project: GCC (`https://gcc.gnu.org`)

### libgcc_s_seh-1.dll

- Source distribution: MSYS2 `mingw64` repository
- Package: `mingw-w64-x86_64-gcc-libs`
- Package URL: `https://repo.msys2.org/mingw/mingw64/mingw-w64-x86_64-gcc-libs-16.1.0-5-any.pkg.tar.zst`
- Package version: `16.1.0-5` (GCC 16.1.0, mingw-w64 packaging revision 5)
- Architecture: x86_64 (win-x64), SEH exception model (standard for x86_64 MinGW-w64)
- Package SHA-256: `aa560f5438c35b71c3e7b24fd5becbca028f70c5b4d1f1697a86ff80fec947da` (same package as libstdc++-6.dll above)
- Extracted file SHA-256 (`libgcc_s_seh-1.dll`): `278f1101f2371c58b6b412a3206718e22343940a5164aceb4fb75f2bf3b38ed4`
- License: GPL-3.0-or-later WITH GCC Runtime Library Exception 3.1 (see below)
- Upstream project: GCC (`https://gcc.gnu.org`)

### libwinpthread-1.dll

- Source distribution: MSYS2 `mingw64` repository
- Package: `mingw-w64-x86_64-libwinpthread`
- Package URL: `https://repo.msys2.org/mingw/mingw64/mingw-w64-x86_64-libwinpthread-14.0.0.r220.gd999af622-1-any.pkg.tar.zst`
- Package version: `14.0.0.r220.gd999af622-1` (mingw-w64 winpthreads, git-describe versioned)
- Architecture: x86_64 (win-x64)
- Package SHA-256: `16bf944184656c6976b3c8b9ca872da49560d8b7cced36a63fe6638c6e63ae45`
- Extracted file SHA-256 (`libwinpthread-1.dll`): `6a661d5846d80a91394dbb9b2dab87ba3cc705eac80ad45fe73677bff70cd6d2`
- License: MIT AND BSD-3-Clause-Clear (see "mingw-w64 winpthreads" below)
- Upstream project: mingw-w64 (`https://www.mingw-w64.org/`)

---

## GCC Runtime Library

`libstdc++-6.dll` and `libgcc_s_seh-1.dll` are parts of the GCC Runtime Library (libstdc++ and
libgcc respectively), copyright the Free Software Foundation and GCC contributors, licensed
under the GNU General Public License version 3 (or later), **with the GCC Runtime Library
Exception**.

- Project: GCC (GNU Compiler Collection)
- Website: https://gcc.gnu.org
- License: GPL-3.0-or-later WITH GCC-exception-3.1
- License text: https://www.gnu.org/licenses/gpl-3.0.html

## GCC Runtime Library Exception

The GCC Runtime Library Exception (version 3.1) is what permits linking and redistributing
compiled binaries against the GCC Runtime Library (libgcc, libstdc++, and related runtime
components) without the resulting binary being subject to the GPL's copyleft terms, provided
the runtime library components themselves are not modified in a way the exception excludes.
VSP redistributes unmodified, officially built `libstdc++-6.dll` and `libgcc_s_seh-1.dll`
binaries only.

- Full text: https://www.gnu.org/licenses/gcc-exception-3.1.html

## mingw-w64 winpthreads

`libwinpthread-1.dll` implements a POSIX threads (pthreads) API on top of the native Windows
threading API, used by the MinGW-w64 toolchain's runtime (including libstdc++'s `std::thread`,
mutexes, and condition variables) on Windows targets.

- Project: mingw-w64
- Website: https://www.mingw-w64.org/
- License: MIT AND BSD-3-Clause-Clear
- Source: https://github.com/mingw-w64/mingw-w64 (`mingw-w64-libraries/winpthreads`)

---

## FFmpeg

FFmpeg native binaries (`ffmpeg/win-x64/`, etc.) are redistributed via the `DevEnvy.FFmpeg.Binaries.LGPL`
NuGet package. See that package's own `THIRD_PARTY_NOTICES.md` (included in the NuGet package
contents) for FFmpeg's license posture and hardware-acceleration header attributions.
