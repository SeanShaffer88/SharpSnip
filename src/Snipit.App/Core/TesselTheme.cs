using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace Snipit;

/// <summary>
/// TesselGUI revision 3 (Carmine, standard v2.0.0) applied to a window: values
/// copied from the design standard's docs/tokens.json — background #1B1B1D,
/// surface #1F1F22, raised #232326, selected #2A2A2F, foreground #E6E3E1, muted
/// #89868A, edge #43434A, and the one Carmine accent #960018 carrying
/// selection and the action family, with a light tint #E8939F for focus rings and check marks (the deep accent is only ~1.9:1 on the dark surfaces, below WCAG 3:1), (hover #A81226, pressed #7E0014), danger
/// #E8939F; Segoe UI, 13px labels, 1px edges carry the structure.
/// The OS title bar is not themed (that needs WindowChrome; out of scope here).
/// </summary>
internal static class TesselTheme
{
    private static Brush B(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }

    public static readonly Brush Background = B("#1B1B1D");
    public static readonly Brush Surface = B("#1F1F22");
    public static readonly Brush Raised = B("#232326");
    public static readonly Brush Selected = B("#2A2A2F");
    public static readonly Brush Foreground = B("#E6E3E1");
    public static readonly Brush Muted = B("#89868A");
    public static readonly Brush Edge = B("#43434A");
    public static readonly Brush Focus = B("#E8939F");
    public static readonly Brush Selection = B("#960018");
    public static readonly Brush AccentDeep = B("#33333A");
    public static readonly Brush Action = B("#960018");
    public static readonly Brush ActionHover = B("#A81226");
    public static readonly Brush ActionPressed = B("#7E0014");
    public static readonly Brush Danger = B("#E8939F");

    public static void Apply(Window window)
    {
        if (SystemParameters.HighContrast)
        {
            // Windows High Contrast: use the user's system colours and the stock control templates.
            window.Background = SystemColors.WindowBrush;
            window.Foreground = SystemColors.WindowTextBrush;
            foreach (var d in Descendants(window))
            {
                if (d is Control c) { c.ClearValue(Control.BackgroundProperty); c.ClearValue(Control.ForegroundProperty); c.ClearValue(Control.BorderBrushProperty); }
                else if (d is TextBlock t) t.ClearValue(TextBlock.ForegroundProperty);
            }
            Name(window);
            return;
        }
        window.Background = Background;
        window.Foreground = Foreground;
        window.FontFamily = new FontFamily("Segoe UI");
        window.FontSize = 13;
        foreach (var d in Descendants(window)) StyleOne(d);
        Name(window);
    }

    /// <summary>
    /// Gives screen readers names: a text label sitting in the same grid row, to the left of an input,
    /// becomes that input's LabeledBy; a string tooltip becomes its help text. Existing names win.
    /// </summary>
    private static void Name(Window window)
    {
        foreach (var d in Descendants(window))
        {
            if (d is Grid g)
            {
                var kids = g.Children.OfType<FrameworkElement>().ToList();
                foreach (var label in kids.OfType<TextBlock>().Where(t => !string.IsNullOrWhiteSpace(t.Text)))
                {
                    int row = Grid.GetRow(label), col = Grid.GetColumn(label);
                    var target = kids.FirstOrDefault(k => k is not TextBlock && Grid.GetRow(k) == row && Grid.GetColumn(k) > col);
                    if (target is not null && AutomationProperties.GetLabeledBy(target) is null
                        && string.IsNullOrEmpty(AutomationProperties.GetName(target)))
                        AutomationProperties.SetLabeledBy(target, label);
                }
            }
            if (d is FrameworkElement fe && fe.ToolTip is string tip && string.IsNullOrEmpty(AutomationProperties.GetHelpText(fe)))
                AutomationProperties.SetHelpText(fe, tip);
        }
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root))
        {
            if (child is not DependencyObject d) continue;
            yield return d;
            foreach (var sub in Descendants(d)) yield return sub;
        }
    }

    private static void StyleOne(DependencyObject d)
    {
        switch (d)
        {
            case FrameworkElement k when k.Tag is "keep":   // owner of the control styles it (the editor's chrome)
                break;
            case TextBlock t:
                if (!ReferenceEquals(t.Foreground, Danger)) t.Foreground = Foreground;
                break;
            case TextBox t:
                t.Background = Surface; t.Foreground = Foreground; t.BorderBrush = Edge;
                t.GotKeyboardFocus += (_, _) => t.BorderBrush = Focus;     // WPF's stock text box shows no focus ring on this theme
                t.LostKeyboardFocus += (_, _) => t.BorderBrush = Edge;
                t.BorderThickness = new Thickness(1); t.CaretBrush = Foreground;
                t.SelectionBrush = Selection; t.Padding = new Thickness(6, 3, 6, 3);
                break;
            case ComboBox c:
                c.Template = ComboTemplate; c.ItemContainerStyle = ComboItemStyle;
                c.Background = Raised; c.Foreground = Foreground; c.BorderBrush = Edge;
                c.BorderThickness = new Thickness(1);
                break;
            case CheckBox c:
                c.Template = CheckTemplate; c.Foreground = Foreground;
                break;
            case Slider s:
                s.Foreground = Muted;
                break;
            case Button b:
                b.Style = b.IsDefault ? PrimaryButtonStyle : ButtonStyle;
                break;
        }
    }

    // ---- buttons ---------------------------------------------------------------

    private static ControlTemplate MakeButtonTemplate()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderBrushProperty, new Binding("BorderBrush") { RelativeSource = RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderThicknessProperty, new Binding("BorderThickness") { RelativeSource = RelativeSource.TemplatedParent });
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);
        return new ControlTemplate(typeof(Button)) { VisualTree = border };
    }

    private static Style MakeButtonStyle(Brush bg, Brush hover, Brush pressed, Brush fg)
    {
        var s = new Style(typeof(Button));
        s.Setters.Add(new Setter(Control.TemplateProperty, MakeButtonTemplate()));
        s.Setters.Add(new Setter(Control.BackgroundProperty, bg));
        s.Setters.Add(new Setter(Control.ForegroundProperty, fg));
        s.Setters.Add(new Setter(Control.BorderBrushProperty, Edge));
        s.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
        s.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(10, 4, 10, 4)));
        var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        over.Setters.Add(new Setter(Control.BackgroundProperty, hover));
        s.Triggers.Add(over);
        var down = new Trigger { Property = Button.IsPressedProperty, Value = true };
        down.Setters.Add(new Setter(Control.BackgroundProperty, pressed));
        s.Triggers.Add(down);
        var foc = new Trigger { Property = UIElement.IsKeyboardFocusedProperty, Value = true };
        foc.Setters.Add(new Setter(Control.BorderBrushProperty, Focus));
        s.Triggers.Add(foc);
        var off = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        off.Setters.Add(new Setter(Control.ForegroundProperty, Muted));
        s.Triggers.Add(off);
        return s;
    }

    private static Style? _buttonStyle, _primaryButtonStyle;
    private static Style ButtonStyle => _buttonStyle ??= MakeButtonStyle(Raised, Selected, AccentDeep, Foreground);
    private static Style PrimaryButtonStyle => _primaryButtonStyle ??= MakeButtonStyle(Action, ActionHover, ActionPressed, Brushes.White);

    // ---- combo box ---------------------------------------------------------------

    private static ControlTemplate? _comboTemplate;
    private static ControlTemplate ComboTemplate => _comboTemplate ??= LoadTemplate(ComboXaml);

    private const string ComboXaml = """
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ComboBox">
          <Grid>
            <ToggleButton Name="ToggleButton" Focusable="False" ClickMode="Press"
                IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"
                Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
              <ToggleButton.Template>
                <ControlTemplate TargetType="ToggleButton">
                  <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="4">
                    <Grid>
                      <Grid.ColumnDefinitions><ColumnDefinition Width="*"/><ColumnDefinition Width="18"/></Grid.ColumnDefinitions>
                      <Path Grid.Column="1" HorizontalAlignment="Center" VerticalAlignment="Center" Data="M0,0 L5,5 L10,0" Stroke="#E6E3E1" StrokeThickness="1.5"/>
                    </Grid>
                  </Border>
                </ControlTemplate>
              </ToggleButton.Template>
            </ToggleButton>
            <ContentPresenter Name="ContentSite" IsHitTestVisible="False" Content="{TemplateBinding SelectionBoxItem}"
                ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}" Margin="7,3,24,3"
                VerticalAlignment="Center" HorizontalAlignment="Left"/>
            <Popup Name="Popup" Placement="Bottom" IsOpen="{TemplateBinding IsDropDownOpen}" AllowsTransparency="True" Focusable="False" PopupAnimation="Slide">
              <Grid SnapsToDevicePixels="True" MinWidth="{TemplateBinding ActualWidth}" MaxHeight="{TemplateBinding MaxDropDownHeight}">
                <Border Background="#1F1F22" BorderBrush="#43434A" BorderThickness="1" CornerRadius="4"/>
                <ScrollViewer Margin="0,2" SnapsToDevicePixels="True">
                  <StackPanel IsItemsHost="True" KeyboardNavigation.DirectionalNavigation="Contained"/>
                </ScrollViewer>
              </Grid>
            </Popup>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsEnabled" Value="False"><Setter Property="Foreground" Value="#89868A"/></Trigger>
            <Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="ToggleButton" Property="BorderBrush" Value="#E8939F"/></Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
        """;

    private static Style? _comboItemStyle;
    private static Style ComboItemStyle => _comboItemStyle ??= MakeComboItemStyle();

    private static Style MakeComboItemStyle()
    {
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetBinding(Border.BackgroundProperty, new Binding("Background") { RelativeSource = RelativeSource.TemplatedParent });
        border.SetValue(Border.PaddingProperty, new Thickness(7, 4, 7, 4));
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        border.AppendChild(content);
        var s = new Style(typeof(ComboBoxItem));
        s.Setters.Add(new Setter(Control.TemplateProperty, new ControlTemplate(typeof(ComboBoxItem)) { VisualTree = border }));
        s.Setters.Add(new Setter(Control.ForegroundProperty, Foreground));
        s.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
        var hot = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true };
        hot.Setters.Add(new Setter(Control.BackgroundProperty, Selected));
        s.Triggers.Add(hot);
        var sel = new Trigger { Property = ComboBoxItem.IsSelectedProperty, Value = true };
        sel.Setters.Add(new Setter(Control.BackgroundProperty, AccentDeep));
        s.Triggers.Add(sel);
        return s;
    }

    // ---- check box ---------------------------------------------------------------

    private static ControlTemplate? _checkTemplate;
    private static ControlTemplate CheckTemplate => _checkTemplate ??= LoadTemplate(CheckXaml);

    private const string CheckXaml = """
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="CheckBox">
          <StackPanel Orientation="Horizontal" Background="Transparent">
            <Border Name="Box" Width="16" Height="16" Margin="0,1,7,0" Background="#1F1F22" BorderBrush="#43434A" BorderThickness="1" CornerRadius="3">
              <Path Name="Mark" Data="M3,8.5 L6.5,12 L13,4.5" Stroke="#E8939F" StrokeThickness="2" Visibility="Collapsed"
                  StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/>
            </Border>
            <ContentPresenter VerticalAlignment="Center"/>
          </StackPanel>
          <ControlTemplate.Triggers>
            <Trigger Property="IsChecked" Value="True">
              <Setter TargetName="Mark" Property="Visibility" Value="Visible"/>
              <Setter TargetName="Box" Property="BorderBrush" Value="#E8939F"/>
            </Trigger>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Box" Property="BorderBrush" Value="#E8939F"/>
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True">
              <Setter TargetName="Box" Property="BorderBrush" Value="#E8939F"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Foreground" Value="#89868A"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
        """;

    private static ControlTemplate LoadTemplate(string xaml) =>
        (ControlTemplate)XamlReader.Load(new MemoryStream(Encoding.UTF8.GetBytes(xaml)));
}
