# Third-party notices

SharpSnip's own code uses **no third-party packages** — it is written
against the .NET 8 base libraries, WPF, Windows Forms (tray icon), UI
Automation and Win32/WinRT in-box APIs only.

Release builds bundle one third-party tool:

## FFmpeg (bundled executable)

- **What:** `ffmpeg/ffmpeg.exe` and its DLLs, shipped beside
  `SharpSnip.exe` and used only for the *Export with FFmpeg* feature
  (WebM/AV1/H.264/MKV/GIF/audio conversions of recordings).
- **Build:** `ffmpeg-n8.1.3-35-gf1acb0a266-win64-lgpl-shared-8.1` from
  the FFmpeg-Builds project — the **LGPL** variant (no GPL components
  such as libx264/libx265).
- **License:** GNU Lesser General Public License, version 2.1 or later.
  The full text ships beside the binary as `ffmpeg/LICENSE.txt` and is
  also available at <https://www.gnu.org/licenses/old-licenses/lgpl-2.1.html>.
- **Source:** the corresponding source for the exact build is available
  from the FFmpeg project at <https://ffmpeg.org/download.html> (release
  8.1.3 branch) and the build scripts at
  <https://github.com/FFmpeg-Builds/FFmpeg-Builds>. The pinned download
  and its SHA256 are recorded in `tools/ffmpeg.json`.
- **Your rights:** FFmpeg is a separate program, not part of SharpSnip.
  You may replace `ffmpeg/ffmpeg.exe` with your own build — SharpSnip
  runs whatever is there, or the path set under *Settings → FFmpeg
  override*. Removing the `ffmpeg` folder disables only the export
  feature; everything else in SharpSnip is unaffected.
- **Why it is bundled:** owner-approved dependency (2026-11-13) so the
  format breadth works out of the box, offline, with nothing to install.
