# Changelog

Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning: [SemVer](https://semver.org/).

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
