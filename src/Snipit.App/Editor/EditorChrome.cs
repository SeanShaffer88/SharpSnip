using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using Path = System.Windows.Shapes.Path;

namespace Snipit;

/// <summary>
/// The editor's TesselGUI revision 3 (Carmine) look, matching the capture mode bar: icon-over-label tool cells
/// with key hints and a Carmine underline for the active tool, a single Carmine Save button, swatch colours,
/// a thin slider, and dark menus. Same 24-unit line-and-dot icon style as the mode bar. All colours come
/// from <see cref="Palette"/>, which has a black-and-white High Contrast variant.
/// </summary>
internal sealed class EditorChrome
{
    internal sealed record Palette(string Canvas, string Bar, string Surface, string Edge, string Ink, string Muted,
        string Hover, string HoverInk, string Pressed, string Selected, string SelInk, string SelLine,
        string Accent, string AccentHover, string AccentPressed, string ActionInk, string Focus)
    {
        public static readonly Palette Dark = new("#17171A", "#232326", "#1F1F22", "#43434A", "#E6E3E1", "#89868A",
            "#28282D", "#E6E3E1", "#2E2E34", "#2A2A2F", "#E6E3E1", "#960018", "#960018", "#A81226", "#7E0014", "#FFFFFF", "#E8939F");
        public static readonly Palette HighContrast = new("#000000", "#000000", "#000000", "#FFFFFF", "#FFFFFF", "#FFFFFF",
            "#FFFFFF", "#000000", "#C0C0C0", "#FFFFFF", "#000000", "#000000", "#FFFFFF", "#E0E0E0", "#C0C0C0", "#000000", "#FFFFFF");
    }

    /// <summary>Tag value that tells <see cref="TesselTheme"/> to leave a control's own styling alone.</summary>
    public const string Keep = "keep";

    private readonly Palette _p;
    public EditorChrome(bool highContrast) { _p = highContrast ? Palette.HighContrast : Palette.Dark; }

    public Brush CanvasBg => Br(_p.Canvas);
    public Brush Bar => Br(_p.Bar);
    public Brush Surface => Br(_p.Surface);
    public Brush Edge => Br(_p.Edge);
    public Brush Ink => Br(_p.Ink);
    public Brush Muted => Br(_p.Muted);

    private static Brush Br(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }

    // ---- icons ---------------------------------------------------------------------------------

    // 24x24 grid, 1.6 stroke, round caps and joins; Dots are the small Nothing-style accents (decoration only).
    private static readonly Dictionary<string, (string[] Paths, (double X, double Y)[] Dots)> Icons = new()
    {
        ["pen"] = (new[] { "M4.5 19.5l1-4L15.5 5.5a1.4 1.4 0 0 1 2 0l1 1a1.4 1.4 0 0 1 0 2L8.5 18.5z" }, new[] { (20.0, 19.5) }),
        ["highlighter"] = (new[] { "M9 16l7-7 3 3-7 7H9z", "M4.5 20.5H11" }, new[] { (20.0, 20.5) }),
        ["arrow"] = (new[] { "M5 19L19 5M10 5h9v9" }, new[] { (5.0, 5.0) }),
        ["rect"] = (new[] { "M6.5 6h11A2.5 2.5 0 0 1 20 8.5v7a2.5 2.5 0 0 1-2.5 2.5h-11A2.5 2.5 0 0 1 4 15.5v-7A2.5 2.5 0 0 1 6.5 6z" }, new[] { (7.5, 9.5) }),
        ["ellipse"] = (new[] { "M4 12a8 6 0 1 0 16 0a8 6 0 1 0 -16 0" }, new[] { (12.0, 12.0) }),
        ["text"] = (new[] { "M6 7V5.5h12V7M12 5.5V19M9.5 19h5" }, new[] { (19.0, 19.0) }),
        ["step"] = (new[] { "M3.8 12a8.2 8.2 0 1 0 16.4 0a8.2 8.2 0 1 0 -16.4 0", "M10.4 10.2l1.8-1.2V15" }, new[] { (12.0, 18.0) }),
        ["blur"] = (new[] { "M6.5 4h11A2.5 2.5 0 0 1 20 6.5v11a2.5 2.5 0 0 1-2.5 2.5h-11A2.5 2.5 0 0 1 4 17.5v-11A2.5 2.5 0 0 1 6.5 4z" },
            new[] { (9.0, 9.0), (12.0, 9.0), (15.0, 9.0), (9.0, 12.0), (12.0, 12.0), (15.0, 12.0), (9.0, 15.0), (12.0, 15.0), (15.0, 15.0) }),
        ["spotlight"] = (new[] { "M4 9V6a2 2 0 0 1 2-2h3M15 4h3a2 2 0 0 1 2 2v3M20 15v3a2 2 0 0 1-2 2h-3M9 20H6a2 2 0 0 1-2-2v-3" }, new[] { (12.0, 12.0) }),
        ["loupe"] = (new[] { "M4 10.5a6.5 6.5 0 1 0 13 0a6.5 6.5 0 1 0 -13 0", "M15.5 15.5L20 20" }, new[] { (10.5, 10.5) }),
        ["crop"] = (new[] { "M7 3.5v13.5h13.5M3.5 7H17v13.5" }, new[] { (12.0, 12.0) }),
        ["cut"] = (new[] { "M4 8h16M4 16h16" }, new[] { (8.0, 12.0), (12.0, 12.0), (16.0, 12.0) }),
        ["undo"] = (new[] { "M9 6.5L4.5 11 9 15.5M5 11h9a5 5 0 0 1 0 9h-3" }, Array.Empty<(double, double)>()),
        ["redo"] = (new[] { "M15 6.5l4.5 4.5-4.5 4.5M19 11h-9a5 5 0 0 0 0 9h3" }, Array.Empty<(double, double)>()),
        ["copy"] = (new[] { "M11 8.5h6.5A2.5 2.5 0 0 1 20 11v6.5a2.5 2.5 0 0 1-2.5 2.5H11a2.5 2.5 0 0 1-2.5-2.5V11A2.5 2.5 0 0 1 11 8.5z", "M15.5 8.5V6.5A2.5 2.5 0 0 0 13 4H6.5A2.5 2.5 0 0 0 4 6.5V13a2.5 2.5 0 0 0 2.5 2.5h2" }, Array.Empty<(double, double)>()),
        ["save"] = (new[] { "M5 7.5A2.5 2.5 0 0 1 7.5 5H16l3 3v9.5a2.5 2.5 0 0 1-2.5 2.5h-9A2.5 2.5 0 0 1 5 17.5z", "M8.5 5v4h6V5M8.5 20v-5h7v5" }, Array.Empty<(double, double)>()),
        ["chevron"] = (new[] { "M7 10l5 5 5-5" }, Array.Empty<(double, double)>()),
    };

    /// <summary>A line icon whose colour follows the Foreground of the button it sits in.</summary>
    public UIElement Icon(string name, double size)
    {
        var (paths, dots) = Icons[name];
        var c = new Canvas { Width = 24, Height = 24 };
        foreach (var d in paths)
        {
            var p = new Path
            {
                Data = Geometry.Parse(d), StrokeThickness = 1.6, Fill = null,
                StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
            };
            p.SetBinding(Shape.StrokeProperty, FollowForeground());
            c.Children.Add(p);
        }
        foreach (var (x, y) in dots)
        {
            var e = new Ellipse { Width = 1.6, Height = 1.6 };
            e.SetBinding(Shape.FillProperty, FollowForeground());
            Canvas.SetLeft(e, x - .8); Canvas.SetTop(e, y - .8);
            c.Children.Add(e);
        }
        return new Viewbox { Width = size, Height = size, Child = c, IsHitTestVisible = false };
    }

    private static Binding FollowForeground() =>
        new("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor, typeof(Control), 1) };

    // ---- cells ---------------------------------------------------------------------------------

    private UIElement CellContent(string icon, string label, string key)
    {
        var grid = new Grid();
        var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        stack.Children.Add(Icon(icon, 22));
        stack.Children.Add(new TextBlock { Text = label, FontSize = 11, Margin = new Thickness(0, 3, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, Tag = Keep });
        grid.Children.Add(stack);
        if (key.Length > 0)
            grid.Children.Add(new TextBlock
            {
                Text = key, FontFamily = new FontFamily("Consolas"), FontSize = 10, Foreground = Muted, Tag = Keep,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 4, 5, 0), IsHitTestVisible = false,
            });
        return grid;
    }

    private static void Name(DependencyObject d, string name, string tip)
    {
        System.Windows.Automation.AutomationProperties.SetName(d, name);
        if (d is FrameworkElement fe) fe.ToolTip = tip;
    }

    /// <summary>A tool cell: icon over label, key hint top right, Carmine underline when active.</summary>
    public ToggleButton ToolCell(string label, string icon, string key, string tip)
    {
        var b = new ToggleButton
        {
            Width = 56, Height = 52, Margin = new Thickness(1), Content = CellContent(icon, label, key), Template = ToggleTemplate,
            Foreground = Ink, Focusable = true, Tag = Keep,
        };
        Name(b, key.Length > 0 ? $"{label} ({key})" : label, tip + (key.Length > 0 ? $" Key: {key}." : ""));
        return b;
    }

    /// <summary>A plain action cell (Undo, Redo, Copy).</summary>
    public Button ActionCell(string label, string icon, string tip)
    {
        var b = new Button { Width = 56, Height = 52, Margin = new Thickness(1), Content = CellContent(icon, label, ""), Template = ButtonTemplate, Foreground = Ink, Tag = Keep };
        Name(b, label, tip);
        return b;
    }

    /// <summary>The one Carmine action: Save.</summary>
    public Button SaveButton(string tip)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, IsHitTestVisible = false };
        row.Children.Add(Icon("save", 18));
        row.Children.Add(new TextBlock { Text = "Save", FontSize = 12, Margin = new Thickness(7, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Tag = Keep });
        var b = new Button { Height = 52, MinWidth = 84, Margin = new Thickness(6, 1, 1, 1), Padding = new Thickness(14, 0, 14, 0), Content = row, Template = SaveTemplate, Foreground = Br(_p.ActionInk), Tag = Keep };
        Name(b, "Save", tip + " Key: Ctrl+S.");
        return b;
    }

    /// <summary>A colour swatch; the chosen one wears a ring (shape, not just colour).</summary>
    public ToggleButton Swatch(Color color, string name)
    {
        var fill = new SolidColorBrush(color); fill.Freeze();
        var b = new ToggleButton { Width = 16, Height = 16, Margin = new Thickness(3, 0, 3, 0), Background = fill, Template = SwatchTemplate, Tag = Keep };
        Name(b, name, "Annotation colour: " + name + ".");
        return b;
    }

    /// <summary>A text button with a chevron that opens a dark menu. Add items with <see cref="Item"/>.</summary>
    public (Button Button, ContextMenu Menu) MenuButton(string label, string tip)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, IsHitTestVisible = false };
        row.Children.Add(new TextBlock { Text = label, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Tag = Keep });
        var chev = Icon("chevron", 14); ((FrameworkElement)chev).Margin = new Thickness(4, 0, 0, 0);
        row.Children.Add(chev);
        var b = new Button { Height = 24, Margin = new Thickness(3, 0, 3, 0), Content = row, Template = MenuButtonTemplate, Foreground = Ink, Tag = Keep };
        Name(b, label + " menu", tip);
        var cm = new ContextMenu { Template = MenuTemplate, Foreground = Ink, PlacementTarget = b, Placement = PlacementMode.Bottom, HasDropShadow = false };
        System.Windows.Automation.AutomationProperties.SetName(cm, label);
        b.Click += (_, _) => cm.IsOpen = !cm.IsOpen;
        return (b, cm);
    }

    public MenuItem Item(string header, string tip, Action action)
    {
        var mi = new MenuItem { Header = header, ToolTip = tip, Template = MenuItemTemplate, Foreground = Ink };
        mi.Click += (_, _) => action();
        return mi;
    }

    public Separator Sep() => new() { Style = SeparatorStyle };

    public ControlTemplate ChipTemplate => MenuButtonTemplate;

    public Style SliderStyle => Style(typeof(Slider), SliderTemplate);

    private static Style Style(Type t, ControlTemplate template)
    {
        var s = new Style(t);
        s.Setters.Add(new Setter(Control.TemplateProperty, template));
        return s;
    }

    // ---- templates (XAML text, colours filled from the palette) ---------------------------------

    private const string Ns = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    private string Fill(string xaml) => xaml
        .Replace("@BAR@", _p.Bar).Replace("@EDGE@", _p.Edge).Replace("@INK@", _p.Ink).Replace("@MUTED@", _p.Muted)
        .Replace("@HOVERINK@", _p.HoverInk).Replace("@HOVER@", _p.Hover).Replace("@PRESSED@", _p.Pressed)
        .Replace("@SELINK@", _p.SelInk).Replace("@SELLINE@", _p.SelLine).Replace("@SELECTED@", _p.Selected)
        .Replace("@ACCENTHOVER@", _p.AccentHover).Replace("@ACCENTPRESSED@", _p.AccentPressed).Replace("@ACCENT@", _p.Accent)
        .Replace("@ACTIONINK@", _p.ActionInk).Replace("@FOCUS@", _p.Focus);

    private ControlTemplate Load(string xaml) => (ControlTemplate)XamlReader.Parse(Fill(xaml));

    private ControlTemplate? _toggle, _button, _save, _swatch, _menuButton, _menu, _menuItem, _slider;
    private Style? _sep;

    private ControlTemplate ToggleTemplate => _toggle ??= Load($@"
<ControlTemplate {Ns} TargetType=""ToggleButton"">
  <Grid>
    <Border x:Name=""bg"" CornerRadius=""8"" Background=""Transparent"" BorderBrush=""Transparent"" BorderThickness=""2""/>
    <Rectangle x:Name=""sel"" Height=""2"" RadiusX=""1"" RadiusY=""1"" Fill=""@SELLINE@"" VerticalAlignment=""Bottom"" Margin=""12,0,12,3"" Visibility=""Collapsed""/>
    <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
  </Grid>
  <ControlTemplate.Triggers>
    <Trigger Property=""IsChecked"" Value=""True""><Setter TargetName=""bg"" Property=""Background"" Value=""@SELECTED@""/><Setter TargetName=""sel"" Property=""Visibility"" Value=""Visible""/><Setter Property=""Foreground"" Value=""@SELINK@""/></Trigger>
    <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""bg"" Property=""Background"" Value=""@HOVER@""/><Setter Property=""Foreground"" Value=""@HOVERINK@""/></Trigger>
    <Trigger Property=""IsPressed"" Value=""True""><Setter TargetName=""bg"" Property=""Background"" Value=""@PRESSED@""/></Trigger>
    <Trigger Property=""IsKeyboardFocused"" Value=""True""><Setter TargetName=""bg"" Property=""BorderBrush"" Value=""@FOCUS@""/></Trigger>
    <Trigger Property=""IsEnabled"" Value=""False""><Setter Property=""Opacity"" Value=""0.4""/></Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>");

    private ControlTemplate ButtonTemplate => _button ??= Load($@"
<ControlTemplate {Ns} TargetType=""Button"">
  <Grid>
    <Border x:Name=""bg"" CornerRadius=""8"" Background=""Transparent"" BorderBrush=""Transparent"" BorderThickness=""2""/>
    <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center""/>
  </Grid>
  <ControlTemplate.Triggers>
    <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""bg"" Property=""Background"" Value=""@HOVER@""/><Setter Property=""Foreground"" Value=""@HOVERINK@""/></Trigger>
    <Trigger Property=""IsPressed"" Value=""True""><Setter TargetName=""bg"" Property=""Background"" Value=""@PRESSED@""/></Trigger>
    <Trigger Property=""IsKeyboardFocused"" Value=""True""><Setter TargetName=""bg"" Property=""BorderBrush"" Value=""@FOCUS@""/></Trigger>
    <Trigger Property=""IsEnabled"" Value=""False""><Setter Property=""Opacity"" Value=""0.4""/></Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>");

    private ControlTemplate SaveTemplate => _save ??= Load($@"
<ControlTemplate {Ns} TargetType=""Button"">
  <Grid>
    <Border x:Name=""bg"" CornerRadius=""8"" Background=""@ACCENT@"" BorderBrush=""Transparent"" BorderThickness=""2""/>
    <ContentPresenter HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Margin=""{{TemplateBinding Padding}}""/>
  </Grid>
  <ControlTemplate.Triggers>
    <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""bg"" Property=""Background"" Value=""@ACCENTHOVER@""/></Trigger>
    <Trigger Property=""IsPressed"" Value=""True""><Setter TargetName=""bg"" Property=""Background"" Value=""@ACCENTPRESSED@""/></Trigger>
    <Trigger Property=""IsKeyboardFocused"" Value=""True""><Setter TargetName=""bg"" Property=""BorderBrush"" Value=""@FOCUS@""/></Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>");

    private ControlTemplate SwatchTemplate => _swatch ??= Load($@"
<ControlTemplate {Ns} TargetType=""ToggleButton"">
  <Grid Width=""16"" Height=""16"">
    <Border x:Name=""ring"" Margin=""-4"" CornerRadius=""7"" BorderThickness=""2"" BorderBrush=""Transparent""/>
    <Border CornerRadius=""4"" Background=""{{TemplateBinding Background}}"" BorderBrush=""@EDGE@"" BorderThickness=""1""/>
  </Grid>
  <ControlTemplate.Triggers>
    <Trigger Property=""IsChecked"" Value=""True""><Setter TargetName=""ring"" Property=""BorderBrush"" Value=""@FOCUS@""/></Trigger>
    <Trigger Property=""IsKeyboardFocused"" Value=""True""><Setter TargetName=""ring"" Property=""BorderBrush"" Value=""@FOCUS@""/></Trigger>
    <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""ring"" Property=""BorderBrush"" Value=""@EDGE@""/></Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>");

    private ControlTemplate MenuButtonTemplate => _menuButton ??= Load($@"
<ControlTemplate {Ns} TargetType=""Button"">
  <Border x:Name=""bg"" CornerRadius=""6"" Background=""@BAR@"" BorderBrush=""@EDGE@"" BorderThickness=""1"" Padding=""9,0,6,0"">
    <ContentPresenter VerticalAlignment=""Center""/>
  </Border>
  <ControlTemplate.Triggers>
    <Trigger Property=""IsMouseOver"" Value=""True""><Setter TargetName=""bg"" Property=""Background"" Value=""@HOVER@""/><Setter Property=""Foreground"" Value=""@HOVERINK@""/></Trigger>
    <Trigger Property=""IsPressed"" Value=""True""><Setter TargetName=""bg"" Property=""Background"" Value=""@PRESSED@""/></Trigger>
    <Trigger Property=""IsKeyboardFocused"" Value=""True""><Setter TargetName=""bg"" Property=""BorderBrush"" Value=""@FOCUS@""/></Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>");

    private ControlTemplate MenuTemplate => _menu ??= Load($@"
<ControlTemplate {Ns} TargetType=""ContextMenu"">
  <Border Background=""@BAR@"" BorderBrush=""@EDGE@"" BorderThickness=""1"" CornerRadius=""8"" Padding=""4"" MinWidth=""170"">
    <StackPanel IsItemsHost=""True"" KeyboardNavigation.DirectionalNavigation=""Cycle""/>
  </Border>
</ControlTemplate>");

    private ControlTemplate MenuItemTemplate => _menuItem ??= Load($@"
<ControlTemplate {Ns} TargetType=""MenuItem"">
  <Border x:Name=""bg"" Background=""Transparent"" CornerRadius=""6"" Height=""28"" Padding=""10,0"">
    <ContentPresenter ContentSource=""Header"" VerticalAlignment=""Center"" RecognizesAccessKey=""True""/>
  </Border>
  <ControlTemplate.Triggers>
    <Trigger Property=""IsHighlighted"" Value=""True""><Setter TargetName=""bg"" Property=""Background"" Value=""@HOVER@""/><Setter Property=""Foreground"" Value=""@HOVERINK@""/></Trigger>
    <Trigger Property=""IsEnabled"" Value=""False""><Setter Property=""Foreground"" Value=""@MUTED@""/></Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>");

    private Style SeparatorStyle => _sep ??= Style(typeof(Separator), Load($@"
<ControlTemplate {Ns} TargetType=""Separator"">
  <Border Height=""1"" Margin=""6,3"" Background=""@EDGE@""/>
</ControlTemplate>"));

    private ControlTemplate SliderTemplate => _slider ??= Load($@"
<ControlTemplate {Ns} TargetType=""Slider"">
  <Grid Height=""20"">
    <Border Height=""4"" CornerRadius=""2"" Background=""@EDGE@"" VerticalAlignment=""Center""/>
    <Track x:Name=""PART_Track"">
      <Track.DecreaseRepeatButton>
        <RepeatButton Command=""Slider.DecreaseLarge"" Focusable=""False"">
          <RepeatButton.Template><ControlTemplate TargetType=""RepeatButton""><Border Height=""4"" CornerRadius=""2"" Background=""@ACCENT@"" VerticalAlignment=""Center""/></ControlTemplate></RepeatButton.Template>
        </RepeatButton>
      </Track.DecreaseRepeatButton>
      <Track.IncreaseRepeatButton>
        <RepeatButton Command=""Slider.IncreaseLarge"" Focusable=""False"">
          <RepeatButton.Template><ControlTemplate TargetType=""RepeatButton""><Border Height=""20"" Background=""Transparent""/></ControlTemplate></RepeatButton.Template>
        </RepeatButton>
      </Track.IncreaseRepeatButton>
      <Track.Thumb>
        <Thumb x:Name=""thumb"" Width=""14"" Height=""14"" BorderBrush=""@EDGE@"" Background=""@INK@"">
          <Thumb.Template><ControlTemplate TargetType=""Thumb""><Ellipse Fill=""{{TemplateBinding Background}}"" Stroke=""{{TemplateBinding BorderBrush}}"" StrokeThickness=""2""/></ControlTemplate></Thumb.Template>
        </Thumb>
      </Track.Thumb>
    </Track>
  </Grid>
  <ControlTemplate.Triggers>
    <Trigger Property=""IsKeyboardFocused"" Value=""True""><Setter TargetName=""thumb"" Property=""BorderBrush"" Value=""@FOCUS@""/></Trigger>
    <Trigger Property=""IsEnabled"" Value=""False""><Setter Property=""Opacity"" Value=""0.4""/></Trigger>
  </ControlTemplate.Triggers>
</ControlTemplate>");
}
