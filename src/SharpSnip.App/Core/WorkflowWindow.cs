using System.Windows;
using System.Windows.Controls;
using SharpSnip.Core;

namespace SharpSnip;

/// <summary>
/// Editor for the named workflows (Settings → Manage workflows…). Works on a
/// deep copy; OK hands the edited list back, Cancel discards it. Steps are
/// plain spec lines, validated and canonicalized by Core WorkflowPlan when
/// stored — lines that don't parse are dropped and the drop is reported.
/// </summary>
internal sealed class WorkflowWindow : Window
{
    private readonly List<WorkflowDef> _items;
    private readonly ListBox _list = new();
    private readonly TextBox _name = new();
    private readonly ComboBox _applyTo = new();
    private readonly TextBox _steps = new() { AcceptsReturn = true, FontFamily = new System.Windows.Media.FontFamily("Consolas"), TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap, Foreground = TesselTheme.Muted };
    private int _selected = -1;

    public List<WorkflowDef> Result => _items;

    public WorkflowWindow(List<WorkflowDef> workflows)
    {
        _items = workflows.Select(Clone).ToList();
        Title = "Workflows";
        Width = 680;
        Height = 440;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;

        foreach (var a in WorkflowPlan.ApplyTos) _applyTo.Items.Add(a);

        var help = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            Foreground = TesselTheme.Muted,
            Text = "One step per line. Kinds: copy: D:\\Folder · run: C:\\tool.exe|\"%path%\" · format: jpg · effect: Grayscale · editor:\nPlaceholders in copy/run: %path% %dir% %name% %ext%. Steps run in order; format/effect make a sibling file and continue with it.",
        };

        var newBtn = new Button { Content = "New", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 8, 0) };
        var delBtn = new Button { Content = "Delete", Padding = new Thickness(10, 3, 10, 3), Margin = new Thickness(0, 0, 8, 0) };
        var okBtn = new Button { Content = "OK", Padding = new Thickness(14, 3, 14, 3), Margin = new Thickness(0, 0, 8, 0), Background = TesselTheme.Action, Foreground = TesselTheme.Foreground };
        var cancelBtn = new Button { Content = "Cancel", Padding = new Thickness(10, 3, 10, 3) };
        newBtn.Click += (_, _) => { StoreCurrent(); _items.Add(new WorkflowDef { Name = UniqueName("Workflow") }); RefreshList(_items.Count - 1); };
        delBtn.Click += (_, _) => { if (_selected >= 0) { _items.RemoveAt(_selected); RefreshList(Math.Min(_selected, _items.Count - 1)); } };
        okBtn.Click += (_, _) => { StoreCurrent(); DialogResult = true; Close(); };
        cancelBtn.Click += (_, _) => { DialogResult = false; Close(); };
        _list.SelectionChanged += (_, _) => { StoreCurrent(); LoadSelected(); };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        buttons.Children.Add(newBtn); buttons.Children.Add(delBtn); buttons.Children.Add(okBtn); buttons.Children.Add(cancelBtn);

        var right = new Grid { Margin = new Thickness(12, 0, 0, 0) };
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = new GridLength(150) });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var nameRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        nameRow.Children.Add(new TextBlock { Text = "Name", Width = 60, VerticalAlignment = VerticalAlignment.Center });
        nameRow.Children.Add(_name);
        var applyRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        applyRow.Children.Add(new TextBlock { Text = "Applies to", Width = 60, VerticalAlignment = VerticalAlignment.Center });
        applyRow.Children.Add(_applyTo);
        Grid.SetRow(nameRow, 0); Grid.SetRow(applyRow, 1); Grid.SetRow(_steps, 2); Grid.SetRow(help, 3); Grid.SetRow(_status, 4);
        right.Children.Add(nameRow); right.Children.Add(applyRow); right.Children.Add(_steps); right.Children.Add(help); right.Children.Add(_status);

        var root = new Grid { Margin = new Thickness(16) };
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(_list, 0); Grid.SetColumn(_list, 0);
        Grid.SetRow(right, 0); Grid.SetColumn(right, 1);
        Grid.SetRow(buttons, 1); Grid.SetColumnSpan(buttons, 2);
        root.Children.Add(_list); root.Children.Add(right); root.Children.Add(buttons);
        Content = root;
        TesselTheme.Apply(this);

        RefreshList(_items.Count > 0 ? 0 : -1);
    }

    private static WorkflowDef Clone(WorkflowDef w) => new() { Name = w.Name, ApplyTo = w.ApplyTo, Steps = new List<string>(w.Steps) };

    private string UniqueName(string baseName)
    {
        string name = baseName;
        int n = 2;
        while (_items.Any(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase))) name = $"{baseName} {n++}";
        return name;
    }

    private void RefreshList(int select)
    {
        _selected = -2; // guard: list rebuild must not trigger StoreCurrent
        _list.Items.Clear();
        foreach (var w in _items) _list.Items.Add(w.Name);
        _selected = -1;
        if (select >= 0 && select < _items.Count) _list.SelectedIndex = select;
        else LoadSelected();
    }

    private void LoadSelected()
    {
        _selected = _list.SelectedIndex;
        if (_selected < 0 || _selected >= _items.Count)
        {
            _name.Text = ""; _steps.Text = ""; _applyTo.SelectedIndex = 2;
            _name.IsEnabled = _steps.IsEnabled = _applyTo.IsEnabled = false;
            return;
        }
        var w = _items[_selected];
        _name.IsEnabled = _steps.IsEnabled = _applyTo.IsEnabled = true;
        _name.Text = w.Name;
        _applyTo.SelectedItem = WorkflowPlan.SanitizeApplyTo(w.ApplyTo);
        _steps.Text = string.Join(Environment.NewLine, w.Steps);
    }

    /// <summary>Writes the field values back into the selected definition, canonicalizing the steps.</summary>
    private void StoreCurrent()
    {
        if (_selected < 0 || _selected >= _items.Count) return;
        var w = _items[_selected];
        string name = _name.Text.Trim();
        if (name.Length > 0 && !_items.Where((x, i) => i != _selected).Any(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase)))
            w.Name = name;
        w.ApplyTo = _applyTo.SelectedItem as string ?? "Both";
        var raw = _steps.Text.Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        var kept = WorkflowPlan.SanitizeSteps(raw);
        w.Steps = kept;
        if (kept.Count != raw.Count) _status.Text = $"{raw.Count - kept.Count} step line(s) didn't parse and were dropped.";
        _list.Items[_selected] = w.Name;
    }
}
