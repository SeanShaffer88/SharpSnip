# Test plan

## Automated (runs anywhere)
```
dotnet run --project tests/SharpSnip.SelfTest
```
247 checks over geometry, monitor stitching, target resolution, delay cycle, hotkey parsing, region hit-testing/resize/snap, state machine, settings load/save/quarantine, file naming, image-format selection, GIF encoding, PDF writing, scroll stitching, OCR text assembly, redaction detection, pixel adjustments, Print Screen takeover planning and mode-bar layout. Last result: **247 passed, 0 failed**.

## Manual (needs real Windows hardware)
**Field pass 2026-11-13 (owner, v0.5.0 release build): "Everything works."** The tray menu (all entries) and the hotkey-conflict notification were observed in the owner's captures. The rows below were not re-run one by one; the table remains the checklist for a deep pass.
Use a fresh profile (no `%APPDATA%\SharpSnip`). Record result as Verified / Partially verified / Unverified.

| # | Check | Pass when |
|---|---|---|
| 1 | Start `SharpSnip.exe`, press `Win+Shift+X` | Mode bar slides in at the top of the screen, tray icon present |
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
| 22 | `Win+Shift+W` on Notepad, then on a window with an open menu and delay 3 | Window frame captured; own tray menu refused with a message |
| 23 | `Win+Shift+R` before any capture, then after a region capture | Message first; then same rectangle, even if windows moved |
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

## Unreleased manual rows (owner's v0.6.0 field fixes)
- [ ] Fresh profile (delete `%APPDATA%\SharpSnip\settings.json` first): starting SharpSnip applies the Print Screen takeover by itself — Print Screen opens the menu, the Windows switch is off, and unticking in Settings restores both.
- [ ] With no output folder set, a capture lands in the Pictures folder itself.
- [ ] Settings: Cancel closes the window; Esc closes it; Save applies and closes. The window renders in the dark TesselGUI theme (inputs, dropdowns, checkboxes, green Save) with readable text throughout.

## Unreleased manual rows (mode bar)
- [ ] Open the menu (hotkey or Print Screen with takeover on): the bar slides down from the top-centre of the monitor the cursor is on; hovering a button highlights it; clicking Element/Region/Window/Full screen/Scrolling starts that mode; clicking Delay cycles off → 3s → 5s → 10s and the label follows; ✕, Esc and right-click all cancel; on a second monitor the bar follows the cursor's monitor; at 150–200% scaling the bar is centred and its buttons hit where they are drawn.

## Unreleased manual rows (Snipping Tool replacement)
- [ ] Settings → tick "Print Screen replaces Snipping Tool" and Save: the menu hotkey becomes Print Screen, pressing Print Screen opens SharpSnip's menu (not Snipping Tool), and Windows Settings → Accessibility → Keyboard shows the Print-screen switch off.
- [ ] Untick it and Save: the Windows switch is back to its previous state and the menu hotkey is the key from before the takeover.
- [ ] With takeover on, run `SharpSnip.exe --clean` from a portable copy: the Windows Print-screen switch is restored before the settings are deleted.
- [ ] Tray menu → Hide tray icon: the icon disappears and captures still work from the hotkeys; running SharpSnip again from the Start menu brings the icon back and opens Settings.

## Unreleased manual rows (editor adjustments)
- [ ] Editor → Invert / Grayscale / Flip ↔ / Flip ↕ / Rotate on a capture with an annotation drawn first: each button transforms the whole picture (annotation baked in), Rotate swaps width/height, Undo restores the pre-transform image with its annotation editable again, and Save writes the transformed image.

## Unreleased manual rows (OCR)
- [ ] Editor → Redact on a capture containing an email, a test card number (4242 4242 4242 4242), an IP and an API-key-shaped token: each is covered by a black box, ordinary text is untouched, Undo removes the boxes one by one, and Save bakes the boxes into the file.
- [ ] Editor → Copy text on a capture of known on-screen text: the clipboard receives the text in reading order; on a text-free image the status says no text was found; with no OCR language installed the status explains how to add one.

## Unreleased manual rows (scrolling capture)
- [ ] Scrolling capture of a long web page / document (ScrollPattern path): result is one tall image, no duplicated or missing bands, and the page is left scrolled where it started.
- [ ] Scrolling capture of an app without ScrollPattern (wheel fallback): stitches correctly and stops at the end of content.
- [ ] Scrolling capture of a non-scrollable element degrades to a normal single capture; Esc cancels cleanly mid-run.

## Unreleased manual rows (export formats + animated GIF + reliability + PDF)
- [ ] Save a capture as PDF from Settings format, the editor, and `--out shot.pdf`: each opens in Edge and Acrobat, shows the capture at full size, and extracted/printed output matches the screen.
- [ ] Capture a region, restart SharpSnip, press the repeat-last hotkey: the same rectangle is captured. Then change the display layout so the rectangle is off-screen: a clear "off screen" message, no crash.
- [ ] Save two captures in rapid succession from the editor: both files exist (second name carries a `-2` suffix when the timestamp collides).
- [ ] Set format to GIF, run `--interval 2 --count 3` and a tray interval run: each produces one `animation.gif` that loops in a browser/image viewer with ~2 s between frames, and no `frame-*.gif` stills.
- [ ] Stop a tray GIF run early from the tray menu: the `animation.gif` from the frames so far is complete and playable (no truncated file).
- [ ] Set each format (png/jpg/bmp/gif/tif) in Settings, capture, and open the saved file: the image renders and its header matches its extension (no PNG bytes in a `.bmp`/`.gif`/`.tif` file).
- [ ] `--fullscreen --out shot.tiff` and `--out shot.bmp`: files open in an image viewer and match the requested format.
- [ ] A capture with transparent gap matte saved as jpg/bmp/gif has no transparent/black fringe artefacts; as png/tif the transparency survives.

## v0.3.0 manual rows (deferred by owner)
- [ ] `S` in menu: frame follows cursor, arrows nudge, Shift = 10 px, click/Enter saves exact W×H.
- [ ] Tray "Start interval capture": frames appear every N s; Stop ends it; summary toast shows count.
- [ ] `--interval 2 --count 3`: three files written, exit 0.
- [ ] `Ctrl+Alt+Shift+2` captures monitor 2; with one monitor shows "not connected".
- [ ] Settings: bad size/interval/duplicate hotkey rejected; old hotkeys restored on conflict.
