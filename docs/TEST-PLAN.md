# Test plan

## Automated (runs anywhere)
```
dotnet run --project tests/Snipit.SelfTest
```
345 checks over geometry, monitor stitching, target resolution, delay cycle, region hit-testing/resize/snap, state machine, settings load/save/quarantine, file naming, image-format selection, GIF encoding, APNG writing, PDF writing, scroll stitching, OCR text assembly, redaction detection, pixel adjustments (incl. borders), the Print Screen override record, mode-bar layout, pixel blitting, recording planning, quality presets, recording output sizes and limits, WAV writing, PCM mixing/resampling, scaled blitting, webcam overlay layout, resize/watermark planning, OCR text-replace matching, polygon masking, the loupe colour readout format, workflow step parsing/expansion, workflow FFmpeg routing, the FFmpeg export plans, the click gate, recording-region resolution, pause timing and WAV segment joining. Last result: **345 passed, 0 failed**.

## Manual (needs real Windows hardware)
**Field pass 2026-11-13 (owner, v0.5.0 release build): "Everything works."** The tray menu (all entries) and the hotkey-conflict notification were observed in the owner's captures. The rows below were not re-run one by one; the table remains the checklist for a deep pass.
Use an isolated Windows profile with neither `%APPDATA%\Snipit` nor `%APPDATA%\SharpSnip`. Record the result as Verified, Partially verified, or Unverified.

| # | Check | Pass when |
|---|---|---|
| 1 | Start `Snipit.exe`, press `Win+Shift+X` | Mode bar slides in at the top of the screen, tray icon present |
| 2 | Element mode on Chrome, Explorer, Notepad, VS Code, Win11 Settings | Highlight follows cursor, click captures exactly the rect (±1 px) |
| 3 | `Tab`, `PgUp`, `PgDn`, `Ctrl+Wheel` in element mode | Deterministic and reversible |
| 4 | Element mode over an app with no UIA (a game) | Switches to region picker, no error |
| 5 | Region: draw, move, resize by each handle, double-click, Enter | Correct rect; snap to window edges; loupe follows |
| 6 | Window mode, `C` toggle | Frame vs client area both correct (incl. Win11 shadow-less frame) |
| 7 | Full screen: `Tab`, `A`, `1`..`9`, `Enter` on 2 monitors | Correct monitor highlighted and captured only |
| 8 | All-monitors capture at 100% + 150% | One image, no gaps/overlap; pixel test at monitor seam |
| 9 | L-shaped arrangement | Union rect correct, gap filled with matte |
| 10 | One-shot CLI `Snipit --fullscreen` | Clipboard image in < 250 ms, no UI flash |
| 11 | Delay 3/5/10 s, open a menu during countdown, `Esc` | Menu captured; `Esc` cancels; chip not in image |
| 12 | Unplug a monitor during countdown | No crash; capture continues |
| 13 | `Esc` from every picker, right-click | No leaked hook/timer/overlay (check Task Manager, input stays normal) |
| 14 | 60 s idle in the menu | Watchdog closes the session |
| 15 | Editor: each tool, undo/redo, crop+undo, blur, save, copy | Output matches canvas |
| 16 | Settings: invalid fixed size or interval | The saved-with-notes toast names the field and its previous value is kept — and every other setting in the same save still applies and persists (a bad field never discards the save). The window has no hotkey fields and no error text about other programs' keys |
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

## Unreleased manual rows (recording)
- [ ] `Snipit.exe --record 5` on the primary monitor: a timestamped MP4 lands in the output folder and plays back in a standard player, showing the desktop and cursor moving; `--record 5 --monitor 2 --fps 60 --out C:\Temp\rec.mp4` honours its arguments; `--region 100,100,800,600` records only that rectangle.
- [ ] Tray → Record screen…: the window opens themed, Start begins (elapsed ticks, tray's Stop recording enables), Stop from either the window or the tray saves the MP4 and toasts its name; ticking the region option after a region capture records just that rectangle; "Pick region…" hides the window, lets you drag a fresh rectangle (Esc cancels and changes nothing), and the window returns showing the new region's size and position; a recording started with the region ticked covers only that rectangle, and the picked region is still offered after a restart.
- [ ] Record 10 s with music playing and Microphone ticked while speaking: the MP4 plays back with both the music and the voice audible and roughly in sync; `--record 5 --no-audio` produces a silent MP4.
- [ ] Record with Webcam overlay ticked: the camera appears in the chosen corner, moving live; the same scene at Quality Low vs Ultra shows the expected file-size difference; `--record 5 --webcam --webcam-corner TopLeft` agrees.
- [ ] Set Max minutes to 1 and record: the recording stops itself at about a minute with a "limit reached" toast; schedule a recording two minutes ahead for 1 minute: it starts and stops on its own; `--record 5 --at` with a time two minutes out waits, then records.
- [ ] Record 5 s in GIF format (window and `--record 5 --gif`): the GIF plays back animated at a sensible speed in a browser.
- [ ] While recording, click Draw on screen and scribble: the strokes show in the playback; Esc ends drawing; colours change with 1–5 and C clears.
- [ ] Editor pack: Step clicks drop 1, 2, 3 badges (Undo removes them); Resize to half keeps the aspect and Undo restores; Watermark corner and tiled both stamp visibly; Bright/Contrast sliders + Apply B/C change the image; Replace text on a screenshot containing a repeated word covers and rewrites every occurrence; set Auto effect = Border in Settings, take a capture — the saved file and the clipboard image already carry the frame (captures never open the editor), and the recording preferences (fps/audio) survive a Settings save.
- [ ] Capture pack: draw a freehand outline around an icon — the saved PNG is transparent outside the outline; the loupe shows a changing #hex readout that matches the pixel under the cursor; set a preset region in Settings — the tray's "Capture preset region" captures exactly that rectangle.
- [ ] Integration: tray → Recent captures lists the newest captures; a still reopens in the editor, a recording opens in the default player; with an external editor path set in Settings, the editor's External editor… button opens the saved capture in that program.

## Unreleased manual rows (WGC backend)
- [ ] Play DRM video (or a hardware-overlay scene) and capture its monitor: the picture is present, not black; the log's "capture backend" line reads Windows.Graphics.Capture.
- [ ] Ordinary desktop capture on each monitor (incl. mixed DPI): pixel-correct, no offset or tearing; a capture spanning two monitors stitches at the seam.

## Unreleased manual rows (owner's v0.6.0 field fixes)
- [ ] On an isolated fresh profile, turn the Windows Print Screen switch on. Start Snipit and verify that Print Screen opens its menu. Quit from the tray and verify the switch returns to on. Repeat with the switch absent and verify quitting removes the value Snipit wrote. Settings has no Print Screen or hotkey fields.
- [ ] With no output folder set, a capture lands in the Pictures folder itself.
- [ ] Settings: Cancel closes the window; Esc closes it; Save applies and closes. The window renders in the dark TesselGUI theme (inputs, dropdowns, checkboxes, green Save) with readable text throughout.

## Unreleased manual rows (mode bar)
- [ ] Open the menu (Print Screen or the tray's Capture… item): the bar slides down from the top-centre of the monitor the cursor is on; hovering a button highlights it; clicking Element/Region/Window/Full screen/Scrolling starts that mode; clicking Delay cycles off → 3s → 5s → 10s and the label follows; ✕, Esc and right-click all cancel; on a second monitor the bar follows the cursor's monitor; at 150–200% scaling the bar is centred and its buttons hit where they are drawn.
- [ ] Clicking a tool in the bar NEVER captures by itself (Snipping Tool contract): click Window — nothing is captured; move over another window and click once — that window is captured. Same for Element and Full screen (this monitor). Repeat with a very fast click on the tool and with a slow press-and-hold release: neither captures. Keyboard switching (W with the bar open) followed by one click also captures normally.

## Unreleased manual rows (Snipping Tool replacement)
- [ ] While Snipit runs, Windows Settings → Accessibility → Keyboard shows the Print-screen switch off and pressing Print Screen opens Snipit's menu (not Snipping Tool); kill Snipit from Task Manager, start it again and quit normally — the switch still ends at its original state (the crash could not corrupt the record).
- [ ] Untick it and Save: the Windows switch is back to its previous state and the menu hotkey is the key from before the takeover.
- [ ] With takeover on, run `Snipit.exe --clean` from a portable copy: the Windows Print-screen switch is restored before the settings are deleted.
- [ ] Tray menu → Hide tray icon: the icon disappears and captures still work from the hotkeys; running Snipit again from the Start menu brings the icon back and opens Settings.

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
- [ ] Capture a region, restart Snipit, press the repeat-last hotkey: the same rectangle is captured. Then change the display layout so the rectangle is off-screen: a clear "off screen" message, no crash.
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

## Unreleased manual rows (workflow recording conversion)
- [ ] Pause/resume: record 5 s with system audio on, Pause for 5 s (move windows, make noise — none of it may appear), Resume for 5 s, Stop — the MP4 plays ≈10 s (not 15), the paused sounds are absent, and speech before/after the pause stays in sync with the picture; same check for a GIF recording (plays ≈10 s).
- [ ] Recording workflow with `format: webm` then `copy: <folder>`: after a recording stops, the WebM appears in the folder and the toast reports both steps.
- [ ] Recording workflow with `format: jpg`: stops with the plain "recordings convert with an FFmpeg preset" reason.
- [ ] Capture workflow with `format: webm`: stops with the "applies to recordings" reason.

## Unreleased manual rows (post-v0.8.0 fix)
- [ ] Mode bar: click **Window** — nothing captures; the window under the NEXT click is captured. Same for Element, Full screen and (via S) Fixed size: the bar click only arms the tool.
- [ ] Mode bar: click **Region**, then drag a rectangle — the drag works normally (the fix must not eat genuine clicks).

## Unreleased manual rows (v0.8.0)
- [ ] Settings: create a workflow (copy + format + run steps), assign it under "After capture", take a capture — every step runs in order; a failing step toasts its reason and stops the chain.
- [ ] Manage workflows window: rename/reassign a workflow, delete one that is assigned — the assignment clears instead of dangling.
- [ ] `--run-workflow NAME --file PATH`: applies the chain headlessly, exit 0; a failing step exits 3 with the reason.
- [ ] Interval capture with the APNG format: `animation.apng` animates in a browser; frame timing matches the interval.
- [ ] A still saved as .apng opens as an image in a browser.
- [ ] Recorder "Output size 720p" on a 1440p+ monitor: the MP4's stored dimensions are ≤1280×720 (even numbers); Original keeps full size.
- [ ] Portable build contains `ffmpeg\ffmpeg.exe` + DLLs + LICENSE.txt and THIRD-PARTY-NOTICES.md at the root.
- [ ] Tray "Export with FFmpeg…": WebM and AV1 exports of a test recording play in a browser; MKV remux is near-instant; MP3 extract has audio only; a failed export shows FFmpeg's log tail.
- [ ] `--export webm --file rec.mp4`: prints the new path, exit 0; `--export bogus` exits 2 with the preset list.
- [ ] Settings FFmpeg override pointing at a different ffmpeg.exe: an export uses it (log shows the run); clearing it returns to the bundle.

## v0.11.0 manual rows (deferred by owner)
- [ ] Kill Snipit in Task Manager, then run `Snipit.exe --restore-printscreen`: Print Screen opens Snipping Tool again.
- [ ] Tray "Give Print Screen back": Print Screen opens Snipping Tool; restart Snipit: Snipit owns the key again.
- [ ] Recording with audio where the mux fails (make the output path read-only): a toast names the problem and the raw video survives.
- [ ] Tab through Settings: every control shows a clearly visible focus ring, including text boxes. Narrator reads each field's label.
- [ ] Turn on a Windows High Contrast theme: Settings/Recorder use system colours; overlays use the yellow highlight.
- [ ] Workflow `run: C:\tools\x.exe|%path%` on a capture in a folder with spaces: the program receives one argument.
- [ ] Edit a workflow step in `settings.json` with Notepad, start Snipit: toast says workflows are paused; a capture workflow with a run step reports "blocked"; Settings > Save re-enables it.

## v0.19.0 manual rows (deferred by owner)
- Region tool: drag an area, press A, drag a second area elsewhere, press Enter. The result is one PNG covering both, transparent between them. The first area stays undimmed and outlined while you draw the second.
- Backspace removes the last area; with nothing being drawn it removes the previous added area. A lone area with Enter captures normally.

## v0.18.0 manual rows (deferred by owner)
- Editor → Cut out, drag a wide strip across the middle of a capture: the strip goes, the halves join, the image is shorter. Undo restores.
- Drag a tall strip: columns go instead. Drag over the whole image: nothing happens.

## v0.17.0 manual rows (deferred by owner)
- Editor → Effects…: set Frame width 20 and colour FF8000, OK, click Frame: the border is 20 px orange. Reopen the dialog after restarting: values are kept.
- Enter "abc" as a width or "zz" as a colour: a red message shows and the dialog stays open. Cancel changes nothing.
- Shadow blur 30 / strength 80 gives a larger, darker shadow; Torn/Wave depth 30 cuts deeper; 0 in each restores automatic sizing.

## v0.15.0 manual rows (deferred by owner)
- Open a capture in the editor, click Torn: all four edges turn ragged and transparent; Undo restores. Click it again on a fresh image: the edge differs.
- Click Wave: edges ripple smoothly. Save as PNG and open it: the cut area is transparent. Save as JPEG: no crash.

## v0.14.0 manual rows (deferred by owner)
- Open the bar, drag the dotted handle left and right: the bar follows the cursor, stops at both screen edges, and the buttons do not fire.
- Release, Esc, open the bar again, then restart Snipit and open it again: the bar is where it was left.
- Repeat on a second monitor and at 150% scale.

## v0.13.0 manual rows (deferred by owner)
- Open the mode bar (Print Screen): 7 icons with key hints and an Esc close; hover fills the cell; labels and icons stay crisp at 100%, 150% and 200% scale.
- Turn on a High Contrast theme: bar is black with white edge and ink; hover inverts to black on white.
- Turn off Windows animations: the bar appears without sliding.

## v0.12.0 manual rows (deferred by owner)
- [ ] Tray > Pick colour…: magnifier follows the cursor; clicking a known colour (e.g. a pure red rectangle) copies the exact value; 1-5 and Tab change the format; the format is remembered next time.
- [ ] Pick a colour on a second monitor and across a mixed-DPI boundary: the value matches what is shown.
- [ ] Editor: Frame adds a visible border and the canvas grows; Shadow shows a soft shadow on transparent PNG; Undo restores both.

## v0.20.0 manual rows (deferred by owner)
- [ ] Open the editor: tools row shows 10 icon cells with key hints plus Undo, Redo, Copy and a Carmine Save; the active tool has a Carmine underline.
- [ ] Press P, H, A, R, O, T, N, B, C, X: the matching tool activates; typing in a text box or dialog does not switch tools.
- [ ] Colour swatches: the chosen one shows a ring; drawing uses it. Width slider changes stroke.
- [ ] Text, Effects, Adjust, More menus open under their button, are dark, and every item works (incl. Adjust > Brightness / contrast…).
- [ ] Status bar, side panel and dialogs (Resize, Watermark, Replace text, Effects settings) are themed.
- [ ] High Contrast: editor is black and white, hover inverts, focus ring visible on every control.

## v0.21.0 manual rows (deferred by owner)
- [ ] Adjust > Sharpen: text edges crisper, no heavy halos; repeat twice still acceptable; Undo restores.
- [ ] Adjust > More colour / Less colour: visibly more / less vivid; Undo restores.

## v0.22.0 manual rows (deferred by owner)
- [ ] Editor Spotlight (S): drag an area; outside dims, inside stays bright; Undo removes; saved/copied image includes the dimming.

## v0.23.0 manual rows (deferred by owner)
- [ ] Editor Magnify (M): drag a circle over small text; it shows larger, centred on the same spot; near an image edge it still shows image (no blank); Undo removes; saved image includes it.

## v0.24.0 manual rows (deferred by owner)
- [ ] Adjust > Hue / colour balance…: hue +120 turns reds toward green; red +50 warms the image; OK with all zero does nothing; Undo restores.

## v0.25.0 manual rows (deferred by owner)
- [ ] Callout (K): click, type a long sentence, Enter: white bubble, coloured ring, tail below left; wraps; Esc discards; Undo removes; saved image includes it.
- [ ] Stamp menu: each of the 5 stamps drops a coloured disc with a white glyph; G then click repeats the last stamp; colour swatches change the disc.
- [ ] Window at 1200 wide shows every tool cell and the right-hand buttons without clipping.

## v0.26.0 manual rows (deferred by owner)
- [ ] Editor Line (L): drag horizontal and diagonal in both directions; line uses selected colour and width; a drag shorter than 4 px adds nothing.
- [ ] Undo removes the line; Redo restores it; copied and saved images include it.
- [ ] At 1200 px width the Line tool, all other tools and right-hand buttons remain visible without clipping.
