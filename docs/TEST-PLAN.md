# Test plan

## Automated (runs anywhere)
```
dotnet run --project tests/SharpSnip.SelfTest
```
76 checks over geometry, monitor stitching, target resolution, delay cycle, hotkey parsing, region hit-testing/resize/snap, state machine, settings load/save/quarantine and file naming. Last result: **76 passed, 0 failed**.

## Manual (needs real Windows hardware — not yet done)
Use a fresh profile (no `%APPDATA%\SharpSnip`). Record result as Verified / Partially verified / Unverified.

| # | Check | Pass when |
|---|---|---|
| 1 | Start `SharpSnip.exe`, press `Win+Shift+X` | Menu HUD in < 150 ms, tray icon present |
| 2 | Element mode on Chrome, Explorer, Notepad, VS Code, Win11 Settings | Highlight follows cursor, click captures exactly the rect (±1 px) |
| 3 | `Tab`, `PgUp`, `PgDn`, `Ctrl+Wheel` in element mode | Deterministic and reversible |
| 4 | Element mode over an app with no UIA (a game) | Switches to region picker, no error |
| 5 | Region: draw, move, resize by each handle, double-click, Enter | Correct rect; snap to window edges; loupe follows |
| 6 | Window mode, `C` toggle | Frame vs client area both correct (incl. Win11 shadow-less frame) |
| 7 | Full screen: `Tab`, `A`, `1`..`9`, `Enter` on 2 monitors | Correct monitor highlighted and captured only |
| 8 | All-monitors capture at 100% + 150% | One image, no gaps/overlap; pixel test at monitor seam |
| 9 | L-shaped arrangement | Union rect correct, gap filled with matte |
| 10 | One-shot hotkey `Win+Shift+F` | Clipboard image in < 250 ms, no UI flash |
| 11 | Delay 3/5/10 s, open a menu during countdown, `Esc` | Menu captured; `Esc` cancels; chip not in image |
| 12 | Unplug a monitor during countdown | No crash; capture continues |
| 13 | `Esc` from every picker, right-click | No leaked hook/timer/overlay (check Task Manager, input stays normal) |
| 14 | 60 s idle in the menu | Watchdog closes the session |
| 15 | Editor: each tool, undo/redo, crop+undo, blur, save, copy | Output matches canvas |
| 16 | Settings: bad hotkey, taken hotkey | Clear message, old hotkey keeps working |
| 17 | Corrupt `settings.json` | Moved to `.bad-*`, defaults load, balloon shown |
| 18 | `data` folder next to exe | Nothing written to `%APPDATA%` |
| 19 | Idle: 10 minutes in the tray | ~0% CPU, working set < 15 MB target |
| 20 | Screen reader (Narrator) on hint bar and countdown | Announcements heard |
| 21 | Windows 10 and 11, 100–200% scaling, light/dark/high contrast | Usable everywhere |

## Screenshots to capture
Real runs on Windows only, saved to `docs/screenshots/`:
1. `menu.png` — mode menu HUD over a desktop.
2. `element-picker.png` — highlight on a toolbar with the hint bar visible.
3. `region-picker.png` — region with handles, size tag and loupe.
4. `fullscreen-picker.png` — two monitors, chips and outline on the target.
5. `countdown.png` — delay chip in the corner.
6. `editor.png` — editor with annotations and the metadata panel.
7. `settings.png` — settings window.
