using System.Windows;
using System.Windows.Controls;
using Snipit.Core;

namespace Snipit;

/// <summary>Small modal dialogs for the editor's Resize / Watermark / Replace-text actions.</summary>
internal static class EditorDialogs
{
    private static Window Base(string title, UIElement content)
    {
        var w = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = content,
        };
        return w;
    }

    private static (Grid Grid, Action<string, UIElement> Add) Grid2()
    {
        var grid = new Grid { Margin = new Thickness(12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        int row = 0;
        void Add(string label, UIElement c)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, Margin = new Thickness(0, 5, 10, 5), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(l, row); Grid.SetRow(c, row); Grid.SetColumn(c, 1);
            grid.Children.Add(l); grid.Children.Add(c); row++;
        }
        return (grid, Add);
    }

    private static StackPanel Buttons(Window w, Action onOk)
    {
        var ok = new Button { Content = "OK", IsDefault = true, Width = 76, Margin = new Thickness(4) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Width = 76, Margin = new Thickness(4) };
        ok.Click += (_, _) => { onOk(); };
        var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        panel.Children.Add(ok); panel.Children.Add(cancel);
        return panel;
    }

    /// <summary>Returns the chosen size, or null when cancelled/invalid.</summary>
    public static (int W, int H)? Resize(Window owner, int origW, int origH)
    {
        var (grid, add) = Grid2();
        var wBox = new TextBox { Text = origW.ToString(), ToolTip = $"Width in pixels ({ResizePlan.MinDim} to {ResizePlan.MaxDim})." };
        var hBox = new TextBox { Text = origH.ToString(), ToolTip = "Height in pixels." };
        var keep = new CheckBox { Content = "Keep aspect ratio", IsChecked = true };
        bool syncing = false;
        wBox.TextChanged += (_, _) =>
        {
            if (syncing || keep.IsChecked != true || !int.TryParse(wBox.Text, out int nw)) return;
            syncing = true; hBox.Text = ResizePlan.FromWidth(origW, origH, nw).H.ToString(); syncing = false;
        };
        hBox.TextChanged += (_, _) =>
        {
            if (syncing || keep.IsChecked != true || !int.TryParse(hBox.Text, out int nh)) return;
            syncing = true; wBox.Text = ResizePlan.FromHeight(origW, origH, nh).W.ToString(); syncing = false;
        };
        add("Width", wBox); add("Height", hBox); add("", keep);
        (int, int)? result = null;
        var win = Base("Resize image", new StackPanel());
        var stack = (StackPanel)win.Content;
        stack.Children.Add(grid);
        stack.Children.Add(Buttons(win, () =>
        {
            if (int.TryParse(wBox.Text, out int nw) && int.TryParse(hBox.Text, out int nh))
            {
                result = (ResizePlan.ClampDim(nw), ResizePlan.ClampDim(nh));
                win.DialogResult = true;
            }
        }));
        win.Owner = owner;
        return win.ShowDialog() == true ? result : null;
    }

    /// <summary>Returns (text, tiled, opacity) or null when cancelled.</summary>
    public static (string Text, bool Tiled, double Opacity)? Watermark(Window owner)
    {
        var (grid, add) = Grid2();
        var text = new TextBox { Text = "CONFIDENTIAL", ToolTip = "The watermark text." };
        var style = new ComboBox { ItemsSource = new[] { "Corner", "Tiled" }, SelectedIndex = 0, ToolTip = "One mark in the bottom-right corner, or a repeating diagonal pattern." };
        var opacity = new Slider { Minimum = 10, Maximum = 90, Value = 40, ToolTip = "How strongly the mark shows." };
        add("Text", text); add("Style", style); add("Opacity %", opacity);
        (string, bool, double)? result = null;
        var win = Base("Watermark", new StackPanel());
        var stack = (StackPanel)win.Content;
        stack.Children.Add(grid);
        stack.Children.Add(Buttons(win, () =>
        {
            if (!string.IsNullOrWhiteSpace(text.Text))
            {
                result = (text.Text.Trim(), style.SelectedIndex == 1, opacity.Value / 100.0);
                win.DialogResult = true;
            }
        }));
        win.Owner = owner;
        return win.ShowDialog() == true ? result : null;
    }

    /// <summary>Returns (find, replace) or null when cancelled.</summary>
    public static (string Find, string Replace)? ReplaceText(Window owner)
    {
        var (grid, add) = Grid2();
        var find = new TextBox { ToolTip = "Text to find in the image (read with the built-in OCR)." };
        var repl = new TextBox { ToolTip = "Text drawn in its place. The original is covered with the surrounding colour first." };
        add("Find", find); add("Replace with", repl);
        (string, string)? result = null;
        var win = Base("Find & replace text", new StackPanel());
        var stack = (StackPanel)win.Content;
        stack.Children.Add(grid);
        stack.Children.Add(Buttons(win, () =>
        {
            if (!string.IsNullOrWhiteSpace(find.Text))
            {
                result = (find.Text.Trim(), repl.Text);
                win.DialogResult = true;
            }
        }));
        win.Owner = owner;
        return win.ShowDialog() == true ? result : null;
    }
}
