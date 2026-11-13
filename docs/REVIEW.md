# Review — 2026-11-13

A full review of SharpSnip as it stands (v0.3.0 on GitHub plus the unreleased
local work), written from the code, the blueprint, the roadmap and the audit —
not from aspirations. Every claim below names its evidence. "Verified" means
run and checked; the Windows application layer has **never been run**, and this
review does not pretend otherwise.

## What SharpSnip is trying to be

- **Purpose** (BLUEPRINT.md, README.md): an offline Windows screenshot tool
  whose differentiator is the **element picker** — hover any control, click,
  capture exactly that UI element's rectangle — plus region/window/full-screen
  capture, multi-monitor mixed-DPI correctness, a delay for transient UI, and
  a small annotation editor whose metadata panel is aimed at bug reports.
- **Audience**: Windows power users and developers/support staff who capture
  UI precisely (the UIA metadata panel, physical-pixel read-outs and CLI
  one-shots all point at technical users), and multi-monitor users ill-served
  by tools that assume one DPI.
- **Constraints** (charter, AUDIT.md): .NET 8 + WPF, **zero third-party
  packages**, zero network/telemetry, Windows 10 1809+, warnings-as-errors,
  pure logic in `SharpSnip.Core` tested anywhere, Windows glue in
  `SharpSnip.App`. Releases (when authorised) follow the portfolio rule:
  packaged with SeanPak, one Setup.exe + one Portable.zip + SHA256SUMS.txt.

## Where it stands — evidence

| Area | State | Evidence |
|---|---|---|
| Pure logic | **Verified** | Self-test passes in full on Linux; covers geometry, stitching, hotkeys, settings quarantine, formats, GIF |
| GIF writer | **Verified (logic)** | Output round-tripped by an independent test decoder and decoded pixel-exact by Pillow (loop + delays) |
| Windows app layer | **Unverified** | AUDIT.md: no overlay, hook, UIA, capture, clipboard or editor code has ever run on a real machine |
| Build | **Verified** | Full solution, Release, warnings-as-errors, on this VM |
| Network surface | **Verified (static)** | Source search: no HTTP/socket APIs anywhere; the only process launches are `explorer.exe` for reveal/open-folder |
| GitHub releases | **None exist** | No tags, no releases on the repo as of this review |
| Repo description/topics | **Missing** | GitHub API shows neither set, although DESCRIPTION.md already contains the intended text |
| README screenshots | **None** | By design (real captures only); the capture list is in TEST-PLAN.md |

## Competitive position — honestly

- **Snipping Tool** (built into Windows): fewer capture modes, no element
  picking, no CLI, no metadata. SharpSnip already exceeds it on paper.
- **ShareX** (free): vastly broader — scrolling capture, OCR, workflows,
  dozens of destinations. Beating it on breadth is not a sane goal for a
  zero-dependency offline tool; beating it on **focus, startup weight and
  element accuracy** is.
- **Greenshot** (free): strong editor and plugin ecosystem; no element picker.
- **Snagit** (paid): scrolling capture, library, polish. The paid bar.

SharpSnip's defensible edge is the combination nobody else in the free set
has: **element-accurate capture + capture metadata for bug reports + fully
offline + no dependencies**. Every priority below either protects that edge
(prove it works on real hardware) or extends it (scrolling, OCR, PDF).

## Gaps, risks, opportunities — prioritised

### P0 — Field verification programme (risk: everything else is built on sand)
Nothing in the app layer has run on Windows. The blueprint's acceptance
criteria (menu < 150 ms, hotkey→clipboard < 250 ms, ±1 px accuracy, the five
named apps) are all unmeasured, and the TEST-PLAN's manual rows are all
unchecked. A single UIA or DPI surprise on real hardware could invalidate
assumptions the rest of the roadmap builds on.
**Measure**: TEST-PLAN rows pass on Windows 10 and 11, on a mixed-DPI
two-monitor setup; the seven README screenshots exist and are real captures.
**Trade-off**: needs SEAN's machine time; nothing else substitutes.

### P0 — Windows.Graphics.Capture backend (risk: black captures)
GDI BitBlt (the only backend) returns black or stale content for many
hardware-accelerated windows, protected content and exclusive-fullscreen
apps, with no warning. Every serious competitor uses WGC on modern Windows.
AUDIT.md deviation 1 already scopes this.
**Measure**: WGC is the default backend on Windows 10 1903+ with GDI
fallback; a hardware-accelerated video region and a game window capture
real pixels, and a protected region produces the documented warning instead
of a silent black rectangle.
**Trade-off**: the largest single engineering item here; WinRT/D3D interop
with no package, and it can only be proven on hardware (see P0 above).

### P1 — Scrolling capture (opportunity: the most-requested capture mode)
Roadmap Phase 1 already plans it: UIA ScrollPattern first, wheel-and-stitch
fallback. It is the one capture mode whose absence users feel weekly.
**Measure**: a long web page/document captures end-to-end; stitched height
matches the content; static-content seams are pixel-exact; dynamic content
(animations, sticky headers) is detected and reported, not silently doubled.
**Trade-off**: stitching quality on moving content is the hard part; the
UIA-first route avoids most of it where the app cooperates.

### P1 — PDF export (opportunity: cheap, already promised by the roadmap)
Single-image PDF via a hand-written writer in Core (the GifWriter pattern:
operation first, byte-level tests, independent-parser verification).
**Measure**: `--out shot.pdf` and Settings format PDF produce files that
open in two independent readers and carry the image losslessly at native
resolution. *Status: implemented as an unreleased local commit after this
review (Poppler-verified; Windows call site still Unverified).* 

### P1 — Hosted CI on windows-latest (risk: regressions land silently)
There is no CI workflow; AUDIT.md notes workflow files need a browser-side
step on this account. Until CI exists, "the gate passed" means "passed on
one Linux VM with cross-compilation".
**Measure**: every PR runs build + self-test on windows-latest and a
packaged smoke (launch, one CLI capture) before merge.
**Trade-off**: account process, not code; one-time setup.
*Status: the workflow file (build + self-test) is committed locally and
goes live on the next push; the packaged-smoke step still needs a field
machine verdict first.*

### P1 — Keep shrinking the untestable layer (risk: App logic rots untested)
The pattern that works — `CapturePixels`, `GifWriter`, `ImageFormats` —
moves behaviour into Core where tests pin it. Remaining logic still sits in
App files (interval orchestration, editor geometry, picker state).
**Measure**: each slice moves or adds zero untested *logic*; new behaviour
lands in Core first, App stays glue.
**Trade-off**: some things (hooks, UIA) cannot move; the line is judgment,
reviewed per slice.

### P2 — Repo presentation (opportunity: five-minute wins, already prepared)
No description, no topics, no releases, no screenshots. DESCRIPTION.md
already holds the exact description and topics to apply; releases wait on
the standing "no releases until SEAN says so".
**Measure**: description + topics set from DESCRIPTION.md at the next
authorised publish; README screenshots after the field programme.

### P2 — OCR: copy text from a capture (opportunity: fits the dev audience)
`Windows.Media.Ocr` ships with Windows — no package, fully offline
(roadmap Phase 4). "Grab the error text" is a daily developer task.
**Measure**: editor gains Copy Text; a known-text fixture recognises
correctly on Windows with the en-US language pack.
**Trade-off**: accuracy is the OS engine's, not ours; language packs vary
by machine and the UI must say so when a language is missing.

### P2 — Editor save-as and annotation depth (roadmap Phase 2/3 items)
Save currently uses the configured format silently; a save-as with an
explicit format choice, step-number stamps and callouts are the editor
items users of competing tools reach for most.
**Measure**: Save As offers every supported format; stamps/callouts are
undoable like every other annotation.
**Trade-off**: editor scope creep is real; each addition must stay
undo-safe and keep the one-window simplicity.

### P3 — Library/history (defer; decision needed)
Roadmap Phase 3's searchable library wants an index. SQLite would be the
portfolio's first package in this repo and needs SEAN's explicit approval
under the dependency rule; a JSON index avoids the dependency but scales
worse. **Recommendation: defer** until the field programme shows how the
tool is actually used; do not build a library speculatively.

### Standing risks (accepted, documented, not actioned)
- Elevated windows block UIA from a non-elevated SharpSnip (UIPI) — region
  fallback is the designed answer; running SharpSnip elevated is not
  recommended as a default.
- GIF is a 256-colour format; animated captures will band on gradients.
  Documented in-app (Settings tooltip) and in the changelog.
- Settings schema is v1 with tolerant loading but no migration framework;
  the first field *removal/rename* must introduce one, with a fixture.

## What this review changed immediately

The following shipped as local commits alongside this review (no PR, no
release, per instruction): BMP/GIF/TIFF export with a tested format policy
(fixing silent PNG mislabelling), animated GIF for interval capture,
persistent repeat-last, collision-proof auto-save names, the single-buffer
capture pixel pipeline, and 30-day log retention. The remaining items above
are ordered so the next authorised steps are: **field verification (P0) →
PDF export (P1, no hardware needed) → WGC design spike (P0, needs
hardware)**.
