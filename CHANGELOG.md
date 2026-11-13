# Changelog

Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning: [SemVer](https://semver.org/).

## [0.9.0] - 2026-11-13

### Changed
- **Captures no longer open the editor by default** (owner direction): a capture goes straight to the clipboard and its file, with a toast. The editor opens after a capture only when "Open the editor after a capture" is ticked in Settings. Existing installs keep whatever their saved setting says — untick it once in Settings to get the new behavior.

### Added
- **Workflows can convert recordings**: a `format:` step in a recording workflow now names an FFmpeg preset (`format: webm`, also av1, h264, mkv, gifhq, mp3, m4a) and converts through the bundled FFmpeg, continuing the chain with the converted file — so "record → make a WebM → copy it to the share folder" is one hands-free chain. Still workflows keep using image formats, and mismatches (a preset on a still, an image format on a recording, FFmpeg missing) stop the chain with a plain reason.

## [0.8.1] - 2026-11-13

### Fixed
- **Mode bar no longer captures by itself** (owner field report on v0.8.0): every picker confirms on mouse-button release, so the release of the very click that chose a tool in the bar landed in the new picker as its capture click — choosing Window instantly "captured" the window under the bar. A picker that takes over while the button is still held now swallows that stranded release; tools arm on the bar click and capture on the next full click, as expected.

## [0.8.0] - 2026-11-13

### Added
- **Workflows** (ShareX-style after-capture chains, fully local): named step lists — copy, format, effect (Border/Grayscale), external editor, run a program with %path%/%dir%/%name%/%ext% — assigned to captures and recordings in Settings, managed in their own window, and runnable by hand with `--run-workflow NAME --file PATH`.
- **Animated APNG**: interval captures can save APNG alongside GIF (hand-written APNG writer; a still .apng export is a proper one-frame APNG).
- **Recording output size**: Original / 1080p / 720p / 480p cap in the recorder window and as a setting; never upscales; applies to MP4 and GIF recordings (`--size` on the CLI).
- **FFmpeg export** (bundled LGPL FFmpeg, owner-approved): tray *Export with FFmpeg…* converts recordings and GIFs to WebM (VP9), AV1, H.264 MP4 (Windows encoder), lossless MKV, palette GIF, MP3 or M4A, with a custom-arguments mode for a user-supplied build; `--export PRESET --file PATH [--out PATH]` on the CLI; Settings FFmpeg override path; license and source notice in THIRD-PARTY-NOTICES.md. The SharpSnip code itself still uses no third-party packages.

## [0.7.0] - 2026-11-13

### Added
- Integration pack: **Recent captures** in the tray menu (the newest stills and recordings from the output folder — stills reopen in the editor, recordings play in your default player) and an **External editor…** button in the editor (path set in Settings; saves, then opens the file in that program). Live screen drawing arrived with the recording extras.
- Capture completion pack: **Freehand capture** (mode bar or H — draw an outline; the bounding box is captured and everything outside the outline becomes transparent), a **preset-region hotkey** (set the region in Settings from your last capture, give it a hotkey, or use the tray's "Capture preset region"), and a **loupe colour readout** — the magnifier now shows the pixel under the cursor as #RRGGBB, its RGB values and its screen position.
- Editor completion pack: **Step tool** (click to drop numbered markers 1, 2, 3…), **Resize…** (new pixel dimensions with aspect lock), **Watermark…** (corner or tiled diagonal text), **Brightness/Contrast sliders** with Apply B/C, **Replace text…** (OCR find & replace — the original is covered with the surrounding colour and the new text drawn in), and an **Auto effect** setting (None / Border / Grayscale) applied when a capture opens. All are undoable like every other edit. (Also fixed: saving Settings no longer resets the recording preferences to defaults.)
- GIF recording: the Recorder window's Format picker (or `--gif`) records straight to an animated GIF with real frame timing — silent, capped at 20 fps, for short shareable clips.
- Draw on screen: a live annotation overlay (tray item, or the Recorder window's button) — draw with the mouse over any monitor; the strokes appear in captures and recordings as they happen. Keys 1–5 change the pen, C clears, Esc finishes.
- Recording limits and scheduling: optional max-minutes and max-size stops (Recorder window fields, CLI `--max-mb`), and a one-shot scheduled recording — set a start time and duration in the Recorder window, or `--record N --at HH:MM` on the command line.
- Webcam picture-in-picture for recordings (Recorder window checkbox + corner picker, CLI `--webcam [--webcam-corner C]`), composited with a tested layout engine, plus recording quality presets (Low / Medium / High / Ultra) in the window and via `--quality`.
- Recording audio: system sound (WASAPI loopback) is recorded by default and the microphone can join it (Recorder window checkboxes, CLI `--mic` / `--no-audio`); the two tracks are mixed and muxed into the MP4 when you stop. A missing audio device never blocks a recording — that source simply drops out.
- Screen recording: `--record N [--monitor K] [--fps F] [--region X,Y,W,H] [--out FILE]` records a monitor — or one rectangle of it — to MP4 (H.264, hardware-accelerated MediaTranscoder over the WGC stream, cursor included). In the app, the tray's "Record screen…" opens a small recording window (monitor picker, "last capture region" option, frame rate, Start/Stop with elapsed time) and "Stop recording" finishes from the tray; recordings land in the output folder with a toast.
- Windows.Graphics.Capture backend for stills (owner-approved program: completion pack + recording suite, 2026-11-13 — this is its foundation slice). Captures now try WGC first and fall back to GDI BitBlt automatically, with the serving backend named in the log. WGC keeps working where GDI returns black — hardware overlays, DRM video, some games. A WGC frame that arrives entirely black is treated as a failure, not a picture, and also falls back. The frame-assembly copy (`PixelBlit`) is unit-tested in the Core layer.

## [0.6.1] - 2026-11-13

### Changed
- Print Screen replaces Snipping Tool **by default**: on a first run (or any profile that never stored the choice) SharpSnip now applies the takeover itself at startup — the live Windows switch value is recorded, the switch turned off, and Print Screen becomes the menu hotkey. Unticking the Settings option still hands everything back exactly as before.
- The default output folder is now the user's Pictures folder itself (was a SharpSnip subfolder inside it). A folder set in Settings still wins.
- The Settings window follows the TesselGUI revision 2 design standard (GitHub scheme): dark surfaces, 1px edges, Segoe UI, green Save action, themed text boxes, combo boxes and check boxes. The OS title bar is unchanged.

### Fixed
- Settings: the Cancel button did nothing. The window is shown modeless, where the old `IsCancel`/`DialogResult` wiring is inert — Cancel (and Esc) now close the window explicitly, and Save closes it the same way after applying.

## [0.6.0] - 2026-11-13

### Added
- Mode bar: the mode menu is now a Snipping-Tool-style tab that slides down from the top-centre of the screen (design picked by the owner from rendered mockups) — icon buttons with labels underneath for Element, Region, Window, Full screen (this monitor), Scrolling and Delay (click cycles it), plus a close button. It replaces the text HUD; every keyboard shortcut works exactly as before (E/R/W/S/F/D/C, 1..9, Space, Esc), and Fixed-size region stays on its `S` key.
- Settings → "Print Screen replaces Snipping Tool": turns off the Windows switch that gives Print Screen to Snipping Tool and makes Print Screen SharpSnip's menu hotkey. The previous switch value and menu hotkey are remembered and restored when the option is unticked, and `--clean` restores them too.
- Tray icon can be hidden from its own context menu ("Hide tray icon"). Running SharpSnip again while it is running in the background brings the icon back and opens Settings.

## [0.5.0] - 2026-11-13

### Added
- Editor whole-image adjustments: Invert, Grayscale, Flip horizontal, Flip vertical and Rotate 90° buttons. Each applies to the flattened image and swaps it in as the new base (annotations baked in, exactly like Crop), and Undo restores the previous image. The pixel math lives in the tested pure-logic layer (`PixelAdjust`), which also carries a brightness/contrast operation whose editor control is still to come.
- Editor "Redact": one click finds sensitive text in the capture — emails, URLs, payment cards (Luhn-validated, so random numbers are left alone), IPv4 addresses, phone numbers, and long letter+digit tokens shaped like API keys — and covers each with a black box annotation (undoable like any other edit). Detection and span-to-box mapping live in the tested pure-logic layer (`Redactor`), reusing the OCR line grouping; it runs fully offline.
- Editor "Copy text": recognises the text in the capture with the OCR engine built into Windows (Windows.Media.Ocr, offline, no package) and copies it to the clipboard in reading order. Reading-order assembly (line grouping, ordering) lives in the tested pure-logic layer (`OcrText`); when no OCR language is installed the status line says so and how to add one.
- Scrolling capture: pick a scrollable element (tray → Scrolling capture…, or [C] in the mode menu) and SharpSnip captures its full content as one tall image. Uses the element's UIA ScrollPattern when available (planned strip offsets, exact composition, original scroll position restored afterwards); otherwise falls back to synthetic wheel scrolling stitched by measured strip overlap, stopping at the end of content. Bounded at 40 strips / 32,767 px. Strip planning, composition and overlap detection live in the tested pure-logic layer (`ScrollStitch`).

## [0.4.0] - 2026-11-13

### Added
- PDF export: captures can be saved as a single-image PDF (Settings format, editor save, `--out file.pdf`) — hand-written writer in the pure-logic layer (`PdfWriter`, no package), pixels embedded losslessly (Flate), page sized to the image at 96 dpi. Cross-verified with Poppler: `pdfinfo` reads the metadata/page size and `pdfimages` extracts the embedded image pixel-exact.
- CI workflow (`.github/workflows/ci.yml`): build + self-test on windows-latest for pushes and PRs. Takes effect when these commits are pushed; AUDIT.md notes workflow files on this account may need a browser-side approval.
- Animated GIF for interval capture: when the format is GIF, an interval run (tray or `--interval`) now produces one looping `animation.gif` — frame delay equals the interval — instead of a folder of still GIFs. Frames are quantised by WPF to a 256-colour palette and streamed through a hand-written GIF89a writer in the pure-logic layer (`GifWriter`, no package), so memory stays at one frame. The writer's output was cross-verified with an independent decoder (Pillow) as well as the self-test's own decoder. A single capture in GIF format remains a still image.
- Export formats BMP, GIF and TIFF (in-box WPF encoders), alongside PNG and JPEG: selectable in Settings, honoured by interval capture, and chosen by extension for `--out FILE`. Previously any `--out` path that was not `.jpg` was silently written as PNG bytes under that name; the written bytes now always match the file name.
- Format policy lives in the tested pure-logic layer (`ImageFormats`): aliases canonicalise (`jpeg` → `jpg`, `tiff` → `tif`, case-insensitive), unsupported formats fall back to PNG in settings, and JPEG/BMP/GIF flatten alpha while PNG/TIFF keep it.

### Changed
- One existing self-test fixture used `gif` as an example of an unsupported format; it now uses `webp` (still unsupported — WPF has no WebP encoder).
- Self-test: 210 checks.

### Fixed
- "Repeat last" now survives a restart: the last capture rectangle is stored in settings (validated on load; re-checked against the current desktop before use, as before).
- Auto-generated save names can no longer overwrite an earlier capture when two saves land in the same millisecond; they gain a `-2`, `-3`, ... suffix instead. Paths named explicitly with `--out` still overwrite, as asked.
- Capture post-processing uses one pixel buffer instead of three full-frame copies, and the alpha/matte logic moved into the tested pure-logic layer (`CapturePixels`).
- Daily log files are now pruned after 30 days (previously they accumulated forever; `--clean` still removes everything).

## [0.3.0] - 2026-11-12

### Added
- Fixed-size region mode (`S` in the menu): a W×H frame follows the cursor, arrows nudge 1 px (Shift = 10), click or Enter captures. Size set in Settings (default 1280×720).
- Interval capture: tray toggle, or `--interval N --count M [--all | --monitor K] [--out DIR]`. Frames go to a new `Interval-<time>` folder as `frame-00001`, ... No clipboard. Stops after 3 failed frames in a row.
- Per-monitor hotkeys: one hotkey per monitor, default `Ctrl+Alt+Shift+1..4` (Win+Shift+digit is reserved by Windows). Up to 9, set in Settings.
- CLI `--monitor N` for `--fullscreen`.
- Settings: monitor hotkeys, fixed width/height, interval seconds. All hotkeys checked for duplicates together.
- Self-test: 98 checks.

## [0.2.0] - 2026-11-12

### Added
- Active-window hotkey (`Win+Shift+W`): foreground window frame straight to clipboard and file.
- Repeat-last hotkey (`Win+Shift+R`, also in the tray menu): same screen rectangle as the previous capture (kept for the session).
- Both honour the default delay and re-read the target after the countdown.
- Settings: two new hotkey fields; all four hotkeys are checked for validity and duplicates, and a taken hotkey restores the previous set.
- Self-test: 4 new checks (80 total), including loading a v0.1 settings file.

### Changed
- Instant full-screen, active-window and repeat-last share one capture pipeline.

## [0.1.0] - 2026-11-12

First build, implementing blueprint v2.

### Added
- Element picker on UI Automation with sibling/parent/child/container navigation and region fallback.
- Region picker: 8 handles, move/resize, size read-out, magnifier, window-edge snapping.
- Window picker with client-area toggle.
- Full-screen picker (current / all / numbered monitor) and one-shot full-screen hotkey.
- Capture delay 0/3/5/10 s with cancellable countdown chip excluded from capture.
- Multi-monitor, mixed-DPI stitching in physical pixels; matte fill for L-shaped layouts.
- Editor: pen, highlighter, arrow, rectangle, ellipse, text, blur, crop, undo/redo, copy, save, metadata panel.
- Clipboard (PNG + bitmap), atomic PNG/JPEG export, timestamped file names.
- Tray icon, settings window, JSON settings with quarantine of corrupt files, portable mode, local logs.
- Command line: `--fullscreen`, `--clean`, `--verbose`, `--version`, `--help`.
- Dependency-free self-test project (76 checks) for all pure logic.

### Known gaps
See [AUDIT.md](AUDIT.md): Windows.Graphics.Capture, WebP, HDR badge, DRM detection, Windows share sheet.
