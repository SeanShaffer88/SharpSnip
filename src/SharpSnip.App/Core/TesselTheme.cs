using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;

namespace SharpSnip;

/// <summary>
/// TesselGUI revision 2 (GitHub scheme) applied to a window: values copied from
/// the design standard's docs/tokens.json — background #0D1117, surface #0E1218,
/// raised #0F1319, selected #1F2630, foreground #F0F6FC, muted #9198A1, edge
/// #3D444D, focus #79B8FF, selection #2F81F7, action green #1A7F37 family,
/// danger #FFA198; Segoe UI, 13px labels, 1px edges carry the structure.
/// The OS title bar is not themed (that needs WindowChrome; out of scope here).
/// </summary>
internal static class TesselTheme
{
    private static Brush B(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }

    public static readonly Brush Background = B("#0D1117");
    public static readonly Brush Surface = B("#0E1218");
    public static readonly Brush Raised = B("#0F1319");
    public static readonly Brush Selected = B("#1F2630");
    public static readonly Brush Foreground = B("#F0F6FC");
    public static readonly Brush Muted = B("#9198A1");
    public static readonly Brush Edge = B("#3D444D");
    public static readonly Brush Focus = B("#79B8FF");
    public static readonly Brush Selection = B("#2F81F7");
    public static readonly Brush AccentDeep = B("#15263C");
    public static readonly Brush Action = B("#1A7F37");
    public static readonly Brush ActionHover = B("#238636");
    public static readonly Brush ActionPressed = B("#116329");
    public static readonly Brush Danger = B("#FFA198");

    public static void Apply(Window window)
    {
        window.Background = Background;
        window.Foreground = Foreground;
        window.FontFamily = new FontFamily("Segoe UI");
        window.FontSize = 13;
        foreach (var d in Descendants(window)) StyleOne(d);
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
            case TextBlock t:
                if (!ReferenceEquals(t.Foreground, Danger)) t.Foreground = Foreground;
                break;
            case TextBox t:
                t.Background = Surface; t.Foreground = Foreground; t.BorderBrush = Edge;
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
                      <Path Grid.Column="1" HorizontalAlignment="Center" VerticalAlignment="Center" Data="M0,0 L5,5 L10,0" Stroke="#F0F6FC" StrokeThickness="1.5"/>
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
                <Border Background="#0E1218" BorderBrush="#3D444D" BorderThickness="1" CornerRadius="4"/>
                <ScrollViewer Margin="0,2" SnapsToDevicePixels="True">
                  <StackPanel IsItemsHost="True" KeyboardNavigation.DirectionalNavigation="Contained"/>
                </ScrollViewer>
              </Grid>
            </Popup>
          </Grid>
          <ControlTemplate.Triggers>
            <Trigger Property="IsEnabled" Value="False"><Setter Property="Foreground" Value="#9198A1"/></Trigger>
            <Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="ToggleButton" Property="BorderBrush" Value="#79B8FF"/></Trigger>
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
            <Border Name="Box" Width="16" Height="16" Margin="0,1,7,0" Background="#0E1218" BorderBrush="#3D444D" BorderThickness="1" CornerRadius="3">
              <Path Name="Mark" Data="M3,8.5 L6.5,12 L13,4.5" Stroke="#2F81F7" StrokeThickness="2" Visibility="Collapsed"
                  StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/>
            </Border>
            <ContentPresenter VerticalAlignment="Center"/>
          </StackPanel>
          <ControlTemplate.Triggers>
            <Trigger Property="IsChecked" Value="True">
              <Setter TargetName="Mark" Property="Visibility" Value="Visible"/>
              <Setter TargetName="Box" Property="BorderBrush" Value="#2F81F7"/>
            </Trigger>
            <Trigger Property="IsMouseOver" Value="True">
              <Setter TargetName="Box" Property="BorderBrush" Value="#79B8FF"/>
            </Trigger>
            <Trigger Property="IsKeyboardFocused" Value="True">
              <Setter TargetName="Box" Property="BorderBrush" Value="#79B8FF"/>
            </Trigger>
            <Trigger Property="IsEnabled" Value="False">
              <Setter Property="Foreground" Value="#9198A1"/>
            </Trigger>
          </ControlTemplate.Triggers>
        </ControlTemplate>
        """;

    private static ControlTemplate LoadTemplate(string xaml) =>
        (ControlTemplate)XamlReader.Load(new MemoryStream(Encoding.UTF8.GetBytes(xaml)));
}
