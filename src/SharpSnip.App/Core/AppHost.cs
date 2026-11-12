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
    private WinForms.ToolStripMenuItem? _intervalItem;
    private CancellationTokenSource? _interval;

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
            else if (id >= HotkeyManager.IdMonitorBase) _ = _router.RunMonitorAsync(id - HotkeyManager.IdMonitorBase + 1);
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
        _intervalItem = Item("Start interval capture", "Capture the configured monitor every N seconds into a new folder. Click again to stop.", () => _ = ToggleIntervalAsync());
        Item("Repeat last capture", "Capture the same screen rectangle as the previous capture.", () => _ = _router.RunRepeatLastAsync());
        m.Items.Add(new WinForms.ToolStripSeparator());
        Item("Open output folder", "Open the folder where captures are saved.", () => OpenFolder(Exporter.OutputFolder(_prefs)));
        Item("Open log folder", "Logs stay on this computer.", () => OpenFolder(AppPaths.LogDir));
        Item("Settings…", "Hotkeys, output folder, delay and more.", ShowSettings);
        m.Items.Add(new WinForms.ToolStripSeparator());
        Item("Exit", "Quit SharpSnip and release the hotkeys.", () => Application.Current.Shutdown());
        return m;
    }

    /// <summary>Every hotkey the app wants: the four fixed ones plus one per monitor entry.</summary>
    private static List<(int Id, string Text)> Slots(Preferences p)
    {
        var list = new List<(int, string)>
        {
            (HotkeyManager.IdMenu, p.HotkeyMenu), (HotkeyManager.IdFullScreen, p.HotkeyFullScreen),
            (HotkeyManager.IdActiveWindow, p.HotkeyActiveWindow), (HotkeyManager.IdRepeatLast, p.HotkeyRepeatLast),
        };
        int i = 0;
        foreach (var part in p.HotkeyMonitors.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            list.Add((HotkeyManager.IdMonitorBase + i++, part));
        return list;
    }

    private void UnregisterAll()
    {
        foreach (int id in new[] { HotkeyManager.IdMenu, HotkeyManager.IdFullScreen, HotkeyManager.IdActiveWindow, HotkeyManager.IdRepeatLast })
            _hotkeys.Unregister(id);
        for (int i = 0; i < HotkeyList.Max; i++) _hotkeys.Unregister(HotkeyManager.IdMonitorBase + i);
    }

    /// <summary>Registers everything; returns the texts that could not be registered.</summary>
    private List<string> RegisterAll(Preferences p)
    {
        UnregisterAll();
        var failed = new List<string>();
        foreach (var (id, text) in Slots(p))
            if (!(HotkeySpec.TryParse(text, out var spec) && _hotkeys.Register(id, spec))) failed.Add(text);
        return failed;
    }

    private void RegisterHotkeys(Preferences p, bool notifyOnFailure)
    {
        var failed = RegisterAll(p);
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
        var failed = RegisterAll(p);
        if (failed.Count > 0)
        {
            RegisterAll(old);
            return $"{string.Join(", ", failed)} is already used by another program.";
        }
        _prefs = p.Sanitize();
        _router.Prefs = _prefs;
        SavePrefs();
        return null;
    }

    // ---- interval capture ----------------------------------------------------------------------

    private async Task ToggleIntervalAsync()
    {
        if (_interval is not null) { _interval.Cancel(); return; }
        if (_router.IsBusy) { Notify("SharpSnip", "Finish the current capture first.", null); return; }

        var prefs = _prefs;
        int seconds = IntervalPlan.SanitizeSeconds(prefs.IntervalSeconds);
        var target = IntervalCapture.ResolveStartTarget(FullScreenTarget.Parse(prefs.FullScreenTarget));
        string folder = Path.Combine(Exporter.OutputFolder(prefs), IntervalPlan.FolderName(DateTime.Now));
        _interval = new CancellationTokenSource();
        var ct = _interval.Token;
        if (_intervalItem is not null) _intervalItem.Text = "Stop interval capture";
        Notify("Interval capture started", $"Every {seconds} s into {folder}. Stop it from the tray menu.", folder);

        int shots = 0, failures = 0;
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
            do
            {
                if (!_router.IsBusy)   // never fight an interactive capture; that tick is skipped
                {
                    try { IntervalCapture.CaptureOnce(prefs, target, folder, shots + 1); shots++; failures = 0; }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        Log.Error("Interval frame failed", ex);
                        if (++failures >= 3) { Notify("Interval capture stopped", "Three frames in a row failed. See the log.", null); break; }
                    }
                }
                if (shots >= IntervalPlan.MaxShots) { Notify("Interval capture stopped", $"Reached {IntervalPlan.MaxShots} frames.", folder); break; }
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) { /* user stopped it */ }
        finally
        {
            _interval.Dispose();
            _interval = null;
            if (_intervalItem is not null) _intervalItem.Text = "Start interval capture";
            Notify("Interval capture finished", $"{shots} frame(s) in {folder}", folder);
        }
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
        _interval?.Cancel();
        _tray.Visible = false;
        _tray.Dispose();
        _hotkeys.Dispose();
        _uia.Dispose();
        Log.Info("SharpSnip stopped");
    }
}
