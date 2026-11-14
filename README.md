# Snipit

Screenshot apps,\
elements, anything!

Windows screenshot tool with an **element picker**: hover any button, toolbar, pane or window, click, and capture exactly that rectangle. Region, window and full-screen (single monitor or all monitors, mixed DPI) are built in, plus a capture delay and a small annotation editor.

Offline only. No telemetry, no accounts, no network code.

> **Status v0.25.7.** Uninstall guidance for the Print Screen override. v0.25.6: Release workflow actions and the SeanPak packager are pinned. v0.25.5: Settings Save no longer resets effect settings; editor letter-key shortcuts can be turned off. v0.25.4: Muted text lightened to pass WCAG AA contrast. v0.25.3: Editor edits run off the UI thread; undo history capped at 512 MB. v0.25.2: Triple-Esc panic exit and visible contained errors. v0.25.1: Workflow trust hash is now keyed per user. v0.25.0: Region capture can combine several areas into one image (press A after each). v0.18.0 added a Cut out tool; the editor has a Cut out tool that removes a strip and joins the sides. v0.17.0 added an Effects… dialog for Frame, Shadow, Torn and Wave sizes. v0.16.0 renamed SharpSnip to Snipit. Earlier releases introduced the movable capture mode bar, colour picker, and Frame, Shadow, Torn and Wave editor effects. See [CHANGELOG.md](CHANGELOG.md) for version history and [AUDIT.md](AUDIT.md) for verification status.

## Uninstalling and Print Screen

While Snipit runs it switches off Windows' "Print Screen opens Snipping Tool" setting, and it hands the setting back when it exits, crashes or you log off. If Snipit was killed hard (power loss, Task Manager) the setting can stay off. **Before you uninstall, exit Snipit from its tray menu; if it was killed, run `Snipit.exe --restore-printscreen` first.** The SeanPak installer has no uninstall hook yet, so uninstalling cannot do this for you.

## Features

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
| Open mode menu | Print Screen or left-click the tray icon |
| Full screen | `D` for the current monitor, `F` for all monitors, or `1`–`9` for a numbered monitor in the mode bar |
| Active window | Window mode in the mode bar |
| Repeat last capture | Tray menu |
| Fixed-size region | `S` in the menu |
| Interval capture | tray menu, or `--interval N --count M` (in GIF format the run becomes one looping `animation.gif`) |

The menu is a bar that slides down from the top of the screen — click a mode, or use its key: `E` element, `R` region, `W` window, `S` fixed-size region, `H` freehand, `F` all monitors, `D` this monitor, `1`..`9` monitor N, `Space` cycles delay, `Esc` cancels. Right-click acts as `Esc`. A preset region (Settings) can be captured from the tray menu.

Output folder, format (PNG/JPEG/BMP/GIF/TIFF/PDF), delay and more are in tray → Settings. Print Screen is Snipit's only hotkey, and it is not a setting: while Snipit runs, Print Screen opens its menu instead of Snipping Tool's (the Windows switch for it is turned off at start), and quitting Snipit restores the switch to whatever it was before. Captures land in your Pictures folder unless you pick another, its windows follow the TesselGUI revision 3 (Carmine) design standard, and the tray icon can be hidden from its own menu — running Snipit again brings it back. Settings and logs use an existing `%APPDATA%\SharpSnip` directory, including any saved Print Screen recovery record. Fresh profiles use `%APPDATA%\Snipit`. A `data` folder beside `Snipit.exe` takes precedence for portable copies. Keep that folder alongside the new executable when switching a portable copy.

### Switching from SharpSnip

Close SharpSnip before opening Snipit. SeanPak installs Snipit as a separate app, so remove the old entry separately when you are ready. Before uninstalling an old copy with an outstanding Print Screen override, use its `--restore-printscreen` option. Update scripts and shortcuts to use `Snipit.exe`. Historical tags and downloads through v0.15.0 keep the SharpSnip name.

`Snipit.exe --clean` deletes settings and logs in the selected data directory. That includes the old `%APPDATA%\SharpSnip` directory when Snipit uses it.

### Command line

```
Snipit.exe --fullscreen [--all] [--delay N] [--out FILE] [--no-clipboard]
Snipit.exe --record N [--monitor K] [--fps F] [--quality Q] [--region X,Y,W,H] [--mic] [--no-audio] [--webcam] [--gif] [--max-mb M] [--at HH:MM] [--out FILE]
Snipit.exe --restore-printscreen   # hand the Print Screen key back to Windows (after a crash, or before uninstalling)
Snipit.exe --clean        # delete settings and logs
Snipit.exe --verbose | --version | --help
```

## Build

Needs the .NET 10 SDK on Windows 10 1809 or later (x64).

```
dotnet build src/Snipit.App -c Release
dotnet run --project tests/Snipit.SelfTest     # logic tests, runs anywhere
```

No third-party packages are used in Snipit's code (WPF, Windows Forms for the tray icon, UI Automation and Win32/WinRT only). Release builds bundle an LGPL build of FFmpeg for the export feature — a separate program you may replace or remove; see [THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES.md).

## Documents

[CHANGELOG](CHANGELOG.md) · [DESCRIPTION](DESCRIPTION.md) · [AUDIT](AUDIT.md) · [REVIEW](docs/REVIEW.md) · [CREDITS](CREDITS.md) · [ROADMAP](docs/ROADMAP.md) · [TEST-PLAN](docs/TEST-PLAN.md) · [BLUEPRINT](docs/BLUEPRINT.md)

## Screenshots

None yet. They must come from real runs on Windows; the exact capture list is in [docs/TEST-PLAN.md](docs/TEST-PLAN.md#screenshots-to-capture).

## License

MIT. See [LICENSE](LICENSE).
