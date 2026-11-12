================================================================================
  SNIP TOOL "ELEMENT PICKER" — WORKFLOW BLUEPRINT   (v2)
================================================================================
  Target: Windows 10/11 (x64)
  Stack : C# (.NET 8) + WPF (overlay UI) + FlaUI (UIA wrapper)
          + Windows.Graphics.Capture (capture) + SharpHook (global input)

  CHANGELOG v1 → v2
    • Added PHASE 3D — FULL-SCREEN PICKER (was referenced in the state
      machine / mode menu but never specified).
    • Added dedicated full-screen hotkey (zero-UI, one-shot capture).
    • Added capture delay timer (0/3/5/10 s) — essential for grabbing
      transient UI such as open menus, tooltips and hover states.
    • Added multi-monitor / mixed-DPI stitching spec for the virtual
      desktop.
    • Extended §7 capture matrix, §10 concerns, §11 file skeleton and
      §12 acceptance criteria accordingly.

────────────────────────────────────────────────────────────────────────────
 0. SYSTEM ARCHITECTURE (component map)
────────────────────────────────────────────────────────────────────────────

  ┌───────────────────────────────────────────────────────────────────────┐
  │                        SNIP TOOL PROCESS                              │
  │                                                                       │
  │  ┌────────────┐   ┌──────────────┐   ┌────────────┐   ┌───────────┐  │
  │  │ Hotkey     │──▶│ Mode Router  │──▶│ Capture    │──▶│ Editor /  │  │
  │  │ Listener   │   │ (State Mach) │   │ Engine     │   │ Exporter  │  │
  │  └────────────┘   └──────┬───────┘   └────────────┘   └───────────┘  │
  │                          │                                            │
  │      ┌───────────┬───────┼───────────┬───────────────┐               │
  │      ▼           ▼       ▼           ▼               ▼               │
  │  ┌────────┐ ┌────────┐ ┌────────┐ ┌──────────────┐ ┌──────────────┐  │
  │  │Element │ │Region  │ │Window  │ │Full-screen   │ │Delay / Timer │  │
  │  │Picker  │ │Picker  │ │Picker  │ │Picker (§3D)  │ │(shared,§3D.6)│  │
  │  │(UIA)   │ │(free)  │ │(HWND)  │ │(monitors)    │ │              │  │
  │  └───┬────┘ └───┬────┘ └───┬────┘ └──────┬───────┘ └──────┬───────┘  │
  │      └──────────┴──────────┴─────────────┴────────────────┘          │
  │                          ▼                                            │
  │  ┌──────────────────────────────────────────────┐                    │
  │  │  Overlay Window (WPF, per-monitor, topmost)  │                    │
  │  │  – dimmer / highlight rect / hint bar        │                    │
  │  │  – monitor outlines (full-screen mode)       │                    │
  │  └──────────────────────────────────────────────┘                    │
  └───────────────────────────────────────────────────────────────────────┘
              │                              │
              ▼                              ▼
   ┌──────────────────┐          ┌────────────────────────┐
   │ UIA / MSAA       │          │ Windows.Graphics.Capture│
   │ (AutomationRoot) │          │ (or GDI BitBlt fallback)│
   └──────────────────┘          └────────────────────────┘

────────────────────────────────────────────────────────────────────────────
 1. STATE MACHINE
────────────────────────────────────────────────────────────────────────────

   IDLE ──(hotkey)──▶ MODE_MENU
     ▲                   │
     │      ┌────────────┼───────────┬──────────────┬──────────────┐
     │      ▼            ▼           ▼              ▼              ▼
     │  ELEMENT      REGION      WINDOW        FULLSCREEN     FULLSCREEN
     │  PICKING      PICKING     PICKING       _PICKING       _INSTANT
     │      │            │           │              │        (own hotkey)
     │      │            │           │        ┌─────┴─────┐        │
     │      │            │           │        ▼           ▼        │
     │      │            │           │   (delay 0s)  (delay>0s)    │
     │      │            │           │        │      countdown    │
     │      │            │           │        └─────┬─────┘        │
     │      └────┬───────┴─────┬─────┴──────────────┴──────────────┘
     │           ▼             ▼             ▼
     │        CAPTURED ──▶ EDITING ──▶ EXPORTING
     │           │            │             │
     │           └──(Esc)─────┴──(Esc)──────┘
     │                          │
     └─────────(done/cancel)────┘

   NOTE: FULLSCREEN_INSTANT skips MODE_MENU and every picker UI entirely
         (one keypress = capture). FULLSCREEN_PICKING shows the overlay so
         the user can choose which monitor / all monitors and set a delay.

────────────────────────────────────────────────────────────────────────────
 2. PHASE 1 — INITIALIZATION (app startup, once)
────────────────────────────────────────────────────────────────────────────

  [1.1] SetPerMonitorV2DpiAwareness()          // correct coords on multi-DPI
  [1.2] Register global hotkeys (SharpHook HotkeyHook / RegisterHotKey)
        • Primary   : Win+Shift+X  → MODE_MENU
        • Full-screen: Win+Shift+F → FULLSCREEN_INSTANT (configurable,
                       set to Print Screen if the user prefers legacy feel)
  [1.3] Pre-warm UIA client (Automation.RootElement) in background thread
  [1.4] Pre-warm D3D11 device + GraphicsCaptureSession factory
  [1.5] Enumerate monitors; cache display config (bounds, DPI, orientation)
        and subscribe to WM_DISPLAYCHANGE to refresh
  [1.6] Load user prefs (output folder, image format, hotkey overrides,
        default full-screen target, default delay)

────────────────────────────────────────────────────────────────────────────
 3. PHASE 2 — ACTIVATION (hotkey pressed)
────────────────────────────────────────────────────────────────────────────

  [2.1] Snapshot all monitor bounds (EnumDisplayMonitors)
  [2.2] Create ONE transparent overlay window per monitor
        Style: WS_EX_LAYERED | WS_EX_TOPMOST | WS_EX_TOOLWINDOW
        Content: dimmer (rgba 0,0,0,~120) + live highlight rect + hint bar
  [2.3] Show "mode menu" HUD near cursor:
          ┌──────────────────────────────────────────────────────┐
          │  [E] Element   [R] Region   [W] Window               │
          │  [F] Full screen (all monitors)                      │
          │  [D] Full screen (this monitor)                      │
          │  [1..9] Full screen (monitor N)                      │
          │  Space = toggle delay 0s/3s/5s/10s                   │
          └──────────────────────────────────────────────────────┘
  [2.4] Route to picker based on selection
        (E → §4, R → §5, W → §6, F/D/1..9 → §7 / PHASE 3D)

────────────────────────────────────────────────────────────────────────────
 4. PHASE 3A — ELEMENT PICKER  ★ core new feature
────────────────────────────────────────────────────────────────────────────

  [3A.1] Start low-level mouse hook (WH_MOUSE_LL) + optional keyboard hook
  [3A.2] Throttle: sample cursor position every ~40–60 ms (not every move)

  LOOP (on each throttled cursor sample):
     ┌─────────────────────────────────────────────────────────────────┐
     │ a. Convert screen px → UIA Point (handles DPI per monitor)      │
     │ b. elem = AutomationElement.FromPoint(point)                    │
     │ c. If elem == null → try MSAA fallback (AccessibleObjectFromPt) │
     │ d. Read rect  = elem.Current.BoundingRectangle                  │
     │ e. Read meta  = ControlType, Name, AutomationId, ClassName      │
     │ f. If rect empty/invalid → try parent (walk up N levels)        │
     │ g. Compute "snap candidate" set:                                │
     │      • the element itself                                       │
     │      • nearest ancestors of a "container" control type          │
     │        (ToolBar, Pane, Group, List, Tree, Tab, MenuBar)         │
     │ h. Draw highlight rect on overlay (animated, non-blocking)      │
     │ i. Update hint bar:                                             │
     │      "ToolBar ▸ 'Address bar'   [Tab] cycle · [PgUp/Dn] parent" │
     └─────────────────────────────────────────────────────────────────┘

  USER INPUT while picking:
     • [Left Click]      → CONFIRM current element → goto PHASE 4
     • [Tab]             → cycle to next sibling element
     • [PgUp / PgDn]     → walk up / down the UIA tree (ancestor↔child)
     • [Ctrl+Wheel]      → expand/contract selection by control type
     • [F]               → switch to full-screen picker (§7) for this monitor
     • [R]               → switch to REGION picker (fallback)
     • [Esc]             → cancel → back to MODE_MENU

  [3A.3] On CONFIRM:
        rect = currentElement.BoundingRectangle
        If rect == empty or app opaque → auto-fallback to REGION picker
        → store { rect, uiaPath, meta } → goto PHASE 4

────────────────────────────────────────────────────────────────────────────
 5. PHASE 3B — REGION PICKER (fallback / classic Snipping Tool)
────────────────────────────────────────────────────────────────────────────

  [3B.1] Hide dimmer for full-screen live preview
  [3B.2] Rubber-band selection with 8 resize handles + live size readout
  [3B.3] Magnifier loupe at cursor (pixel-precise, zoom 6–10×)
  [3B.4] Auto-snap to detected window edges (EnumWindows + GetWindowRect)
  [3B.5] [Enter / double-click] → CONFIRM  ·  [Esc] → cancel

────────────────────────────────────────────────────────────────────────────
 6. PHASE 3C — WINDOW PICKER
────────────────────────────────────────────────────────────────────────────

  [3C.1] On cursor hover, resolve HWND under point (WindowFromPoint +
         GetAncestor(GA_ROOT))
  [3C.2] Highlight client or full window (user toggle: [C] client only)
  [3C.3] Confirm → rect = GetExtendedFrameBounds(hwnd)

────────────────────────────────────────────────────────────────────────────
 7. PHASE 3D — FULL-SCREEN PICKER  ★ NEW IN v2
────────────────────────────────────────────────────────────────────────────

  Purpose: capture an entire monitor, a subset of monitors, or the whole
  virtual desktop — with no region drawing required. This is the
  "screenshot everything" path and the fastest route to a clipboard image.

  Two entry points:

  ┌────────────────────────────────────────────────────────────────────────┐
  │ 3D.A  FULLSCREEN_INSTANT  (dedicated hotkey, e.g. Win+Shift+F)         │
  │        • No overlay, no menu.                                          │
  │        • Target = user pref: "monitor under cursor" (default) or       │
  │          "all monitors".                                               │
  │        • Honours the default delay (0 s unless user set one).          │
  │        • Straight to PHASE 4. Latency target: <250 ms hotkey→clipboard.│
  ├────────────────────────────────────────────────────────────────────────┤
  │ 3D.B  FULLSCREEN_PICKING  (from MODE_MENU: F / D / 1..9)               │
  │        • Overlay stays up so the user can pick a target and a delay.   │
  └────────────────────────────────────────────────────────────────────────┘

  [3D.1] TARGET MODEL
        enum FullScreenTarget {
          AllMonitors,      // virtual desktop = union of every monitor
          CurrentMonitor,   // monitor under the cursor at activation
          MonitorIndex(n)   // explicit, via keys 1..9
        }
        Default = CurrentMonitor (matches Snipping Tool / Snip & Sketch
        behaviour, where the current display is captured).

  [3D.2] OVERLAY BEHAVIOUR (only in 3D.B)
        • Dimmer drawn on every monitor EXCEPT the target, which is shown
          clear/undimmed and outlined with a 2 px accent border.
        • Each monitor gets a small label chip: "1 · 2560×1440 · 150% ·
          PRIMARY" so the user knows exactly which display is targeted.
        • Hint bar (on the target monitor):
            "Full screen · Monitor 1 of 3 · [Tab] next · [A] all ·
             [Space] delay: 3 s · [Enter] capture · [Esc] cancel"

  [3D.3] USER INPUT while in 3D.B
        • [Tab / Shift+Tab]  → cycle target monitor (wraps around)
        • [A]                → target = AllMonitors (virtual desktop)
        • [1..9]             → jump to monitor N
        • [Space]            → cycle delay 0 → 3 → 5 → 10 → 0 s
        • [Enter / LMB]      → CONFIRM → PHASE 4
        • [Esc]              → cancel → MODE_MENU
        • [E / R / W]        → hop to element / region / window picker

  [3D.4] DELAY / COUNTDOWN TIMER  (shared by all pickers, primary use = 3D)
        • On CONFIRM with delay > 0:
            a. Hide all overlay windows immediately (fade-out 80 ms).
            b. Show a small always-on-top countdown chip in a screen corner
               ("Capturing in 3… 2… 1… [Esc] cancel") — tool-window style,
               excluded from capture via SetWindowDisplayAffinity.
            c. Wait N seconds (cancellable). During the wait the app keeps
               the mouse/keyboard hook live so Esc aborts cleanly.
            d. Fire PHASE 4 with the pre-resolved rect set.
        • Rationale: lets the user open a menu, trigger a tooltip, or set a
          hover state before the shutter fires — the single most-requested
          full-screen capture feature.

  [3D.5] TARGET RECT RESOLUTION
        AllMonitors:
            vx = min(m.Left)  vy = min(m.Top)
            vw = max(m.Right) - vx
            vh = max(m.Bottom) - vy
            rect = { vx, vy, vw, vh }        // in PHYSICAL pixels
        CurrentMonitor / MonitorIndex(n):
            rect = monitor.Bounds              // PHYSICAL pixels
        • All coordinates stay in physical pixels from here on; DPI
          conversion happens only at UI presentation time.

  [3D.6] EDGE CASES
        • Monitor unplugged / display change during countdown
          → re-enumerate monitors, recompute rect, continue (no crash).
        • Capture-protected windows (DRM) inside the target
          → those areas come back black; warn once via toast.
        • Overlay self-capture prevented by hiding overlays before the
          frame is pulled (see §4.1 in PHASE 4).
        • Clamshell / disabled monitors → filtered out of target list.
        • HDR-enabled display → capture in SDR-compatible format and note
          a "converted from HDR" badge in the editor.

────────────────────────────────────────────────────────────────────────────
 8. PHASE 4 — CAPTURE
────────────────────────────────────────────────────────────────────────────

  [4.1] Hide all overlay windows (fade-out 80 ms) to avoid self-capture
  [4.2] Choose capture path by scope:
        ┌────────────────────────────┬────────────────────────────────┐
        │ single monitor region      │ Windows.Graphics.Capture       │
        │                            │  → Direct3D11CaptureFramePool  │
        ├────────────────────────────┼────────────────────────────────┤
        │ region spans >1 monitor    │ per-monitor frame pools, each  │
        │ (incl. AllMonitors full-   │ rendered at native resolution  │
        │  screen capture, §3D.5)    │ into one virtual-desktop       │
        │                            │ canvas (see stitching below)   │
        ├────────────────────────────┼────────────────────────────────┤
        │ GDI-only app / protected   │ BitBlt fallback (GDI)          │
        ├────────────────────────────┼────────────────────────────────┤
        │ hardware/DRM-protected     │ black region + "cannot capture"│
        │                            │ toast                          │
        └────────────────────────────┴────────────────────────────────┘

  [4.2.1] MULTI-MONITOR STITCHING (AllMonitors path)
        • Allocate canvas: width = vw, height = vh (physical px, from §3D.5)
        • For each monitor m:
              dst = (m.Left - vx, m.Top - vy)
              blit m's captured frame 1:1 at dst
              (no scaling — each monitor keeps its native pixel density)
        • Gaps in non-rectangular arrangements (L-shaped desks) are filled
          with transparent or user-chosen matte colour; flagged in metadata.
        • Mixed DPI is handled naturally because we never rescale; the
          resulting PNG may be very large — warn if > 100 MP.

  [4.3] Crop frame buffer to target rect (single-monitor case)
  [4.4] Convert to BGRA8 → BitmapSource / PNG buffer
  [4.5] Push to clipboard (PNG + DIB) immediately  ✓ Snipping-Tool parity
  [4.6] Show overlays again (fade-in) → goto PHASE 5
        (FULLSCREEN_INSTANT skips §4.6 and goes straight to §9 export with
         a "copied to clipboard" toast — no editor window.)

────────────────────────────────────────────────────────────────────────────
 9. PHASE 5 — EDITING (mini editor window)
────────────────────────────────────────────────────────────────────────────

  [5.1] Show captured image on canvas, fit-to-window
  [5.2] Toolbar: Pen · Highlighter · Arrow · Rect · Ellipse · Text ·
                Blur · Crop · Undo/Redo · Copy · Save · Share
  [5.3] Attach metadata side-panel: capture mode (Element / Region / Window /
        Full screen), monitor count, app + UIA Name/ControlType when known,
        rect in physical px, delay used — useful for bug reports
  [5.4] [Ctrl+S] save · [Ctrl+C] copy · [Esc] close → IDLE

────────────────────────────────────────────────────────────────────────────
10. PHASE 6 — EXPORT
────────────────────────────────────────────────────────────────────────────

  [6.1] Default path: %USERPROFILE%\Pictures\Sniptool\<timestamp>.png
  [6.2] Options: PNG / JPEG / WebP / clipboard-only / auto-copy
  [6.3] Toast notification with "Open folder" + "Copy path"
  [6.4] Return to IDLE

────────────────────────────────────────────────────────────────────────────
11. CROSS-CUTTING CONCERNS
────────────────────────────────────────────────────────────────────────────

  DPI / MULTI-MONITOR
    • PerMonitorV2 awareness; never assume 96 DPI.
    • Keep overlay windows per-monitor; convert coords with
      PhysicalToLogicalPoint / LogicalToPhysicalPoint as needed.
    • Full-screen canvas is always built in PHYSICAL pixels (§4.2.1).

  THREADING
    • UIA queries → dedicated STA thread (UIA is slow & can block)
    • Capture    → MTA / thread-pool
    • Overlay UI → WPF dispatcher thread only
    • Delay timer→ System.Threading.Timer, cancellable
    • Use CancellationToken when switching modes rapidly

  PERFORMANCE
    • Cache last N UIA elements; debounce cursor sampling (≥40 ms)
    • Cache bounding-rect lookups for 200 ms (avoid tree thrash)
    • Overlay uses D3D-backed WPF surface, not per-pixel redraws
    • Full-screen capture target: ≤250 ms hotkey→clipboard on 2 monitors

  RELIABILITY / FALLBACKS
    • If UIA throws / times out (≤1 s budget) → MSAA → Region picker
    • If WGC unsupported (Win < 10 1903) → GDI BitBlt
    • If overlay covers target (rare) → temporarily set click-through
    • Watchdog: kill hook if no input for 60 s (prevents stuck overlay)
    • Countdown survives display change; abort cleanly if target vanishes

  SECURITY / PRIVACY
    • Never persist raw UIA tree across sessions
    • No telemetry; clipboard cleared on user request only
    • Respect capture-exclusion (SetWindowDisplayAffinity on our overlay
      AND on the countdown chip)

  ACCESSIBILITY
    • Keyboard-only flow (E/R/W/F/D/1..9/Tab/Space/Enter/Esc) fully usable
    • High-contrast highlight mode + adjustable dimmer opacity
    • Screen-reader announcements via AutomationPeer for hint bar and
      countdown ("Capturing in 3 seconds")

────────────────────────────────────────────────────────────────────────────
12. DIRECTORY / FILE SKELETON (suggested C# layout)
────────────────────────────────────────────────────────────────────────────

  /src
    Program.cs                     // entry, DPI awareness, single-instance mutex
    /Core
      HotkeyManager.cs             // SharpHook or RegisterHotKey (2 hotkeys)
      ModeRouter.cs                // state machine (§1)
      CaptureEngine.cs             // WGC + GDI fallback (§8)
      VirtualDesktopStitcher.cs    // multi-monitor canvas assembly (§4.2.1)
      DelayTimer.cs                // cancellable countdown + chip UI (§3D.4)
      MonitorEnumerator.cs         // display config, WM_DISPLAYCHANGE (§1.5)
      Preferences.cs
    /Pickers
      ElementPicker.cs             // §4  (UIA + mouse hook + snapshot)
      UiaTreeWalker.cs             // ancestor/descendant cycling
      RegionPicker.cs              // §5
      WindowPicker.cs              // §6
      FullScreenPicker.cs          // §7  (targets, monitor cycling, §3D.5)
    /Overlay
      OverlayWindow.xaml(.cs)      // WPF per-monitor overlay
      HighlightRenderer.cs         // D2D highlight + dimmer
      MonitorLabelChip.xaml        // "1 · 2560×1440 · 150%" labels
      HintBar.xaml                 // HUD text
    /Editor
      EditorWindow.xaml(.cs)       // §9
      Annotator.cs
    /Export
      Exporter.cs                  // §10
      ClipboardWriter.cs

────────────────────────────────────────────────────────────────────────────
13. ACCEPTANCE CRITERIA (definition of "done")
────────────────────────────────────────────────────────────────────────────

  ✔ Hotkey opens mode menu in <150 ms
  ✔ Element hover highlight tracks cursor at ≥20 Hz with <100 ms lag
  ✔ Element cycle (Tab/PgUp/PgDn) is deterministic and reversible
  ✔ Captured pixels match the highlighted rect within ±1 px
  ✔ Works on: Chrome, Explorer, Notepad, VS Code, Win11 Settings
  ✔ Graceful fallback on apps with no UIA (games, some Electron)
  ✔ Multi-monitor + mixed DPI: coordinates correct on all screens

  ★ Full-screen specific (§7)
  ✔ Dedicated full-screen hotkey captures to clipboard in <250 ms
    with no visible UI flash
  ✔ Current-monitor capture on a 2-monitor setup grabs ONLY the monitor
    under the cursor (verified by pixel test)
  ✔ All-monitors capture produces one stitched image with correct
    geometry, no gaps or overlaps, on mixed-DPI (100% + 150%) setups
  ✔ L-shaped / non-rectangular monitor arrangement yields correct union
    rect with matte fill for the gap
  ✔ Delay timer 0/3/5/10 s works, countdown is cancellable via Esc, and
    the countdown chip never appears in the captured image
  ✔ Display unplugged mid-countdown does not crash; capture continues
    with the recomputed target
  ✔ Monitor cycling (Tab) and direct monitor jump (1..9) highlight the
    correct display in the overlay

  ✔ Clipboard image available immediately after confirm
  ✔ Esc from any picker (incl. full-screen) returns to IDLE with no
    leaked hooks, timers or overlay windows
================================================================================