# SharpSnip

Windows screenshot tool with an **element picker**: hover any button, toolbar, pane or window, click, and capture exactly that rectangle. Region, window and full-screen (single monitor or all monitors, mixed DPI) are built in, plus a capture delay and a small annotation editor.

Offline only. No telemetry, no accounts, no network code.

> **Status: v0.9.0.** Compiles cleanly and the pure-logic layer is tested (339 checks). The owner field-ran the v0.5.0 release build on 2026-11-13 — everything works. New in 0.9.0: captures go straight to the clipboard and file by default (the editor opens after a capture only when that Settings option is ticked), and workflow `format:` steps convert recordings through the bundled FFmpeg. 0.8.x brought local workflows, animated APNG, recording output sizes and the FFmpeg export, plus the mode-bar fix (0.8.1, owner field report). The 0.7.0 recording suite and later additions have not yet had their full field pass; [docs/TEST-PLAN.md](docs/TEST-PLAN.md) lists one manual check per feature. See [AUDIT.md](AUDIT.md) for exactly what is verified and what is not.

## Features (v0.1)

| Mode | What it does |
|---|---|
| Element | UI Automation hit-test under the cursor. `Tab` next sibling, `PgUp`/`PgDn` parent/child, `Ctrl+Wheel` expand to container. Falls back to region when an app exposes no UIA. |
| Scrolling | Pick a scrollable element (tray → Scrolling capture…, or `C` in the mode menu) and capture its full content as one tall image — UIA ScrollPattern when available, wheel-and-stitch otherwise. |
| Region | Rubber band with 8 handles, live size read-out, magnifier (with a #hex colour readout), edge snapping to windows, move/resize after drawing. |
| Freehand | Draw an outline (`H` in the menu); the bounding box is captured and everything outside the outline is transparent. |
| Recording | Record a monitor or region to MP4 — system sound and microphone mixed in, optional webcam picture-in-picture, quality presets, time/size limits, one-shot scheduling — or straight to animated GIF. Tray → Record screen…, or `--record`. |
| Draw on screen | A live annotation overlay (tray item): draw over the screen; the strokes appear in captures and recordings. |
| Window | Window under cursor, full frame or client area only (`C`). |
| Full screen | One monitor, all monitors, or a chosen monitor (`1`..`9`). Mixed DPI and L-shaped layouts are stitched 1:1 in physical pixels. |
| One-shot full screen | Dedicated hotkey, no UI: capture straight to clipboard and file. |
| Active window / repeat last | Two more one-shot hotkeys: foreground window, or the previous rectangle again. |
| Delay | 0 / 3 / 5 / 10 s countdown for menus and tooltips. `Esc` cancels. The countdown chip never appears in the image. |
| Editor | Pen, highlighter, arrow, rectangle, ellipse, text, step markers, blur (pixelate), crop, undo/redo, copy, save, capture metadata panel — plus Copy text (built-in Windows OCR), one-click Redact of emails, cards, IPs and key-shaped tokens, OCR find & replace text, resize, watermark, brightness/contrast, an optional auto-applied effect, and whole-image invert, grayscale, flip and rotate. Recent captures reopen from the tray menu, and an external editor can be wired up in Settings. |

Every capture is placed on the clipboard (PNG + bitmap) immediately.

## Use

| Action | Default |
|---|---|
| Open mode menu | `Win+Shift+X` (or left-click the tray icon) |
| Full screen now | `Win+Shift+F` |
| Active window now | `Win+Shift+W` |
| Repeat last capture | `Win+Shift+R` |
| Capture monitor 1..4 | `Ctrl+Alt+Shift+1..4` (configurable, up to 9) |
| Fixed-size region | `S` in the menu |
| Interval capture | tray menu, or `--interval N --count M` (in GIF format the run becomes one looping `animation.gif`) |

The menu is a bar that slides down from the top of the screen — click a mode, or use its key: `E` element, `R` region, `W` window, `S` fixed-size region, `H` freehand, `F` all monitors, `D` this monitor, `1`..`9` monitor N, `Space` cycles delay, `Esc` cancels. Right-click acts as `Esc`. A preset region (Settings) can have its own one-shot hotkey too.

Hotkeys, output folder, format (PNG/JPEG/BMP/GIF/TIFF/PDF), delay and more are in tray → Settings. SharpSnip takes over the Print Screen key from Snipping Tool by default (untick it in Settings to hand the key and your old menu hotkey back), captures land in your Pictures folder unless you pick another, the Settings window follows the TesselGUI revision 2 design standard, and the tray icon can be hidden from its own menu — running SharpSnip again brings it back. Settings are plain JSON (`%APPDATA%\SharpSnip\settings.json`). Create a `data` folder next to `SharpSnip.exe` for portable mode: everything is then stored there.

### Command line

```
SharpSnip.exe --fullscreen [--all] [--delay N] [--out FILE] [--no-clipboard]
SharpSnip.exe --record N [--monitor K] [--fps F] [--quality Q] [--region X,Y,W,H] [--mic] [--no-audio] [--webcam] [--gif] [--max-mb M] [--at HH:MM] [--out FILE]
SharpSnip.exe --clean        # delete settings and logs
SharpSnip.exe --verbose | --version | --help
```

## Build

Needs the .NET 8 SDK on Windows 10 1809 or later (x64).

```
dotnet build src/SharpSnip.App -c Release
dotnet run --project tests/SharpSnip.SelfTest     # logic tests, runs anywhere
```

No third-party packages are used in SharpSnip's code (WPF, Windows Forms for the tray icon, UI Automation and Win32/WinRT only). Release builds bundle an LGPL build of FFmpeg for the export feature — a separate program you may replace or remove; see [THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES.md).

## Documents

[CHANGELOG](CHANGELOG.md) · [DESCRIPTION](DESCRIPTION.md) · [AUDIT](AUDIT.md) · [REVIEW](docs/REVIEW.md) · [CREDITS](CREDITS.md) · [ROADMAP](docs/ROADMAP.md) · [TEST-PLAN](docs/TEST-PLAN.md) · [BLUEPRINT](docs/BLUEPRINT.md)

## Screenshots

None yet. They must come from real runs on Windows; the exact capture list is in [docs/TEST-PLAN.md](docs/TEST-PLAN.md#screenshots-to-capture).

## License

MIT. See [LICENSE](LICENSE).
