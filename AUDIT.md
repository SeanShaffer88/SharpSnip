# Audit — v0.2.0 (blueprint v2 + roadmap phase 1 slice)

Evidence labels: **Verified** (run and checked), **Partial** (compiled, reviewed, not run), **Unverified** (not run, depends on real Windows), **Not implemented**.

## What was actually done
- Compiled for `net10.0-windows10.0.19041.0` (cross-build on Linux): 0 errors, 0 warnings, warnings-as-errors on.
- Ran the self-test project: 80 passed, 0 failed (Linux).
- **Nothing in the Windows layer has been executed.** No overlay, hook, UI Automation, capture, clipboard or editor code has run on a real machine.

## Blueprint coverage

| Blueprint item | State | Evidence / note |
|---|---|---|
| §1 state machine | Verified (logic) | `AppStateMachine`, tested; router uses it |
| §1.1 Per-monitor V2 DPI | Partial | `SetProcessDpiAwarenessContext` at start of `Main`. The SDK refuses DPI keys in the manifest when WinForms is referenced (WFAC010), so there is no manifest entry. |
| §1.2 hotkeys | Changed by owner direction 2026-11-13 | All configurable hotkeys removed. Print Screen is the only global hotkey (`RegisterHotKey`, unconditional, failure logged only); the Windows Snipping switch is forced off while Snipit runs and restored on exit. Nothing to configure means no conflict errors can exist. Runtime Unverified. |
| §1.3 UIA pre-warm | Partial | STA worker touches `RootElement` at start |
| §1.4 D3D11 / WGC pre-warm | Partial | At startup a background thread creates the process-shared D3D11 device and pulls one throwaway frame through the full WGC pipeline (`WgcCapture.Prewarm`), so the first user capture is served warm; the shared device replaces per-capture device creation, and any capture failure drops the cache (that capture falls back to GDI, the next builds a fresh device). Runtime effect Unverified — no Windows machine has measured the warm/cold difference yet. |
| §1.5 monitor cache + WM_DISPLAYCHANGE | Partial | message-only window refreshes cache |
| §1.6 prefs | Verified (logic) | load/save/quarantine tested |
| §2 mode menu, per-monitor overlays | Partial | WS_EX_LAYERED/TOPMOST/TOOLWINDOW (+TRANSPARENT, NOACTIVATE) |
| §3A element picker | Partial | 50 ms sampling, 1 s UIA budget, tree navigation, region fallback |
| §3B region picker | Partial (geometry Verified) | handles, move, resize, snap, loupe, double-click |
| §3C window picker | Partial | extended frame bounds, client toggle |
| §3D full-screen picker + instant hotkey | Partial (target/stitch logic Verified) | |
| §3D.4 delay + countdown chip | Partial | chip excluded from capture, Esc cancel, logic tested |
| §3D.5 / §4.2.1 stitching | Verified (geometry) / Unverified (pixels) | placements, gaps, mixed DPI tested on paper-geometry; BitBlt untested |
| §3D.6 display change mid-countdown | Partial | re-enumerate, fall back to primary monitor |
| §4.5 clipboard PNG + DIB | Unverified | |
| §5 editor | Partial | all listed tools except Share (see Deviations) |
| §6 export | Partial | PNG, JPEG, atomic write; BMP/GIF/TIFF added unreleased (logic Verified, encoders Unverified on Windows) |
| §11 threading | Partial | UIA on STA thread, overlays on UI thread |
| §11 watchdog 60 s | Partial | |
| §11 security / privacy | Partial | no network code, no UIA tree persisted, SetWindowDisplayAffinity on overlays and chip |
| §11 accessibility | Partial | keyboard-only flow, high-contrast mode, UIA live regions on hint and countdown; screen-reader behaviour of a non-activating window is unverified |
| §13 acceptance criteria (latency, ±1 px, Chrome/VS Code/etc.) | Unverified | see docs/TEST-PLAN.md |

## Added after the blueprint (v0.2)
| Item | State | Note |
|---|---|---|
| Active-window capture | Unverified | `GetForegroundWindow` + extended frame bounds; refuses Snipit's own windows; no check for fullscreen/exclusive apps. Reached from the menu's Window mode (its hotkey was removed 2026-11-13 with all configurable hotkeys) |
| Repeat-last capture | Unverified | the rectangle persists in settings (sanitised on load, clipped to the current desktop on use) — still not run on Windows. Its hotkey was removed 2026-11-13 |
| Default hotkeys Win+Shift+W / Win+Shift+R | Removed 2026-11-13 | Owner direction: no configurable hotkeys at all — on his machine both keys are owned by another program, which made every Settings save carry a conflict warning. Print Screen is the only hotkey now |

## Deviations from the blueprint
1. **Capture uses GDI BitBlt only; Windows.Graphics.Capture is not implemented.** WGC needs D3D11 interop and a frame pool; it was left out of v0.1 rather than shipped untested. Effects: no DRM-aware black-region detection (protected windows may capture as black without a warning), no HDR "converted" badge, hardware-accelerated or exclusive-fullscreen content may not capture. Planned for Phase 1.
2. **MSAA fallback is reduced to window-bounds detection.** If `AutomationElement.FromPoint` fails, the picker reads the window rectangle and then switches to the region picker, as §3A.3 requires for opaque apps.
3. **Own-overlay hit test.** Overlays are click-through, but if UIA still returns an overlay element, the picker finds the real window by z-order and walks down to the child under the cursor. Unverified.
4. **WebP export not implemented.** WPF has no WebP encoder; adding one needs a package, which the charter forbids without approval.
5. **"Share" button is "Reveal"** (save, then show in Explorer). The Windows share sheet needs WinRT interop; deferred.
6. **Esc behaviour.** Esc inside a picker returns to the mode menu (§3A/§3D.3); Esc in the menu ends the session (§1). §13 says "Esc from any picker returns to IDLE". Both are honoured in the sense that no hook, timer or overlay survives; pressing Esc twice reaches Idle. Right-click behaves like Esc.
7. **Overlays are not re-shown after capture** (§4.6). A toast appears; the editor never opens after a capture (owner direction, 2026-11-13 — it remains for deliberate opens from the capture history only).
8. Captures default to the user's Pictures folder. Earlier versions used `Pictures\SharpSnip`.
9. **Hotkey hook scope.** Keys pressed with Ctrl/Alt/Win held are never swallowed so system shortcuts keep working; all other keys (except bare modifiers) are swallowed while a picker is active.
10. **Loupe needs OS-level capture exclusion.** On Windows older than 10 2004 the region picker runs without the magnifier.

## Known limitations
- Elevated (administrator) windows: UI Automation from a non-elevated Snipit is blocked by UIPI, so those apps fall back to region mode.
- Per-pixel correctness at the seam of mixed-DPI monitors is untested.
- WPF windows moved across monitors with different DPI can be resized by the framework; the overlay re-applies exact physical bounds in `OnDpiChanged`. Unverified.
- CI workflow exists locally (unreleased commits) but is not live until pushed; workflow files on this account have needed browser-side approval.
- Overlay colours are neutral (accent #2D9CFF). Conformance to the TesselGUI standard is **pending Sean's confirmation**; no mockups were made (standing instruction).
- No automated tests exist for the Windows layer.

## Charter checks
| Rule | State |
|---|---|
| No external packages | Met (0 packages) |
| No secrets, no network | Met (no network APIs referenced) |
| No `unwrap`-style crashes; errors at boundaries | Partial: every hook handler and session is wrapped; unhandled UI exceptions are logged, not shown |
| Local timestamped logs | Met |
| Atomic writes | Met (settings, images) |
| Window size/position persisted and validated against monitors | Editor only (the only resizable window) |
| Tooltips on interactive controls | Met in editor and settings |
| Remember window geometry on display change | Partial |

## Unreleased additions (v0.8.0)

- Workflows (Core `WorkflowPlan`, App `WorkflowRunner` + manager window, **Verified (logic)**; runtime Unverified): local after-capture/after-recording step chains. Steps are data (spec lines), sanitized and capped in Preferences; external programs run without a shell and only when a workflow names them — no new ambient execution surface.
- APNG (Core `ApngWriter` + `Crc32`, **Verified (logic)** against the PNG/APNG chunk rules): streaming writer, frame count patched on close like the WAV writer; interval captures and still exports both route through it.
- Recording output size (Core `RecordingSize`, **Verified (logic)**): height-capped, never-upscale target shared by the MP4 transcoder profile and the GIF sink.
- FFmpeg bundle (**dependency, owner-approved 2026-11-13**): the release workflow downloads one pinned LGPL shared build (pin + SHA256 in `tools/ffmpeg.json`), verifies the hash, and ships ffmpeg.exe + DLLs + LICENSE.txt under `ffmpeg/` in both assets; THIRD-PARTY-NOTICES.md states the license, source and replacement rights. Presets (Core `FfmpegPlan`, **Verified (logic)**) use only encoders present in an LGPL build — a test pins the absence of libx264/libx265; H.264 goes through Windows' MediaFoundation encoder. Runtime Unverified (exports have never run here). **Exit sketch:** delete the workflow step, the `ffmpeg/` lookup falls back to the Settings override, and export reports FFmpeg missing — every other feature is untouched. Cost of the dependency: the payload adds 165.5 MB uncompressed to both release assets.

## Unreleased additions (after v0.6.1)

- Recording (`WgcStream` + `ScreenRecorder` + `--record` CLI + tray Recorder window; planning math in Core `RecordingPlan`, WAV writing and PCM mixing in Core `WavWriter`/`PcmMix`, **Verified (logic)**): **compile-verified only — runtime Unverified** until the owner records a clip on Windows and plays the MP4 back. An unconsumed frame is dropped rather than queued, so a slow encoder lowers the effective frame rate instead of growing memory. Region recording rides the pixel path (readback + crop, throttled to the target rate) while full monitors ride GPU surfaces; the window's region option records a rectangle — picked fresh with its "Pick region…" button (remembered across runs) or falling back to the last capture rectangle; a freehand-shaped recording region is not built. Monitor/crop resolution is Core `RegionCrop`, Verified (logic). Audio: WASAPI loopback (system) + default mic, each to its own WAV, mixed at Stop by streaming (chunked reads, on-the-fly resample — Core `WavReader`/`PcmResampler`, Verified (logic) as sample-identical to the whole-array mix, so long recordings no longer load whole tracks into RAM) and muxed via MediaComposition; a failed mux keeps the silent video instead of losing the recording. Audio/video start skew is tens of milliseconds, not sample-synced. Pause/resume (Recorder window button, both recorders): frames drop while paused and later timestamps shift by the paused span (Core `PauseClock`, Verified (logic)); the audio captures close a segment file per pause and reopen on resume, the segments joining back into one track at Stop (Core `WavConcat`, Verified (logic)) — so paused time is absent from audio and video alike; the runtime path is Unverified like the rest of recording. Webcam overlay (MediaCapture frame reader, Core `PipLayout` + scaled blit, **Verified (logic)**) forces the pixel path for every frame — a real CPU cost at 4K that the field check should judge; a camera that will not open simply drops out. GIF recordings (`GifRecorder`) share the pixel path and quantise through WPF like the interval GIF sink; the draw-on-screen overlay is a plain topmost window, so captures and recordings include it by construction.
- Windows.Graphics.Capture stills backend (`WgcCapture`, raw COM interop, no packages): **compile-verified only — its runtime is Unverified** until the owner captures something GDI blacks out (DRM video, a game) and confirms the picture, plus a normal capture to confirm no regression; the log names the serving backend on every capture. Frame assembly uses Core `PixelBlit`, **Verified (logic)**. GDI remains the automatic fallback, including when a WGC frame arrives entirely black. Deviation noted: the session's border-suppression property is not in the 19041 SDK contract this project targets, so it cannot be set; for full-monitor items the border overlay sits on the screen edge, outside the captured frame. This closes REVIEW.md's P0 in code, pending the field verdict.

## v0.6.1 additions (compile-verified on Linux only; not run on Windows)

- Owner-directed fixes from his v0.6.0 field captures, all compile-verified only: Print Screen takeover is the default and self-applies at startup when it was never applied (prior value recorded first, same machinery as the Settings path); the unset output folder resolves to the OS Pictures folder (`OutputNaming.ResolveFolder` contract updated, logic **Verified**); Settings Cancel/Esc close the modeless window explicitly (root cause: `IsCancel`/`DialogResult` are inert without `ShowDialog`); the Settings window is themed to TesselGUI revision 2 tokens (`TesselTheme`, values copied from the standard's `docs/tokens.json`) — its two XAML control templates are XML-validated here but only render on Windows, so the theme itself is Unverified until the owner's next look. Noted from the same captures, pre-existing and unchanged: a Settings Save is rejected while any hotkey conflicts (his Win+Shift+W / Win+Shift+R are owned by another program), so an in-Settings change cannot persist until those two are reassigned — the startup takeover apply does not depend on it.

## v0.6.0 additions (compile-verified on Linux only; not run on Windows)

- Mode bar (owner-picked design A, from rendered mockups): `ModeBarLayout` (bar geometry + hit-testing in device-independent units) is **Verified (logic)**. The overlay rendering (`OverlayWindow.SetModeBar`: tab attached to the top edge of the cursor's monitor, slides in over 180 ms, hover states, high-contrast variant, screen-reader names with keys) and `MenuPicker` click/hover dispatch are compile-verified only. The text HUD menu is gone; all keys (E/R/W/S/F/D/C, 1..9, Space, Esc, right-click) behave as before, and the bar's Full screen button means "this monitor" (all monitors stays on F).
- Theme: all four `TesselTheme` windows (Settings, Recorder, Workflows, Export) conform to TesselGUI revision 3 (Carmine, standard v2.0.0) as of v0.10.0 — token values copied from the standard's `docs/tokens.json` (charcoal surfaces, Carmine accent/action family); token conformance is Verified by inspection against the published standard, the rendering itself is Unverified until the owner sees it on Windows. The capture overlay/mode bar keep their owner-picked design A colours (see the overlay note above).
- Print Screen override (owner direction, 2026-11-13; replaces the old optional takeover and its `PrintScreenPlan`): unconditional and not a setting. At startup the app records the Windows switch's original state once (`PrintScreenKey` reads HKCU\Control Panel\Keyboard\PrintScreenKeyForSnippingEnabled — including "value absent" — into settings with a recorded flag, kept across runs so a crash cannot corrupt the record) and writes the switch off; Print Screen is registered as the menu hotkey. A graceful exit restores the recorded state and clears the record, so the next start records fresh; `--clean` restores from a leftover record. The record's persistence/clamping is **Verified (logic)**; the registry/runtime path is compile-verified only; TEST-PLAN.md has the manual rows.
- Hide tray icon (tray menu): sets `ShowTrayIcon` off and hides the icon; a second launch signals the running instance over a named event to restore the icon and open Settings. Compile-verified only.

## Field verification — 2026-11-13 (owner's machine, v0.5.0 release build)

- SEAN ran the shipped v0.5.0 and reports: "Everything works." His captures show the tray context menu with every entry (including Scrolling capture) and the hotkey-conflict notification — Win+Shift+W and Win+Shift+R were already owned on his machine, and the app said so plainly instead of failing silently. The Windows layer is therefore no longer never-run: startup, tray icon and menu, notifications, and hotkey registration are field-observed working. The TEST-PLAN manual rows were not re-run one by one; the deep rows (scrolling capture end-to-end, OCR accuracy, idle memory) still stand as listed there.

## v0.5.0 additions (compile-verified on Linux; the build was field-run 2026-11-13 — see Field verification)

- Editor whole-image adjustments: `PixelAdjust` (invert, grayscale, brightness/contrast, flips, 90° rotation on raw BGRA buffers) is **Verified (logic)** — exact byte expectations including luma values, clamping, rotation provenance and argument validation. The editor wiring (`TransformBase`: flatten → BGRA convert → op → base swap through the shared `BaseEdit`, so Undo restores base + annotations) is compile-verified only. Brightness/contrast has no editor control yet.
- Editor Redact: `Redactor` detectors (URL/email priority claiming, Luhn-validated cards, octet-validated IPv4, phone shapes, token shape = long + letters + digits) and span→word-box mapping are **Verified (logic)**, including the split-email case (OCR engines often break an address at the '@'; the detector tolerates one space either side). Boxes land in the editor as ordinary undoable annotations. The editor wiring is compile-verified only.
- Editor Copy text (OCR): `OcrText` reading-order assembly is **Verified (logic)** (line grouping by median-height tolerance, ordering, blank filtering). The WinRT call site (`OcrService`: PNG hand-off to BitmapDecoder, OcrEngine from user profile languages) is compile-verified only; recognition quality is the OS engine's and varies with installed language packs.

- Scrolling capture: `ScrollStitch` (strip planning, composition, overlap detection) is **Verified (logic)** — pixel-provenance tests reproduce a source image exactly from overlapping strips, and the plan/anchor/edge cases are pinned. The app layer (`ScrollCapture`: ScrollPattern driving, wheel injection via SendInput, focus handling) is compile-verified only; TEST-PLAN.md has the manual rows. Known design limits: fixed-position headers inside a scrolled page will repeat in the wheel path (the pattern path is exact); a page that reflows while scrolling can misalign strips.

## v0.25.0 additions (compile-verified on Linux; not run on Windows)
- Callout and Stamp are editor UI only (WPF shapes on the annotation layer); no Core change, so no new self-test checks (484). **Unverified:** bubble layout, tail alignment, long-text wrapping (capped at 360 px), glyph rendering (Segoe UI Symbol). The bubble's top-left, not the tail tip, sits at the click.

## v0.24.0 additions (compile-verified on Linux; not run on Windows)
- Core `HueShift` and `ColorBalance`: **Verified** by 7 self-test checks (identity, wrap, green-dominant at 120 degrees, grey and alpha preserved, clamps). Hue uses the common Rec.601 rotation matrix, an approximation, not a perceptual HSL rotation. Dialog: Unverified. No live preview; apply, look, Undo.

## v0.23.0 additions (compile-verified on Linux; not run on Windows)
- Core `LoupeView.Zoom`: **Verified** by 5 self-test checks. Editor Magnify tool (`ImageBrush` with a relative viewbox): **Unverified**. Zoom fixed at 2x. Assumes the capture bitmap maps 1:1 to layer units (96 dpi), as the rest of the editor does.

## v0.22.0 additions (compile-verified on Linux; not run on Windows)
- Editor Spotlight tool (`CombinedGeometry` Exclude over the layer, 55% black). UI only, no Core change: **Unverified**. Dim strength is fixed. Window MinWidth raised to 960 (1020 from v0.23) so the tool cells fit.

## v0.21.0 additions (compile-verified on Linux; not run on Windows)
- Core `Saturation` and `Sharpen`: **Verified** by 9 self-test checks (identity, grey, clamp, flat/edge, alpha, bad input). Editor menu items: Unverified (not run). Sharpen strength (0.8) and colour step (±30%) are fixed guesses, not tuned by eye.

## v0.20.0 additions (compile-verified on Linux against .NET 10; not run on Windows)
- Editor chrome restyled (`EditorChrome`): icon tool cells, key hints, swatches, slider, dark menus, status bar, themed dialogs. Build 0 warnings, 463 self-test checks pass (no Core change).
- **Unverified:** every pixel of it. XAML control templates are parsed at runtime (`XamlReader`), so a template typo would only show when the editor opens. High Contrast variant and hotkeys likewise not run.
- Removed Apply B/C sliders; Brightness / contrast is now a dialog.

## v0.19.0 additions (compile-verified on Linux against .NET 10; not run on Windows)
- `AreaMask` bounds and masking are self-tested. The Region picker's A and Backspace keys, the extra overlay outlines and the dim holes are Windows-layer code that has never run on hardware. Multi-area works within one capture; there is no scrolling or freehand variant, and a delay countdown re-resolves only the single-area case.

## v0.18.0 additions (compile-verified on Linux against .NET 10; not run on Windows)
- `ImageCut` strip removal and the drag-to-band decision are self-tested. The tool's drag feedback and the swap-in through `TransformBase` are unverified on hardware. One strip per drag; no zigzag or torn join along the seam.

## v0.17.0 additions (compile-verified on Linux against .NET 10; not run on Windows)
- `EffectSettings` and the new preference clamps are self-tested, including that automatic values equal the old hard-coded ones. The Effects… dialog (validation messages, save on OK) is unverified on hardware. Settings apply to all four effects at once; there is no per-side choice for Torn/Wave.

## v0.15.0 additions (compile-verified on Linux against .NET 10; not run on Windows)
- `EdgeEffects` pixel math is self-tested (size, depth limit, determinism, side selection, transparency). The editor buttons are wired through the existing `TransformBase` path and are unverified on hardware. Effects cut all four sides at a fixed depth; there is no per-side or depth setting yet.

## v0.14.0 additions (compile-verified on Linux against .NET 10; not run on Windows)
- Movable mode bar: placement math and grip hit-testing are self-tested; the live drag (hook move events, per-monitor DPI, save on release) is unverified on hardware.
- Known limits: the bar slides along the top edge only (no side or bottom docking), and stays on the monitor under the cursor when it opens; the stored fraction applies to every monitor.

## v0.13.0 additions (compile-verified on Linux against .NET 10; not run on Windows)
- Mode bar restyle (`ModeBar.cs`, `OverlayWindow.SetModeBar`, `ModeBarLayout`): visual only. Hit-testing math is covered by the self-tests; rendering, the drop shadow, the High Contrast look and icon legibility at 100-200% scale are unverified on hardware.
- Known gaps: the bar has no keyboard focus ring because it is not tabbable (keys are direct hotkeys); there is no persistent selected state because choosing a tool leaves the bar.

## v0.12.0 additions (compile-verified on Linux against .NET 10; not run on Windows)
- `ColorFormat` and `ImageFrames` (border, shadow) are **Verified (logic)**: exact bytes, margins, clamping, determinism and the opacity-0 and transparent-image cases are in the self-test.
- Colour picker overlay, magnifier, key handling, clipboard text and sampling a desktop snapshot taken before the overlays open are **unverified on hardware**. The colour comes from the capture backend, so HDR or colour-managed displays may differ slightly from what the eye sees; there is no HDR handling.
- Editor Frame / Shadow buttons use fixed proportions (border ≈ 1/60 of the short side; blur ≈ 1/40, 4-24 px, offset about half the blur plus 2); no dialog yet. Shadow on a JPG flattens to black.

## v0.11.0 additions (compile-verified on Linux against .NET 10; not run on Windows)
- Print Screen recovery: `PrintScreenKey.Restore` / `RestoreFromFile`, ProcessExit and UnhandledException hooks, `--restore-printscreen`, tray item. Logic is simple but **unverified on hardware**; a hard kill (Task Manager, power loss) still skips every hook, so `--restore-printscreen` and the next clean exit are the only recovery there.
- Recorder mux fallback and the "keep the raw video" path: **unverified** (needs a forced mux failure).
- Accessibility: contrast ratios are **Verified (logic)** by the self-test; UIA names, High Contrast mapping and the text-box focus ring are **unverified** — need a Narrator pass and a High Contrast theme pass.
- Workflow trust: `WorkflowTrust`, `CliArgs`, `WorkflowPlan.ExpandArgs/QuoteArg`, `LogPolicy` are **Verified (logic)**. A blank hash is accepted, so a config planted *before* the first run is not caught.
- .NET 10: builds with 0 warnings; WGC/WinRT interop behaviour on .NET 10 is **unverified at runtime**.

## v0.4.0 additions (compile-verified on Linux only; not run on Windows)
- PDF export: `PdfWriter` is **Verified (logic)** — structure, xref offsets, stream length and Flate round-trip are self-tested, and a sample file was verified with Poppler (`pdfinfo`: title/page size; `pdfimages`: embedded image extracted pixel-exact; a `pdftoppm` page render resamples and is not pixel-exact on noise, which is a renderer artifact, not a file defect). The WPF BGR→RGB call site is compile-verified only.
- CI workflow added (windows-latest, build + self-test) — **not yet live**: it takes effect on push, and workflow files on this account have previously needed a browser-side approval.
- Reliability bundle: repeat-last persistence, unique auto-save names, single-buffer capture pixels (`CapturePixels`, byte-tested against the previous algorithm's semantics), and 30-day log retention — all **Verified (logic)**; their Windows call sites are compile-verified only.
- Animated GIF (interval runs): the Core `GifWriter` is **Verified (logic)** — the self-test decodes its output with an independent test-side decoder (structure, delays, loop block, pixel round-trips incl. dictionary-growth/clear cases), and a sample file was additionally decoded pixel-exact by Pillow (frames, 2 s delays, loop-forever). The WPF quantisation step (`FormatConvertedBitmap` to the halftone palette) and the tray/CLI wiring are compile-verified only — **Unverified** on Windows. Palette is 256 colours, so gradients will band; that is inherent to GIF.
- Export formats BMP/GIF/TIFF: format selection, alias canonicalisation, alpha-flatten policy and file naming are **Verified (logic)** in `ImageFormats` (self-test). The WPF encoder calls (`BmpBitmapEncoder`, `GifBitmapEncoder`, `TiffBitmapEncoder` with LZW) are compile-verified only — **Unverified** on Windows until the TEST-PLAN export-format rows are run. GIF output is a single still frame. This also fixes a pre-existing mislabel: `--out file.bmp` (etc.) previously received PNG bytes.

## v0.3.0 additions (compile-verified on Linux only; not run on Windows)
- Fixed-size region picker: unverified on hardware (cursor follow, arrow nudge, clamp across mixed DPI).
- Interval capture: unverified on hardware (PeriodicTimer loop, 3-failure stop, tick skipped while a capture is open). "Current monitor" is fixed at start.
- Per-monitor hotkeys: removed 2026-11-13 with all configurable hotkeys (per-monitor capture remains on the mode bar's `1`..`9` keys and the CLI's `--monitor`).
