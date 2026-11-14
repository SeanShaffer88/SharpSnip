# Roadmap

Goal: eventually cover the full feature list Sean supplied (screen capture, recording, annotation, AI/smart tools, library, sharing, utilities).

**Status through v0.25.8.** ✅ Implemented · 🔜 Planned · ❓ Needs a design or scope decision · ⛔ Outside the offline scope unless approved.

Implementation status comes from the source and release history. The Windows field test covered the v0.16.0 x64 installer and portable build, live UI and Print Screen behavior, SharpSnip settings carryover, and uninstall cleanup. See [Windows field verification](../CHANGELOG.md#windows-field-verification), [AUDIT.md](../AUDIT.md), and [TEST-PLAN.md](TEST-PLAN.md).

The phase order is a proposal. Each phase ends in a tagged release with README, changelog and tests updated.

## Phase 0 — Capture foundation

Blueprint v2 shipped in v0.1 with element, region, window and full-screen capture, multi-monitor support, delay, the editor, clipboard and export.

## Phase 1 — Capture breadth

| Feature | Status |
|---|---|
| Full-screen, all monitors, or a numbered monitor | ✅ Capture mode bar and CLI |
| Window under the cursor | ✅ Window mode, with whole-window or client-area capture. The standalone active-window hotkey was retired in v0.10. |
| Rectangular region | ✅ |
| Fixed-size / exact-dimension region | ✅ v0.3 |
| Repeat last capture / preset region | ✅ Tray menu |
| Delayed capture | ✅ |
| Interval capture (every N seconds) | ✅ v0.3 |
| Object and window-control capture | ✅ Element mode |
| Menu capture | ✅ Capture delay. A dedicated menu-capture shortcut is pending. |
| Print Screen replaces Snipping Tool | ✅ Sole global hotkey since v0.10. Exit Snipit or use `--restore-printscreen` before uninstalling. |
| Dark settings and capture mode bar | ✅ TesselGUI revision 3, with a movable capture bar since v0.14 |
| Multiple-area capture | ✅ v0.19. In Region mode, press A to save up to 16 rectangles, then Enter to combine the selected areas into one image. |
| Hand Print Screen back automatically on uninstall | ❓ Requires an uninstall hook in SeanPak. Writing the setting at install time would change the user's Print Screen behavior. |
| Freehand and polygon capture | ✅ Freehand capture with transparency outside the outline. 🔜 Click-by-click polygon capture. |
| Scrolling capture | ✅ v0.5.0. Uses UI Automation ScrollPattern where available, with a wheel-and-stitch fallback. |
| Capture backend | ✅ Windows Graphics Capture for stills, with a GDI fallback |
| Webcam still | ❓ WinRT MediaCapture design |
| Printer capture | ❓ Virtual printer driver |

## Phase 2 — Annotation and editing

| Feature | Status |
|---|---|
| Arrows, shapes, text, pen, highlighter, pixelation, crop | ✅ |
| Numbered step markers, callouts, stamps | ✅ Step markers in v0.7, callouts and stamps in v0.25 |
| Lines and connectors | 🔜 |
| Spotlight and magnify annotations | ✅ Spotlight in v0.22, magnify in v0.23 |
| Manual redaction | ✅ Selective pixelation. 🔜 Dedicated manual redaction workflow. |
| Rotate, flip, grayscale, invert | ✅ v0.5.0 |
| Resize, watermark, brightness and contrast | ✅ v0.7. The brightness and contrast controls moved into Adjust in v0.20. |
| Sharpen, saturation, hue, and colour balance | ✅ Sharpen and saturation in v0.21, hue and colour balance in v0.24 |
| Frame, Shadow, Torn, and Wave effects | ✅ Frame and Shadow in v0.12, Torn and Wave in v0.15, configurable Effects dialog in v0.17 |
| Cut out a strip | ✅ v0.18. Remove a strip and join the remaining sides. |
| Fill, eraser / retouch | 🔜 |
| Eyedropper with HEX, RGB, HTML, C++, and Delphi output | ✅ v0.12. A palette generation helper exists in Core. 🔜 Palette UI. |
| Dark editor and portable mode | ✅ TesselGUI revision 3 editor since v0.20. A `data` folder beside the executable enables portable mode. |
| Quick styles, selectable themes, and toolbar customisation | 🔜 |
| Shortcuts | ✅ Single-key editor shortcuts can be disabled since v0.25.5. Configurable global capture hotkeys were retired in v0.10. |

## Phase 3 — Library, export, and utilities

| Feature | Status |
|---|---|
| Recent captures | ✅ Tray list in v0.7. 🔜 Searchable library, tags, filters, sorting controls and history gallery. |
| Custom file-naming patterns, automatic folder organisation | 🔜 |
| Export PNG, JPG, BMP, TIFF, GIF, and PDF | ✅ Includes a lossless single-image PDF writer |
| APNG and FFmpeg export | ✅ v0.8. FFmpeg converts recordings and GIFs to supported video or audio formats. |
| Export PSD / TGA | ❓ Custom writers, low priority |
| External editor | ✅ v0.7 |
| Local workflows | ✅ v0.8 for after-capture and after-recording tasks. Recording conversion arrived in v0.9. Existing program-launching workflows may require approval in Settings from v0.25.1. |
| Proprietary editable format (layers kept) | 🔜 |
| Insert into Office documents | ❓ COM automation per app |
| Capture magnifier and pixel readout, colour picker, drawing on screen | ✅ Available in Region mode or from the tray, depending on the tool |
| On-screen ruler and dedicated whiteboard | 🔜 |
| Hideable tray icon | ✅ v0.6.0. Running Snipit again restores it. |
| Scheduling and context-menu quick actions | ✅ Interval still captures, one-shot recorder scheduling, and tray quick actions. 🔜 Schedule a still capture for a specific time. |

## Phase 4 — Text recognition

| Feature | Status |
|---|---|
| OCR and copy text from an image | ✅ v0.5.0. Uses `Windows.Media.Ocr` and the installed Windows OCR languages. |
| OCR find and replace | ✅ v0.7 editor tool |
| Text-inside-image search | 🔜 Builds on OCR and a library index |
| Smart Redact (auto-find emails, numbers, keys) | ✅ v0.5.0. Detection and box mapping run offline. |

## Phase 5 — Recording

| Feature | Status |
|---|---|
| Monitor and region recording to MP4 or animated GIF | ✅ Auto-save, pause and resume, frame-rate and quality settings, duration and file-size limits, and original size or 1080p/720p/480p caps. 🔜 Dedicated window recording. |
| System audio and microphone | ✅ WASAPI system loopback and microphone capture |
| Webcam picture-in-picture | ✅ Optional webcam overlay. 🔜 Background blur and camera switching. |
| Drawing on screen while recording | ✅ Live annotations. 🔜 Cursor highlighting and click animation. |
| Trim or combine clips, convert an image sequence to video, extract frames | ❓ |
| GIF from screen activity or a recording | ✅ Direct GIF recording, animated GIF from interval capture, and FFmpeg GIF export |
| Audio noise removal | ❓ DSP design or library |

## Needs a decision before any work (⛔ / ❓)

- **Cloud and sharing** (share links, Dropbox/Drive/OneDrive/Box, Slack/Teams, email, social, FTP, YouTube, short links, cloud sync, enterprise SSO/analytics) need a network decision. Snipit is offline by charter. Proposed integrations would be optional and off by default. A Windows share sheet or `mailto:` flow can hand off to another app.
- **AI features** (AI Step Capture, AI background removal, AI captions/chapters, AI redaction risk scoring, Simplify, Smart Move) need a model and a decision about local model files, licensing, runtime dependencies and network use.
- **New packages**, such as SQLite for the library or an ONNX runtime for local AI, need a dependency decision and pinned versions.
- **Enterprise tier** (SSO, team analytics, custom branding, unlimited storage) is outside the current single-user scope.
