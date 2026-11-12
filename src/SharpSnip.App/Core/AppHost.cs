using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using SharpSnip.Core;
using WinForms = System.Windows.Forms;

namespace SharpSnip;

/// <summary>Owns tray icon, hotkeys, preferences and the router for the process lifetime.</summary>
internal sealed class AppHost : IDisposable
{
    private readonly HotkeyManager _hotkeys = new();
    private readonly UiaWorker _uia = new();
    private readonly WinForms.NotifyIcon _tray;
    private readonly ModeRouter _router;
    private Preferences _prefs;
    private string? _lastPath;
    private SettingsWindow? _settings;

    public AppHost()
    {
        _prefs = PrefsStore.Load(AppPaths.PrefsPath, out string? warn);
        _router = new ModeRouter(_prefs, _uia, Notify, SavePrefs);

        using var iconStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("icon.ico");
        _tray = new WinForms.NotifyIcon
        {
            Icon = iconStream is null ? System.Drawing.SystemIcons.Application : new System.Drawing.Icon(iconStream),
            Text = "SharpSnip", Visible = true,
            ContextMenuStrip = BuildMenu(),
        };
        _tray.MouseClick += (_, e) => { if (e.Button == WinForms.MouseButtons.Left) _ = _router.RunSessionAsync(); };
        _tray.BalloonTipClicked += (_, _) => OpenLastFolder();

        _hotkeys.HotkeyPressed += id =>
        {
            if (id == HotkeyManager.IdMenu) _ = _router.RunSessionAsync();
            else if (id == HotkeyManager.IdFullScreen) _ = _router.RunInstantFullScreenAsync();
        };
        RegisterHotkeys(_prefs, notifyOnFailure: true);
        if (warn is not null) Notify("SharpSnip", warn, null);
        MonitorEnumerator.Refresh();
        Log.Info("SharpSnip started");
    }

    private WinForms.ContextMenuStrip BuildMenu()
    {
        var m = new WinForms.ContextMenuStrip();
        WinForms.ToolStripMenuItem Item(string text, string tip, Action a)
        {
            var i = new WinForms.ToolStripMenuItem(text) { ToolTipText = tip };
            i.Click += (_, _) => a();
            m.Items.Add(i);
            return i;
        }
        Item("Capture…", "Open the mode menu (same as the menu hotkey).", () => _ = _router.RunSessionAsync());
        Item("Full screen now", "Capture the configured monitor straight to the clipboard.", () => _ = _router.RunInstantFullScreenAsync());
        m.Items.Add(new WinForms.ToolStripSeparator());
        Item("Open output folder", "Open the folder where captures are saved.", () => OpenFolder(Exporter.OutputFolder(_prefs)));
        Item("Open log folder", "Logs stay on this computer.", () => OpenFolder(AppPaths.LogDir));
        Item("Settings…", "Hotkeys, output folder, delay and more.", ShowSettings);
        m.Items.Add(new WinForms.ToolStripSeparator());
        Item("Exit", "Quit SharpSnip and release the hotkeys.", () => Application.Current.Shutdown());
        return m;
    }

    private void RegisterHotkeys(Preferences p, bool notifyOnFailure)
    {
        bool ok1 = HotkeySpec.TryParse(p.HotkeyMenu, out var a) && _hotkeys.Register(HotkeyManager.IdMenu, a);
        bool ok2 = HotkeySpec.TryParse(p.HotkeyFullScreen, out var b) && _hotkeys.Register(HotkeyManager.IdFullScreen, b);
        if (notifyOnFailure && (!ok1 || !ok2))
            Notify("SharpSnip", $"Hotkey already in use: {(ok1 ? "" : p.HotkeyMenu)} {(ok2 ? "" : p.HotkeyFullScreen)}. Change it in Settings, or start from the tray icon.".Replace("  ", " "), null);
    }

    private void ShowSettings()
    {
        if (_settings is not null) { _settings.Activate(); return; }
        _settings = new SettingsWindow(_prefs.Sanitize(), Apply);
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show();
    }

    /// <summary>Returns an error message, or null on success.</summary>
    private string? Apply(Preferences p)
    {
        // Register new hotkeys first; keep the old ones if that fails.
        var oldMenu = _prefs.HotkeyMenu; var oldFull = _prefs.HotkeyFullScreen;
        HotkeySpec.TryParse(p.HotkeyMenu, out var a); HotkeySpec.TryParse(p.HotkeyFullScreen, out var b);
        if (!_hotkeys.Register(HotkeyManager.IdMenu, a))
        {
            if (HotkeySpec.TryParse(oldMenu, out var o)) _hotkeys.Register(HotkeyManager.IdMenu, o);
            return $"{p.HotkeyMenu} is already used by another program.";
        }
        if (!_hotkeys.Register(HotkeyManager.IdFullScreen, b))
        {
            if (HotkeySpec.TryParse(oldMenu, out var o1)) _hotkeys.Register(HotkeyManager.IdMenu, o1);
            if (HotkeySpec.TryParse(oldFull, out var o2)) _hotkeys.Register(HotkeyManager.IdFullScreen, o2);
            return $"{p.HotkeyFullScreen} is already used by another program.";
        }
        _prefs = p.Sanitize();
        _router.Prefs = _prefs;
        SavePrefs();
        return null;
    }

    private void SavePrefs()
    {
        try { PrefsStore.Save(AppPaths.PrefsPath, _prefs); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log.Error("Could not save settings", ex); }
    }

    private void Notify(string title, string text, string? path)
    {
        _lastPath = path ?? _lastPath;
        Log.Info($"{title}: {text}");
        if (!_prefs.ShowToasts) return;
        _tray.ShowBalloonTip(3000, title, text, WinForms.ToolTipIcon.Info);
    }

    private void OpenLastFolder()
    {
        if (_lastPath is null) return;
        if (File.Exists(_lastPath)) Process.Start("explorer.exe", $"/select,\"{_lastPath}\"");
        else OpenFolder(Exporter.OutputFolder(_prefs));
    }

    private static void OpenFolder(string dir)
    {
        try { Directory.CreateDirectory(dir); Process.Start("explorer.exe", $"\"{dir}\""); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Log.Warn("Could not open " + dir + ": " + ex.Message); }
    }

    public void Dispose()
    {
        _tray.Visible = false;
        _tray.Dispose();
        _hotkeys.Dispose();
        _uia.Dispose();
        Log.Info("SharpSnip stopped");
    }
}
