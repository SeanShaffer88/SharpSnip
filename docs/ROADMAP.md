# Roadmap

Goal: eventually cover the full feature list Sean supplied (screen capture, recording, annotation, AI/smart tools, library, sharing, utilities).

Legend: ✅ in v0.1 · 🔜 planned, no new dependency · 🟡 uses a Windows built-in API, needs design check · ❓ needs Sean's decision (new package, network, or large scope) · ⛔ conflicts with the offline / no-dependency charter unless Sean opts in per feature.

Order is a proposal. Each phase ends in a tagged release with README, changelog and tests updated.

## Phase 0 — v0.1 (this release)
Blueprint v2: element, region, window, full-screen, multi-monitor, delay, mini editor, clipboard, export.

## Phase 1 — v0.2 Capture breadth
| Feature | Status |
|---|---|
| Full-screen / multi-monitor / monitor-specific capture | ✅ (per-monitor *hotkeys* 🔜) |
| Active-window capture hotkey | ✅ v0.2 (untested on Windows) |
| Rectangular region | ✅ |
| Fixed-size / exact-dimension region | ✅ v0.3 |
| Repeat last capture / last region | ✅ v0.2 (session only; untested on Windows) |
| Delayed capture | ✅ |
| Interval capture (every N s) | ✅ v0.3 |
| Object / element / window capture | ✅ |
| Window-control capture | ✅ (element picker) |
| Menu capture (disappearing menus) | ✅ via delay; hotkey-while-menu-open 🔜 |
| Multiple-area capture (several regions, one action) | 🔜 |
| Freehand and polygon capture | 🔜 (mask drawn into transparent PNG) |
| Scrolling capture (web pages, documents, wide tables) | 🟡 UIA ScrollPattern first, wheel-and-stitch fallback |
| Windows.Graphics.Capture backend (DRM-aware, HDR) | 🟡 replaces GDI as primary |
| Webcam still | 🟡 WinRT MediaCapture |
| Printer capture | ❓ needs a virtual printer driver; large scope |

## Phase 2 — v0.3 Annotation and editing
| Feature | Status |
|---|---|
| Arrows, shapes, text, pen, highlighter, blur/pixelate, crop | ✅ |
| Lines/connectors, speech-bubble callouts, step numbers, stamps | 🔜 |
| Spotlight / dim surroundings, magnify / loupe annotation | 🔜 |
| Redaction (manual) | 🔜 |
| Resize, rotate, flip | 🔜 |
| Borders, frames, drop shadows, torn-edge/wave, cut-out | 🔜 |
| Watermark | 🔜 |
| Brightness / contrast / hue / saturation / balance, grayscale, invert, sharpen | 🔜 |
| Fill, eraser / retouch | 🔜 |
| Eyedropper with HEX / RGB / HTML / C++ / Delphi output, palette generation | 🔜 |
| Quick styles / themes, customisable toolbar | 🔜 |
| Dark mode, hotkey customisation, portable mode | hotkeys ✅, portable ✅, dark mode 🔜 |

## Phase 3 — v0.4 Library, export, utilities
| Feature | Status |
|---|---|
| Local searchable library, tags, filters, sort, recent tray, history gallery | 🔜 (local SQLite would be a new package ❓; JSON index otherwise) |
| Custom file-naming patterns, automatic folder organisation | 🔜 |
| Export PNG / JPG / BMP / TIFF / GIF | 🔜 (all in-box WPF encoders) |
| Export PDF | 🔜 (hand-written single-image PDF writer, no package) |
| Export PSD / TGA | ❓ custom writers, low priority |
| Send to external program / open in external editor | 🔜 |
| Automated after-capture tasks (copy, save, run script) | 🔜 |
| Proprietary editable format (layers kept) | 🔜 |
| Insert into Office documents | ❓ needs COM automation per app |
| Screen magnifier, on-screen ruler / pixel measure, colour picker, whiteboard | 🔜 |
| Scheduled captures, context-menu quick actions | 🔜 |

## Phase 4 — v0.5 Text recognition
| Feature | Status |
|---|---|
| OCR (grab / copy text from image), multi-language | 🟡 `Windows.Media.Ocr` ships with Windows, no package; languages depend on installed language packs |
| Text-inside-image search | 🔜 builds on OCR + library index |
| Smart Redact (auto-find emails, numbers, keys) | 🟡 OCR + regex rules, runs fully offline |

## Phase 5 — v0.6+ Recording
| Feature | Status |
|---|---|
| Screen recording (full / window / region), 4K, frame-rate and quality settings, auto-save | ❓ needs a capture + encode pipeline; Windows.Graphics.Capture + Media Foundation H.264 (MP4), no package if done via WinRT/MF |
| System audio, microphone | ❓ WASAPI loopback via P/Invoke, or a package (NAudio) |
| Webcam picture-in-picture, background blur, camera swap | ❓ |
| Cursor highlight, click animation, on-screen drawing while recording | 🔜 after recorder exists |
| Trim / combine clips, image-sequence → video, frame extraction | ❓ |
| GIF from video / screen activity | 🟡 WPF `GifBitmapEncoder` (limited palette quality) |
| Audio noise removal | ❓ needs a DSP library |

## Needs a decision before any work (⛔ / ❓)
- **Cloud and sharing** (share links, Dropbox/Drive/OneDrive/Box, Slack/Teams, email, social, FTP, YouTube, short links, cloud sync, enterprise SSO/analytics): all need the network. SharpSnip is offline by charter. Proposal: ship as optional, off-by-default, separately toggled modules, each with its own tokens stored via DPAPI. Email via `mailto:` and "share" via the Windows share sheet need no accounts.
- **AI features** (AI Step Capture, AI background removal, AI captions/chapters, AI redaction risk scoring, Simplify, Smart Move): need a model. Options: local model files shipped or downloaded on demand (size, licence, new runtime dependency), or a cloud API (breaks offline). Not started.
- **New packages**: SQLite (library), NAudio (audio), an ONNX runtime (local AI). Each needs explicit approval and pinned versions.
- **Enterprise tier** (SSO, team analytics, custom branding, unlimited storage): out of scope for a single-user offline tool unless requested.
