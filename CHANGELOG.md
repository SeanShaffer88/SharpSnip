# Changelog

Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning: [SemVer](https://semver.org/).

## [Unreleased]

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
