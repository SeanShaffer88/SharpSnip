# SharpSnip

Windows screenshot tool with an **element picker**: hover any button, toolbar, pane or window, click, and capture exactly that rectangle. Region, window and full-screen (single monitor or all monitors, mixed DPI) are built in, plus a capture delay and a small annotation editor.

Offline only. No telemetry, no accounts, no network code.

> **Status: v0.2.0.** Compiles cleanly and the pure-logic layer is tested (80 checks), but it has **not yet been run on a real Windows machine**. See [AUDIT.md](AUDIT.md) for exactly what is verified and what is not.

## Features (v0.1)

| Mode | What it does |
|---|---|
| Element | UI Automation hit-test under the cursor. `Tab` next sibling, `PgUp`/`PgDn` parent/child, `Ctrl+Wheel` expand to container. Falls back to region when an app exposes no UIA. |
| Region | Rubber band with 8 handles, live size read-out, magnifier, edge snapping to windows, move/resize after drawing. |
| Window | Window under cursor, full frame or client area only (`C`). |
| Full screen | One monitor, all monitors, or a chosen monitor (`1`..`9`). Mixed DPI and L-shaped layouts are stitched 1:1 in physical pixels. |
| One-shot full screen | Dedicated hotkey, no UI: capture straight to clipboard and file. |
| Active window / repeat last | Two more one-shot hotkeys: foreground window, or the previous rectangle again. |
| Delay | 0 / 3 / 5 / 10 s countdown for menus and tooltips. `Esc` cancels. The countdown chip never appears in the image. |
| Editor | Pen, highlighter, arrow, rectangle, ellipse, text, blur (pixelate), crop, undo/redo, copy, save, capture metadata panel. |

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
| Interval capture | tray menu, or `--interval N --count M` |

In the menu: `E` element, `R` region, `W` window, `F` all monitors, `D` this monitor, `1`..`9` monitor N, `Space` cycles delay, `Esc` cancels. Right-click acts as `Esc`.

Hotkeys, output folder, format (PNG, JPG, BMP, TIFF, GIF, PDF), file-name and sub-folder patterns, delay and more are in tray → Settings. Settings are plain JSON (`%APPDATA%\SharpSnip\settings.json`). Create a `data` folder next to `SharpSnip.exe` for portable mode: everything is then stored there.

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

[CHANGELOG](CHANGELOG.md) · [DESCRIPTION](DESCRIPTION.md) · [AUDIT](AUDIT.md) · [CREDITS](CREDITS.md) · [ROADMAP](docs/ROADMAP.md) · [TEST-PLAN](docs/TEST-PLAN.md) · [BLUEPRINT](docs/BLUEPRINT.md)

## Screenshots

None yet. They must come from real runs on Windows; the exact capture list is in [docs/TEST-PLAN.md](docs/TEST-PLAN.md#screenshots-to-capture).

## License

MIT. See [LICENSE](LICENSE).
