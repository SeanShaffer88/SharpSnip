# Credits

- **Sean Shaffer** (SeanShaffer88): author, product owner, and author of the workflow blueprint this build follows (see [docs/BLUEPRINT.md](docs/BLUEPRINT.md)).
- Built with Claude (Anthropic).

## Platform and references

No third-party packages are bundled. Snipit uses only components that ship with .NET 10 and Windows:

- .NET 10, WPF and Windows Forms (tray icon only), MIT, Microsoft.
- UI Automation (`System.Windows.Automation`), Win32 (`user32`, `gdi32`, `dwmapi`, `shcore`).
- Documentation consulted by the blueprint: Microsoft Learn (UI Automation, RegisterHotKey, SetWindowDisplayAffinity, DWM extended frame bounds, per-monitor DPI).

Icon and logo were drawn for this project (generated with Pillow; not a bundled dependency).
