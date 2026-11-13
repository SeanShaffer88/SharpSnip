# SharpSnip

Windows screenshot tool with an **element picker**: hover any button, toolbar, pane or window, click, and capture exactly that rectangle. Region, window and full-screen (single monitor or all monitors, mixed DPI) are built in, plus a capture delay and a small annotation editor.

Offline only. No telemetry, no accounts, no network code.

> **Status: v0.6.0.** Compiles cleanly and the pure-logic layer is tested (247 checks). The owner field-ran the v0.5.0 release build on 2026-11-13 — everything works; the deep manual checklist in [docs/TEST-PLAN.md](docs/TEST-PLAN.md) has not been re-run row by row. See [AUDIT.md](AUDIT.md) for exactly what is verified and what is not.

## Features (v0.1)

| Mode | What it does |
|---|---|
| Element | UI Automation hit-test under the cursor. `Tab` next sibling, `PgUp`/`PgDn` parent/child, `Ctrl+Wheel` expand to container. Falls back to region when an app exposes no UIA. |
| Scrolling | Pick a scrollable element (tray → Scrolling capture…, or `C` in the mode menu) and capture its full content as one tall image — UIA ScrollPattern when available, wheel-and-stitch otherwise. |
| Region | Rubber band with 8 handles, live size read-out, magnifier, edge snapping to windows, move/resize after drawing. |
| Window | Window under cursor, full frame or client area only (`C`). |
| Full screen | One monitor, all monitors, or a chosen monitor (`1`..`9`). Mixed DPI and L-shaped layouts are stitched 1:1 in physical pixels. |
| One-shot full screen | Dedicated hotkey, no UI: capture straight to clipboard and file. |
| Active window / repeat last | Two more one-shot hotkeys: foreground window, or the previous rectangle again. |
| Delay | 0 / 3 / 5 / 10 s countdown for menus and tooltips. `Esc` cancels. The countdown chip never appears in the image. |
| Editor | Pen, highlighter, arrow, rectangle, ellipse, text, blur (pixelate), crop, undo/redo, copy, save, capture metadata panel — plus Copy text (built-in Windows OCR), one-click Redact of emails, cards, IPs and key-shaped tokens, and whole-image invert, grayscale, flip and rotate. |

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

The menu is a bar that slides down from the top of the screen — click a mode, or use its key: `E` element, `R` region, `W` window, `S` fixed-size region, `F` all monitors, `D` this monitor, `1`..`9` monitor N, `Space` cycles delay, `Esc` cancels. Right-click acts as `Esc`.

Hotkeys, output folder, format (PNG/JPEG/BMP/GIF/TIFF/PDF), delay and more are in tray → Settings. Settings can also hand SharpSnip the Print Screen key so it replaces Snipping Tool (switch it back off there to restore), and the tray icon can be hidden from its own menu — running SharpSnip again brings it back. Settings are plain JSON (`%APPDATA%\SharpSnip\settings.json`). Create a `data` folder next to `SharpSnip.exe` for portable mode: everything is then stored there.

### Command line

```
SharpSnip.exe --fullscreen [--all] [--delay N] [--out FILE] [--no-clipboard]
SharpSnip.exe --clean        # delete settings and logs
SharpSnip.exe --verbose | --version | --help
```

## Build

Needs the .NET 8 SDK on Windows 10 1809 or later (x64).

```
dotnet build src/SharpSnip.App -c Release
dotnet run --project tests/SharpSnip.SelfTest     # logic tests, runs anywhere
```

No third-party packages are used (WPF, Windows Forms for the tray icon, UI Automation and Win32 only).

## Documents

[CHANGELOG](CHANGELOG.md) · [DESCRIPTION](DESCRIPTION.md) · [AUDIT](AUDIT.md) · [REVIEW](docs/REVIEW.md) · [CREDITS](CREDITS.md) · [ROADMAP](docs/ROADMAP.md) · [TEST-PLAN](docs/TEST-PLAN.md) · [BLUEPRINT](docs/BLUEPRINT.md)

## Screenshots

None yet. They must come from real runs on Windows; the exact capture list is in [docs/TEST-PLAN.md](docs/TEST-PLAN.md#screenshots-to-capture).

## License

MIT. See [LICENSE](LICENSE).
