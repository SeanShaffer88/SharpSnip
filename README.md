# SharpSnip

Windows screenshot tool with an **element picker**: hover any button, toolbar, pane or window, click, and capture exactly that rectangle. Region, window and full-screen (single monitor or all monitors, mixed DPI) are built in, plus a capture delay and a small annotation editor.

Offline only. No telemetry, no accounts, no network code.

> **Status: v0.11.0.** Compiles cleanly and the pure-logic layer is tested (374 checks). 0.11.0 is a hardening release from a full code review: the Print Screen switch is restored after crashes, recordings are never lost silently, focus rings and screen-reader names are fixed, the project moves to .NET 10, and workflows are protected against settings edited outside the app. The owner field-ran the v0.5.0 release build on 2026-11-13 — everything works. New in 0.9.2: captures never open the editor (it is for deliberate opens from the capture history only), Settings saves apply per-field instead of all-or-nothing, the Recorder picks its own region, recording audio mixes by streaming, and the capture pipeline pre-warms at startup. 0.9.0/0.9.1 sent captures straight to clipboard + file by default, let workflow `format:` steps convert recordings through the bundled FFmpeg, and fixed mode-bar tool selection after owner field reports. 0.8.x brought local workflows, animated APNG, recording output sizes and the FFmpeg export. The 0.7.0 recording suite and later additions have not yet had their full field pass; [docs/TEST-PLAN.md](docs/TEST-PLAN.md) lists one manual check per feature. See [AUDIT.md](AUDIT.md) for exactly what is verified and what is not.

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
| One-shot full screen | From the command line (`--fullscreen`, also `--all` / `--monitor N`): capture straight to clipboard and file, no UI. |
| Active window | The foreground window, from the menu's Window mode. |
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

The menu is a bar that slides down from the top of the screen — click a mode, or use its key: `E` element, `R` region, `W` window, `S` fixed-size region, `H` freehand, `F` all monitors, `D` this monitor, `1`..`9` monitor N, `Space` cycles delay, `Esc` cancels. Right-click acts as `Esc`. A preset region (Settings) can be captured from the tray menu.


### Command line

```
SharpSnip.exe --fullscreen [--all] [--delay N] [--out FILE] [--no-clipboard]
SharpSnip.exe --record N [--monitor K] [--fps F] [--quality Q] [--region X,Y,W,H] [--mic] [--no-audio] [--webcam] [--gif] [--max-mb M] [--at HH:MM] [--out FILE]
SharpSnip.exe --restore-printscreen   # hand the Print Screen key back to Windows (after a crash, or before uninstalling)
SharpSnip.exe --clean        # delete settings and logs
SharpSnip.exe --verbose | --version | --help
```

## Build

Needs the .NET 10 SDK on Windows 10 1809 or later (x64).

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
