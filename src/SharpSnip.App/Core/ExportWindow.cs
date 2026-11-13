using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SharpSnip.Core;

namespace SharpSnip;

/// <summary>
/// FFmpeg export dialog: pick a recording or GIF, pick a preset (Core
/// FfmpegPlan — the LGPL-safe matrix), and convert. A custom-arguments mode
/// covers anything a user-supplied FFmpeg build can do. Runs headless and
/// reports FFmpeg's own log tail on failure.
/// </summary>
internal sealed class ExportWindow : Window
{
    private readonly Preferences _prefs;
    private readonly Action<string, string, string?> _notify;
    private readonly TextBox _file = new();
    private readonly ComboBox _preset = new();
    private readonly TextBox _out = new();
    private readonly CheckBox _customMode = new() { Content = "Custom arguments" };
    private readonly TextBox _customArgs = new();
    private readonly Button _run = new() { Content = "Export", Padding = new Thickness(14, 3, 14, 3), Background = TesselTheme.Action, Foreground = TesselTheme.Foreground };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Foreground = TesselTheme.Muted };
    private CancellationTokenSource? _cts;

    public ExportWindow(string? initialFile, Preferences prefs, Action<string, string, string?> notify)
    {
        _prefs = prefs;
        _notify = notify;
        Title = "Export with FFmpeg";
        Width = 560;
        Height = 400;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ResizeMode = ResizeMode.NoResize;

        _file.Text = initialFile ?? "";
        _file.ToolTip = "The recording or GIF to convert.";
        foreach (var p in FfmpegPlan.Presets) _preset.Items.Add(p.Label);
        _preset.SelectedIndex = 0;
        _preset.ToolTip = "What to make. The bundled FFmpeg is an LGPL build: no libx264, so H.264 uses Windows' own encoder.";
        _out.ToolTip = "Where the converted file goes. An existing name gets a -1, -2 suffix instead of overwriting.";
        _customArgs.ToolTip = "Full argument template with %in% and %out% placeholders, e.g. -i %in% -c:v libx264 -y %out% (for a user-supplied FFmpeg that has it).";
        _customArgs.IsEnabled = false;
        _customMode.Checked += (_, _) => { _customArgs.IsEnabled = true; _preset.IsEnabled = false; };
        _customMode.Unchecked += (_, _) => { _customArgs.IsEnabled = false; _preset.IsEnabled = true; };
        _preset.SelectionChanged += (_, _) => RefreshOut();
        _file.LostFocus += (_, _) => RefreshOut();

        var browse = new Button { Content = "Browse…", Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(8, 0, 0, 0) };
        browse.Click += (_, _) =>
        {
            var dlg = new OpenFileDialog { Filter = "Media files|*.mp4;*.gif;*.mkv;*.webm;*.mp3;*.m4a|All files|*.*" };
            if (dlg.ShowDialog(this) == true) { _file.Text = dlg.FileName; RefreshOut(); }
        };
        var fileRow = new DockPanel();
        DockPanel.SetDock(browse, Dock.Right);
        fileRow.Children.Add(browse);
        fileRow.Children.Add(_file);

        _run.Click += async (_, _) => await RunAsync();
        var close = new Button { Content = "Close", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(8, 0, 0, 0) };
        close.Click += (_, _) => Close();
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(_run);
        buttons.Children.Add(close);

        var grid = new Grid { Margin = new Thickness(16) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        int row = 0;
        void Add(string label, UIElement c)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, Margin = new Thickness(0, 6, 12, 6), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(l, row); Grid.SetRow(c, row); Grid.SetColumn(c, 1);
            grid.Children.Add(l); grid.Children.Add(c); row++;
        }
        Add("File", fileRow);
        Add("Preset", _preset);
        Add("", _customMode);
        Add("Arguments", _customArgs);
        Add("Output", _out);
        Add("", _status);
        Add("", buttons);
        Content = grid;
        TesselTheme.Apply(this);
        Closed += (_, _) => _cts?.Cancel();
        RefreshOut();
        _status.Text = FfmpegLocator.Find(_prefs) is null
            ? "FFmpeg not found: the bundled copy is missing and no override path is set in Settings."
            : "Ready.";
    }

    private void RefreshOut()
    {
        if (_preset.SelectedIndex < 0 || string.IsNullOrWhiteSpace(_file.Text)) return;
        var preset = FfmpegPlan.Presets[_preset.SelectedIndex];
        _out.Text = FfmpegPlan.DefaultOutputPath(preset, _file.Text.Trim());
    }

    private async Task RunAsync()
    {
        string input = _file.Text.Trim();
        if (!File.Exists(input)) { _status.Text = "Pick an existing file first."; return; }
        string? ffmpeg = FfmpegLocator.Find(_prefs);
        if (ffmpeg is null) { _status.Text = "FFmpeg not found: the bundled copy is missing and no override path is set in Settings."; return; }
        string output = OutputNaming.UniquePath(_out.Text.Trim(), File.Exists);
        string[] args;
        if (_customMode.IsChecked == true)
        {
            args = FfmpegPlan.ParseCustomArgs(_customArgs.Text, input, output);
            if (args.Length == 0 || !args.Any(a => a == output)) { _status.Text = "The custom arguments must produce the output file — include %out%."; return; }
        }
        else
        {
            var preset = FfmpegPlan.Presets[Math.Max(0, _preset.SelectedIndex)];
            if (!FfmpegPlan.TryBuildArgs(preset.Name, input, output, out args)) { _status.Text = "Unknown preset."; return; }
        }
        _run.IsEnabled = false;
        _status.Text = "Exporting…";
        _cts = new CancellationTokenSource();
        try
        {
            var (exit, tail) = await FfmpegExport.RunAsync(ffmpeg, args, _cts.Token);
            if (exit == 0 && File.Exists(output))
            {
                _status.Text = "Done: " + output;
                _notify("Export finished", Path.GetFileName(output), output);
            }
            else _status.Text = "FFmpeg failed (exit " + exit + "). Last log lines:\n" + tail;
        }
        catch (OperationCanceledException) { _status.Text = "Export cancelled."; }
        catch (Exception ex) { _status.Text = "Could not run FFmpeg: " + ex.Message; }
        finally { _run.IsEnabled = true; _cts = null; }
    }
}
