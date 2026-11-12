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
            else if (id == HotkeyManager.IdActiveWindow) _ = _router.RunActiveWindowAsync();
            else if (id == HotkeyManager.IdRepeatLast) _ = _router.RunRepeatLastAsync();
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
        Item("Repeat last capture", "Capture the same screen rectangle as the previous capture.", () => _ = _router.RunRepeatLastAsync());
        m.Items.Add(new WinForms.ToolStripSeparator());
        Item("Open output folder", "Open the folder where captures are saved.", () => OpenFolder(Exporter.OutputFolder(_prefs)));
        Item("Open log folder", "Logs stay on this computer.", () => OpenFolder(AppPaths.LogDir));
        Item("Settings…", "Hotkeys, output folder, delay and more.", ShowSettings);
        m.Items.Add(new WinForms.ToolStripSeparator());
        Item("Exit", "Quit SharpSnip and release the hotkeys.", () => Application.Current.Shutdown());
        return m;
    }

    private static readonly (int Id, Func<Preferences, string> Get)[] HotkeySlots =
    {
        (HotkeyManager.IdMenu, p => p.HotkeyMenu),
        (HotkeyManager.IdFullScreen, p => p.HotkeyFullScreen),
        (HotkeyManager.IdActiveWindow, p => p.HotkeyActiveWindow),
        (HotkeyManager.IdRepeatLast, p => p.HotkeyRepeatLast),
    };

    private void RegisterHotkeys(Preferences p, bool notifyOnFailure)
    {
        var failed = new List<string>();
        foreach (var (id, get) in HotkeySlots)
            if (!(HotkeySpec.TryParse(get(p), out var spec) && _hotkeys.Register(id, spec))) failed.Add(get(p));
        if (notifyOnFailure && failed.Count > 0)
            Notify("SharpSnip", $"Hotkey already in use: {string.Join(", ", failed)}. Change it in Settings, or start from the tray icon.", null);
    }

    private void ShowSettings()
    {
        if (_settings is not null) { _settings.Activate(); return; }
        _settings = new SettingsWindow(_prefs.Sanitize(), Apply);
        _settings.Closed += (_, _) => _settings = null;
        _settings.Show();
    }

    /// <summary>Returns an error message, or null on success. Old hotkeys are restored if any new one is taken.</summary>
    private string? Apply(Preferences p)
    {
        var old = _prefs;
        foreach (var (id, get) in HotkeySlots)
        {
            HotkeySpec.TryParse(get(p), out var spec);
            if (_hotkeys.Register(id, spec)) continue;
            foreach (var (rid, rget) in HotkeySlots)
                if (HotkeySpec.TryParse(rget(old), out var o)) _hotkeys.Register(rid, o);
            return $"{get(p)} is already used by another program.";
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
