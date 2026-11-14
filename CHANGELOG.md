# Changelog

Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning: [SemVer](https://semver.org/).

## [0.25.7] - 2026-11-14

### Changed
- **Docs: uninstalling.** The README now says to exit Snipit (or run `Snipit.exe --restore-printscreen` if it was killed) before uninstalling, because the SeanPak installer has no uninstall hook that could hand the Print Screen key back. The roadmap records the missing SeanPak feature. I looked at using the installer's registry writes instead and rejected it: it would change Print Screen behaviour at install time for everyone.

## [0.25.6] - 2026-11-14

### Security
- **Release workflow supply chain pinned.** GitHub Actions are pinned to commit SHAs (with the version in a comment), and the SeanPak packager is checked out at a pinned commit instead of its moving default branch, so a tag push builds exactly the code that was reviewed. FFmpeg was already hash-pinned. A weekly Dependabot check proposes action bumps as PRs.
- Tooling and CI only; no change to the program.

## [0.25.5] - 2026-11-14

### Fixed
- **Saving Settings no longer resets the Frame, Shadow and Torn/Wave settings.** The Settings window built a fresh preferences object and only copied some fields, so pressing Save reset the Effects… values (frame width and colour, shadow blur and strength, edge depth) to automatic. It now starts from a copy of the stored settings and changes only what the window edits, so a setting added later can never be dropped this way.

### Added
- **Single-key tool shortcuts in the editor can be turned off** (Settings > Single-key tool shortcuts in the editor; on by default). Meets the WCAG rule that letter-key shortcuts must be switchable.
- Core: `Preferences.Clone`. 3 new self-test checks (509 total), including one that fails if any simple setting is lost by the copy.

## [0.25.4] - 2026-11-14

### Fixed
- **Muted text now meets WCAG AA.** The muted grey (#89868A, about 4.3:1 on the toolbar) used for key hints, labels and status text is lightened to #A3A0A4 (about 5.6:1) in the editor and the themed windows. This departs from the TesselGUI token value on purpose.
- Core: `WcagContrast`, with self-tests asserting the foreground and muted tokens pass 4.5:1 on every dark surface. 7 new checks (506 total).

## [0.25.3] - 2026-11-14

### Fixed
- **Whole-image editor edits no longer freeze the window.** Sharpen, Hue, Colour, Shadow, Torn, Wave, Frame, Cut out, Invert, Grayscale, Flip, Rotate and the rest now do their pixel work off the UI thread. The editor is disabled with a wait cursor and a status line while an edit runs, then enabled again.
- **Undo memory is capped.** Each whole-image edit keeps a full bitmap, so a few edits of a large multi-monitor capture could use gigabytes. The history now keeps the newest edits within 512 MB (the latest edit always stays undoable); annotations cost nothing and are all kept.
- Core: `UndoBudget`. 5 new self-test checks (499 total).

## [0.25.2] - 2026-11-14

### Fixed
- **A stuck overlay can always be escaped.** Pressing Esc three times within 1.5 seconds now ends the capture session, region pick or colour pick from the input hook itself, whatever the picker is doing. A single Esc still goes back one step as before.
- **Contained errors are no longer silent.** When the UI thread hits an exception that Snipit contains, a tray notice says something went wrong and points to the log (at most once a minute).
- Core: `EscapeChord`. 5 new self-test checks (494 total).

## [0.25.1] - 2026-11-14

### Security
- **Workflow trust check hardened.** The hash over the editor, FFmpeg override and workflow settings is now keyed (HMAC-SHA256) with a per-user key kept in HKCU, so a settings file planted in a downloaded portable `data` folder, copied from another PC or edited by hand cannot carry a valid hash. A blank hash is now trusted only when there is nothing to launch; blank plus an editor, an FFmpeg override or a run/editor step reads as changed. Program-launching steps stay blocked until you press Save in Settings once.
- Upgrade note: existing settings carry the old unkeyed hash, so program-launching workflow steps pause once after upgrading until you press Save in Settings.
- 5 new self-test checks (489 total).

## [0.25.0] - 2026-11-13

### Added
- **Callout tool** in the editor (key K): click, type, Enter, and a white speech bubble ringed in the chosen colour appears with a tail below it. Esc discards.
- **Stamp** menu in the options row (key G repeats the last stamp): check, cross, question, exclamation and star marks in a coloured disc. Click the image to drop one.
- Editor window opens at 1200 wide (minimum 1100) so all tool cells fit.

## [0.24.0] - 2026-11-13

### Added
- Editor **Adjust > Hue / colour balance…** dialog: rotate hues (-180..180 degrees) and add or remove red, green and blue (-100..100). Annotations are baked in; Undo restores.
- Core: `PixelAdjust.HueShift`, `PixelAdjust.ColorBalance`. 7 new self-test checks (484 total).

## [0.23.0] - 2026-11-13

### Added
- **Magnify tool** in the editor (key M): drag a circle over a detail and it is shown 2x larger inside the circle, ringed in the chosen colour. An annotation: Undo removes it. It magnifies the image underneath, not other annotations.
- Core: `LoupeView.Zoom` (source window, kept inside the image). 5 new self-test checks (477 total).

## [0.22.0] - 2026-11-13

### Added
- **Spotlight tool** in the editor (key S): drag the area to keep bright; everything else dims. It is an annotation, so Undo removes it and it is baked in on save like the others.

## [0.21.0] - 2026-11-13

### Added
- Editor **Adjust** menu: **Sharpen**, **More colour**, **Less colour** (saturation about luma). Annotations are baked in; Undo restores.
- Core: `PixelAdjust.Saturation`, `PixelAdjust.Sharpen`. 9 new self-test checks (472 total).

## [0.20.0] - 2026-11-13

### Changed
- **Editor redesigned to match the capture mode bar** (TesselGUI revision 3, Carmine). Tools row: line-and-dot icons over labels with key hints, a Carmine underline on the active tool, Undo / Redo / Copy, and a single Carmine **Save** button. Options row: colour swatches (the chosen one wears a ring), a thin width slider, and **Text / Effects / Adjust / More** dropdown menus that replace the old wall of buttons. Dark canvas, status bar, side panel and editor dialogs. High Contrast is black and white; animations follow the system setting.
- Brightness / contrast moved to an **Adjust > Brightness / contrast…** dialog (was two sliders plus Apply B/C).

### Added
- Tool hotkeys in the editor: P pen, H highlight, A arrow, R rect, O ellipse, T text, N step, B blur, C crop, X cut out (ignored while typing).

## [0.19.0] - 2026-11-13

### Added
- **Multiple-area capture** in the Region tool: draw an area, press **A** to keep it, draw another, and so on (up to 16). Enter captures them as one image: the bounding box of all areas, with everything between and around them transparent, so save as PNG. Areas you added stay undimmed and outlined; **Backspace** removes the last one (or the area you are drawing). A single area behaves exactly as before.
- Core: `AreaMask` (`Bounds`, `ApplyAlpha`). 9 new self-test checks (463 total).

## [0.18.0] - 2026-11-13

### Added
- **Cut out tool** in the editor: drag across the image to remove a strip and join the two sides, to drop the boring middle of a tall or wide capture. A drag that covers more of the image's width than its height removes rows; a taller drag removes columns. A drag over the whole image does nothing. Annotations are baked in; Undo restores.
- Core: `ImageCut` (`Plan`, `Remove`, `CutAxis`). 14 new self-test checks (454 total).

## [0.17.0] - 2026-11-13

### Added
- **Effects… button in the editor**: set the Frame width and colour, the Shadow blur and strength, and the Torn/Wave depth. Zero means automatic by image size, which is exactly how the buttons behaved before, so nothing changes until you set a value. Settings are validated (bad numbers or colours show a message and nothing is saved) and remembered between runs.
- Core: `EffectSettings` resolves each setting to pixels for a given image, with clamps; new `Preferences` fields `FrameThickness`, `FrameColor`, `ShadowBlur`, `ShadowOpacityPercent`, `EdgeDepth`, all clamped on load. 14 new self-test checks (440 total).

## [0.16.0] - 2026-11-13

### Changed

- **SharpSnip is now Snipit.** Launch `Snipit.exe` and update any scripts or shortcuts that called the old executable. The new installer registers Snipit as a separate app.
- Portable copies use the `data` folder beside the executable. Installed copies continue using an existing `%APPDATA%\SharpSnip` folder, including saved settings and Print Screen recovery. Fresh profiles use `%APPDATA%\Snipit`.
- Release tags and downloads before v0.16.0 retain the SharpSnip name.

## [0.15.0] - 2026-11-13

### Added
- **Torn** and **Wave** edge effects in the editor. Torn rips all four edges like paper; Wave gives them a smooth ripple. The cut area becomes transparent, so save as PNG to keep it. The cut is about 1/40 of the shorter side deep and never more than a quarter of it. Both bake in annotations and are undoable like the other whole-image effects. Torn is different on every click.
- Core: `EdgeEffects` (`Apply`, `Profile`, `EdgeStyle`, `EdgeSides`), deterministic per seed, same canvas size. Self-tests cover depth limits and narrow crops.

## [0.14.0] - 2026-11-13

### Added
- **Movable capture mode bar**: a dotted drag handle on the bar's left end. Press it, slide the bar along the top edge of the screen and release. The position is saved after each move and used the next time the bar opens. It is stored as a fraction of the monitor width, so it stays put across resolution and DPI changes, and the bar always stays fully on screen. Esc mid-drag keeps the position reached.
- Core: `ModeBarLayout` placement math (`Left`, `CenterFor`, `SanitizeCenter`) and a grip hit zone; `Preferences.ModeBarCenter` (0..1, default 0.5, clamped on load). 9 new self-test checks (403 total).

### Changed
- The mode bar is 16 units wider for the handle.

## [0.13.0] - 2026-11-13

### Changed
- **Capture mode bar redesigned to the TesselGUI revision 3 (Carmine) standard**, design approved by the owner from a mockup. Raised charcoal surface, 1px edge, popup shadow, 12px bottom corners, compact 64x60 cells (was 76x80) with a visible key hint on each (E, R, W, D, H, C, Sp, Esc). Hover is a quiet fill, never colour alone.
- **New icon set in the Nothing-OS line-and-dot style**: 24x24 grid, 1.6 stroke, round caps and joins, one small dot accent each. The outline carries the meaning; the dots are decoration. Replaces the 28x28 hand-drawn glyphs.
- **High Contrast mode is now black and white** (white ink and edge on black; hover inverts to black on white), not black on yellow.
- The slide-in is skipped when Windows animations are turned off.
- Core: `ModeBarLayout` constants resized (button 64, bar 72, close 36, separator 9) with `CellHeight`; self-tests updated. 394 checks.

## [0.12.0] - 2026-11-13

### Added
- **Colour picker** (tray → "Pick colour…"): move over any pixel on screen, read it in the 8x magnifier, press 1-5 or Tab to change the format, click or Enter to copy. Formats: HEX (`FF8000`), RGB (`rgb(255, 128, 0)`), HTML (`#FF8000`), C++ (`0x0080FF`, COLORREF byte order) and Delphi (`$000080FF`, TColor). The last format is remembered. SharpSnip captures the desktop before its picker overlays open and samples that frozen image.
- **Frame** and **Shadow** buttons in the editor. Frame adds a solid border outside the image (the canvas grows). Shadow adds a soft, offset drop shadow on a transparent margin; save as PNG to keep the transparency. Both bake in annotations and are undoable like the other whole-image effects.
- Core: `ColorFormat` (formats, hex parsing, gradient palette helper) and `ImageFrames` (border and shadow pixel math), 20 new self-test checks (394 total).

## [0.11.0] - 2026-11-13

A hardening release from a full review of v0.10.0. No new capture features.

### Fixed
- **Print Screen is handed back after a crash or kill.** The Windows "Print Screen opens Snipping Tool" switch used to be restored only by a clean exit. It is now also restored on process exit, on an unhandled exception, and by the new `--restore-printscreen` command and "Give Print Screen back" tray item (use it before uninstalling). The restore is idempotent and reads the settings file itself.
- **A recording can no longer vanish silently.** If adding audio fails, the silent video is saved and a toast says so. If even that copy fails, the raw video is kept in the temp folder and its path is shown, instead of being deleted. Stopping a recording that throws now reports an error instead of doing nothing.
- **Every swallowed error is logged.** The ten empty `catch { }` blocks now write a log line (debug level for plain cleanup, a warning for the recorder's temp-folder cleanup and the size check).
- **Focus is visible.** The focus ring and check marks use a light tint (#E8939F, at least 3:1 on every surface; the deep Carmine was about 1.9:1). Text boxes, which showed no focus ring at all, now do. A self-test pins the palette's contrast ratios.
- **Screen readers can name the fields.** Each input in Settings, Recorder, Export and Workflows is now labelled by the text beside it, and tooltips become help text.
- **Windows High Contrast is honoured**: windows use system colours and stock controls, and capture overlays switch to the high-contrast highlight.
- **Workflow "run" and "editor" steps handle paths with spaces**: `%path%`, `%dir%`, `%name%` and `%ext%` are quoted automatically unless you already put them in quotes. The process handle is released.
- **Command-line values can't be mistaken for flags**: `--out --clean` now means a file named "--clean", not "wipe my settings".
- **Log files are capped** at 5 MB per day (rolled to `.1`).

### Changed
- **.NET 10 (LTS)**: the project, tests, CI and release workflows move from .NET 8, whose support ended on 2026-11-10. One interop call (`Marshal.QueryInterface`) changed signature and was updated.

### Security
- **Settings edited outside the app can no longer launch programs.** SharpSnip records a hash of the external editor, the FFmpeg override and all workflow steps whenever you save in the app. If the settings file changes any other way (a planted portable-mode config, a text editor), `run`/`editor` steps and the FFmpeg override stay blocked, with a toast, until you press Save in Settings. Fresh installs and upgrades record their current settings the first time.

## [0.10.0] - 2026-11-13

### Added
- **Pause and resume recordings**: the Recorder window gains a Pause button (both MP4 and GIF). Paused time is cut, not kept — video frames drop while paused and every later timestamp shifts by the paused span, and the audio captures close a segment file at each pause and reopen on resume, the segments joining back into a single track when the recording stops. The window's clock shows media time, so it freezes while paused, and stopping from a pause works as expected.

### Changed
- **Print Screen is the only hotkey, and it is not optional**: SharpSnip now overrides Print Screen for as long as it runs — the Windows "Print Screen opens Snipping Tool" switch is turned off at startup and **restored to its previous state when SharpSnip exits** (the original state is recorded once and survives crashes, so a later exit still restores the truth). There is no Settings option for it and nothing to conflict, so the "already used by another program" message is gone for good.
- **TesselGUI revision 3 (Carmine) theme**: every window (Settings, Recorder, Workflows, Export) moves from the revision 2 GitHub palette to the current standard — near-black charcoal surfaces, off-white text, and the deep Carmine accent carrying selection, focus and the action buttons.

### Removed
- **Every configurable hotkey**: the menu, full-screen, active-window, repeat-last, per-monitor and preset hotkeys and their Settings fields are gone, along with the Print Screen takeover checkbox. Captures are started from Print Screen, the tray menu, the mode bar's keys once a session is open, or the command line (`--fullscreen`, `--record`, …). Settings saved by older versions load fine — the old keys are ignored — and a preset region is still captured from the tray menu.

## [0.9.2] - 2026-11-13

### Changed
- **Captures never open the editor** (owner direction): the post-capture editor and its Settings option are gone, along with the stored preference — no setting can bring it back. A capture is clipboard + file + workflow, always. The editor remains for deliberate opens only: a past capture from the tray's Recent captures. The Settings auto-effect (Border / Grayscale), which the editor used to apply when it opened, is now applied to the capture itself, so the clipboard image and the saved file carry it.
- **Recording audio mixes by streaming now**: the system + microphone tracks are combined in chunks with an on-the-fly resample instead of loading both tracks into memory at Stop, so long recordings no longer need RAM proportional to their length. The streamed result is sample-identical to the old whole-track mix (pinned by tests).

### Fixed
- **Settings saves are no longer all-or-nothing** (owner field report: the editor option appeared unticked yet the editor kept opening — the stored setting never changed, because every save was being discarded whole when the hotkey registration failed against another program's keys). Validation and applying are per-field now: an invalid or conflicting entry keeps its previous value and is named in a "saved, with notes" warning, and everything else in the same save applies and persists.
- A Settings save no longer silently clears the Recorder's picked region (the save now carries it over, like the other recording preferences).

### Added
- **WGC pre-warm (§1.4)**: at startup SharpSnip warms the capture pipeline on a background thread — the D3D11 device is created once, shared by every capture, and primed with a throwaway frame — so the first screenshot of a session no longer pays the full device + pipeline setup cost. If a capture ever fails on the shared device, the cache is dropped (that shot falls back to GDI) and the next capture builds a fresh one.
- **The Recorder can pick its own region**: a "Pick region…" button drags a fresh rectangle on screen (remembered as the recording region for next time), instead of only reusing the last capture's rectangle — which remains the fallback, and the window now shows which rectangle will be recorded. The monitor/crop resolution moved into tested Core logic (`RegionCrop`).

## [0.9.1] - 2026-11-13

### Fixed
- **Choosing a tool in the mode bar could still take the screenshot** (owner field report on v0.9.0; the v0.8.1 fix probed the physical button state at picker takeover, which races the queued release event — the probe could run after the hardware release but before that release was delivered). Tool switches from the bar now carry their mouse origin through the router, and a Core `ClickGate` decides purely from the event stream: the release that ends the arming click is always swallowed, and capture requires a fresh, complete click of the user's own — the Snipping Tool contract. Keyboard tool switches are unaffected.

## [0.9.0] - 2026-11-13

### Changed
- **Captures no longer open the editor by default** (owner direction): a capture goes straight to the clipboard and its file, with a toast. The editor opens after a capture only when "Open the editor after a capture" is ticked in Settings. Existing installs keep whatever their saved setting says — untick it once in Settings to get the new behavior.

### Added
- **Workflows can convert recordings**: a `format:` step in a recording workflow now names an FFmpeg preset (`format: webm`, also av1, h264, mkv, gifhq, mp3, m4a) and converts through the bundled FFmpeg, continuing the chain with the converted file — so "record → make a WebM → copy it to the share folder" is one hands-free chain. Still workflows keep using image formats, and mismatches (a preset on a still, an image format on a recording, FFmpeg missing) stop the chain with a plain reason.

## [0.8.1] - 2026-11-13

### Fixed
- **Mode bar no longer captures by itself** (owner field report on v0.8.0): every picker confirms on mouse-button release, so the release of the very click that chose a tool in the bar landed in the new picker as its capture click — choosing Window instantly "captured" the window under the bar. A picker that takes over while the button is still held now swallows that stranded release; tools arm on the bar click and capture on the next full click, as expected.

## [0.8.0] - 2026-11-13

### Added
- **Workflows** (ShareX-style after-capture chains, fully local): named step lists — copy, format, effect (Border/Grayscale), external editor, run a program with %path%/%dir%/%name%/%ext% — assigned to captures and recordings in Settings, managed in their own window, and runnable by hand with `--run-workflow NAME --file PATH`.
- **Animated APNG**: interval captures can save APNG alongside GIF (hand-written APNG writer; a still .apng export is a proper one-frame APNG).
- **Recording output size**: Original / 1080p / 720p / 480p cap in the recorder window and as a setting; never upscales; applies to MP4 and GIF recordings (`--size` on the CLI).
- **FFmpeg export** (bundled LGPL FFmpeg, owner-approved): tray *Export with FFmpeg…* converts recordings and GIFs to WebM (VP9), AV1, H.264 MP4 (Windows encoder), lossless MKV, palette GIF, MP3 or M4A, with a custom-arguments mode for a user-supplied build; `--export PRESET --file PATH [--out PATH]` on the CLI; Settings FFmpeg override path; license and source notice in THIRD-PARTY-NOTICES.md. The SharpSnip code itself still uses no third-party packages.

## [0.7.0] - 2026-11-13

### Added
- Integration pack: **Recent captures** in the tray menu (the newest stills and recordings from the output folder — stills reopen in the editor, recordings play in your default player) and an **External editor…** button in the editor (path set in Settings; saves, then opens the file in that program). Live screen drawing arrived with the recording extras.
- Capture completion pack: **Freehand capture** (mode bar or H — draw an outline; the bounding box is captured and everything outside the outline becomes transparent), a **preset-region hotkey** (set the region in Settings from your last capture, give it a hotkey, or use the tray's "Capture preset region"), and a **loupe colour readout** — the magnifier now shows the pixel under the cursor as #RRGGBB, its RGB values and its screen position.
- Editor completion pack: **Step tool** (click to drop numbered markers 1, 2, 3…), **Resize…** (new pixel dimensions with aspect lock), **Watermark…** (corner or tiled diagonal text), **Brightness/Contrast sliders** with Apply B/C, **Replace text…** (OCR find & replace — the original is covered with the surrounding colour and the new text drawn in), and an **Auto effect** setting (None / Border / Grayscale) applied when a capture opens. All are undoable like every other edit. (Also fixed: saving Settings no longer resets the recording preferences to defaults.)
- GIF recording: the Recorder window's Format picker (or `--gif`) records straight to an animated GIF with real frame timing — silent, capped at 20 fps, for short shareable clips.
- Draw on screen: a live annotation overlay (tray item, or the Recorder window's button) — draw with the mouse over any monitor; the strokes appear in captures and recordings as they happen. Keys 1–5 change the pen, C clears, Esc finishes.
- Recording limits and scheduling: optional max-minutes and max-size stops (Recorder window fields, CLI `--max-mb`), and a one-shot scheduled recording — set a start time and duration in the Recorder window, or `--record N --at HH:MM` on the command line.
- Webcam picture-in-picture for recordings (Recorder window checkbox + corner picker, CLI `--webcam [--webcam-corner C]`), composited with a tested layout engine, plus recording quality presets (Low / Medium / High / Ultra) in the window and via `--quality`.
- Recording audio: system sound (WASAPI loopback) is recorded by default and the microphone can join it (Recorder window checkboxes, CLI `--mic` / `--no-audio`); the two tracks are mixed and muxed into the MP4 when you stop. A missing audio device never blocks a recording — that source simply drops out.
- Screen recording: `--record N [--monitor K] [--fps F] [--region X,Y,W,H] [--out FILE]` records a monitor — or one rectangle of it — to MP4 (H.264, hardware-accelerated MediaTranscoder over the WGC stream, cursor included). In the app, the tray's "Record screen…" opens a small recording window (monitor picker, "last capture region" option, frame rate, Start/Stop with elapsed time) and "Stop recording" finishes from the tray; recordings land in the output folder with a toast.
- Windows.Graphics.Capture backend for stills (owner-approved program: completion pack + recording suite, 2026-11-13 — this is its foundation slice). Captures now try WGC first and fall back to GDI BitBlt automatically, with the serving backend named in the log. WGC keeps working where GDI returns black — hardware overlays, DRM video, some games. A WGC frame that arrives entirely black is treated as a failure, not a picture, and also falls back. The frame-assembly copy (`PixelBlit`) is unit-tested in the Core layer.

## [0.6.1] - 2026-11-13

### Changed
- Print Screen replaces Snipping Tool **by default**: on a first run (or any profile that never stored the choice) SharpSnip now applies the takeover itself at startup — the live Windows switch value is recorded, the switch turned off, and Print Screen becomes the menu hotkey. Unticking the Settings option still hands everything back exactly as before.
- The default output folder is now the user's Pictures folder itself (was a SharpSnip subfolder inside it). A folder set in Settings still wins.
- The Settings window follows the TesselGUI revision 2 design standard (GitHub scheme): dark surfaces, 1px edges, Segoe UI, green Save action, themed text boxes, combo boxes and check boxes. The OS title bar is unchanged.

### Fixed
- Settings: the Cancel button did nothing. The window is shown modeless, where the old `IsCancel`/`DialogResult` wiring is inert — Cancel (and Esc) now close the window explicitly, and Save closes it the same way after applying.

## [0.6.0] - 2026-11-13

### Added
- Mode bar: the mode menu is now a Snipping-Tool-style tab that slides down from the top-centre of the screen (design picked by the owner from rendered mockups) — icon buttons with labels underneath for Element, Region, Window, Full screen (this monitor), Scrolling and Delay (click cycles it), plus a close button. It replaces the text HUD; every keyboard shortcut works exactly as before (E/R/W/S/F/D/C, 1..9, Space, Esc), and Fixed-size region stays on its `S` key.
- Settings → "Print Screen replaces Snipping Tool": turns off the Windows switch that gives Print Screen to Snipping Tool and makes Print Screen SharpSnip's menu hotkey. The previous switch value and menu hotkey are remembered and restored when the option is unticked, and `--clean` restores them too.
- Tray icon can be hidden from its own context menu ("Hide tray icon"). Running SharpSnip again while it is running in the background brings the icon back and opens Settings.

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
