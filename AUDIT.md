# Audit — v0.2.0 (blueprint v2 + roadmap phase 1 slice)

Evidence labels: **Verified** (run and checked), **Partial** (compiled, reviewed, not run), **Unverified** (not run, depends on real Windows), **Not implemented**.

## What was actually done
- Compiled for `net8.0-windows10.0.19041.0` (cross-build on Linux): 0 errors, 0 warnings, warnings-as-errors on.
- Ran the self-test project: 80 passed, 0 failed (Linux).
- **Nothing in the Windows layer has been executed.** No overlay, hook, UI Automation, capture, clipboard or editor code has run on a real machine.

## Blueprint coverage

| Blueprint item | State | Evidence / note |
|---|---|---|
| §1 state machine | Verified (logic) | `AppStateMachine`, tested; router uses it |
| §1.1 Per-monitor V2 DPI | Partial | `SetProcessDpiAwarenessContext` at start of `Main`. The SDK refuses DPI keys in the manifest when WinForms is referenced (WFAC010), so there is no manifest entry. |
| §1.2 hotkeys | Partial | `RegisterHotKey`, configurable, failure reported. Win+Shift+X/F untested for conflicts. |
| §1.3 UIA pre-warm | Partial | STA worker touches `RootElement` at start |
| §1.4 D3D11 / WGC pre-warm | Not implemented | see Deviations |
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
| §6 export | Partial | PNG, JPEG, atomic write |
| §11 threading | Partial | UIA on STA thread, overlays on UI thread |
| §11 watchdog 60 s | Partial | |
| §11 security / privacy | Partial | no network code, no UIA tree persisted, SetWindowDisplayAffinity on overlays and chip |
| §11 accessibility | Partial | keyboard-only flow, high-contrast mode, UIA live regions on hint and countdown; screen-reader behaviour of a non-activating window is unverified |
| §13 acceptance criteria (latency, ±1 px, Chrome/VS Code/etc.) | Unverified | see docs/TEST-PLAN.md |

## Added after the blueprint (v0.2)
| Item | State | Note |
|---|---|---|
| Active-window hotkey | Unverified | `GetForegroundWindow` + extended frame bounds; refuses SharpSnip's own windows; no check for fullscreen/exclusive apps |
| Repeat-last hotkey | Unverified | stores last physical rectangle in memory only; lost on restart; ignores display changes except clipping to the current desktop |
| Default hotkeys Win+Shift+W / Win+Shift+R | Unverified | may be taken by other software; failure is reported and Settings can change them |

## Deviations from the blueprint
1. **Capture uses GDI BitBlt only; Windows.Graphics.Capture is not implemented.** WGC needs D3D11 interop and a frame pool; it was left out of v0.1 rather than shipped untested. Effects: no DRM-aware black-region detection (protected windows may capture as black without a warning), no HDR "converted" badge, hardware-accelerated or exclusive-fullscreen content may not capture. Planned for Phase 1.
2. **MSAA fallback is reduced to window-bounds detection.** If `AutomationElement.FromPoint` fails, the picker reads the window rectangle and then switches to the region picker, as §3A.3 requires for opaque apps.
3. **Own-overlay hit test.** Overlays are click-through, but if UIA still returns an overlay element, the picker finds the real window by z-order and walks down to the child under the cursor. Unverified.
4. **WebP export not implemented.** WPF has no WebP encoder; adding one needs a package, which the charter forbids without approval.
5. **"Share" button is "Reveal"** (save, then show in Explorer). The Windows share sheet needs WinRT interop; deferred.
6. **Esc behaviour.** Esc inside a picker returns to the mode menu (§3A/§3D.3); Esc in the menu ends the session (§1). §13 says "Esc from any picker returns to IDLE". Both are honoured in the sense that no hook, timer or overlay survives; pressing Esc twice reaches Idle. Right-click behaves like Esc.
7. **Overlays are not re-shown after capture** (§4.6). The editor opens instead, or a toast appears.
8. **Default folder is `Pictures\SharpSnip`**, not `Pictures\Sniptool` (the blueprint name was a placeholder).
9. **Hotkey hook scope.** Keys pressed with Ctrl/Alt/Win held are never swallowed so system shortcuts keep working; all other keys (except bare modifiers) are swallowed while a picker is active.
10. **Loupe needs OS-level capture exclusion.** On Windows older than 10 2004 the region picker runs without the magnifier.

## Known limitations
- Elevated (administrator) windows: UI Automation from a non-elevated SharpSnip is blocked by UIPI, so those apps fall back to region mode.
- Per-pixel correctness at the seam of mixed-DPI monitors is untested.
- WPF windows moved across monitors with different DPI can be resized by the framework; the overlay re-applies exact physical bounds in `OnDpiChanged`. Unverified.
- No CI workflow (workflow files need browser-side approval on this account).
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

## v0.3.0 additions (compile-verified on Linux only; not run on Windows)
- Fixed-size region picker: unverified on hardware (cursor follow, arrow nudge, clamp across mixed DPI).
- Interval capture: unverified on hardware (PeriodicTimer loop, 3-failure stop, tick skipped while a capture is open). "Current monitor" is fixed at start.
- Per-monitor hotkeys: default `Ctrl+Alt+Shift+1..4` chosen because `Win+Shift+digit` is reserved by Windows. Registration conflicts unverified.
