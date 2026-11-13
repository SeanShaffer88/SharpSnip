using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Path = System.Windows.Shapes.Path;

namespace SharpSnip;

internal enum ModeBarIcon { Element, Region, Window, FullScreen, Freehand, Scrolling, Delay, Close }

/// <summary>One mode-bar button: icon kind, label, screen-reader name (with its key) and the visible key hint.</summary>
internal sealed record ModeBarButton(ModeBarIcon Icon, string Label, string AutomationName, string Key = "");

/// <summary>
/// Line icons for the mode bar, TesselGUI iconography: 24×24 grid, 1.6 stroke,
/// round caps and joins, one small dot accent (Nothing-style). Meaning lives in
/// the outline; the dot is decoration only. Drawn at 22 px.
/// </summary>
internal static class ModeBarIcons
{
    public const double Size = 22;

    public static UIElement Create(ModeBarIcon kind, Brush brush)
    {
        var c = new Canvas { Width = 24, Height = 24 };
        const double t = 1.6;
        Shape Line(Shape s, double left = 0, double top = 0)
        {
            s.Stroke = brush; s.StrokeThickness = t; s.Fill = null;
            s.StrokeStartLineCap = s.StrokeEndLineCap = PenLineCap.Round;
            s.StrokeLineJoin = PenLineJoin.Round;
            Canvas.SetLeft(s, left); Canvas.SetTop(s, top);
            c.Children.Add(s);
            return s;
        }
        Shape P(string d) => Line(new Path { Data = Geometry.Parse(d) });
        void Dot(double x, double y)
        {
            var e = new Ellipse { Width = 1.6, Height = 1.6, Fill = brush };
            Canvas.SetLeft(e, x - .8); Canvas.SetTop(e, y - .8);
            c.Children.Add(e);
        }
        switch (kind)
        {
            case ModeBarIcon.Element:
                Line(new Rectangle { Width = 13, Height = 11, RadiusX = 2.5, RadiusY = 2.5 }, 3.5, 4.5);
                P("M13.5,13 L20,15.6 L17.2,16.6 L16.2,19.4 Z");
                Dot(7, 8.5);
                break;
            case ModeBarIcon.Region:
                var r = (Shape)Line(new Rectangle { Width = 16, Height = 16, RadiusX = 2.5, RadiusY = 2.5 }, 4, 4);
                r.StrokeDashArray = new DoubleCollection { 1.4, 1.9 };
                Dot(12, 12);
                break;
            case ModeBarIcon.Window:
                Line(new Rectangle { Width = 17, Height = 14, RadiusX = 2.5, RadiusY = 2.5 }, 3.5, 5);
                P("M3.5,9.5 H20.5");
                Dot(6.5, 7.3); Dot(9.2, 7.3);
                break;
            case ModeBarIcon.FullScreen:
                P("M4,9 V6.5 A2.5,2.5 0 0 1 6.5,4 H9 M15,4 H17.5 A2.5,2.5 0 0 1 20,6.5 V9 M20,15 V17.5 A2.5,2.5 0 0 1 17.5,20 H15 M9,20 H6.5 A2.5,2.5 0 0 1 4,17.5 V15");
                Dot(12, 12);
                break;
            case ModeBarIcon.Freehand:
                P("M4,16 C6.6,9 8.6,19 11.4,13.4 S16,7 18,8.5");
                Dot(20, 10.5); Dot(5, 19.5);
                break;
            case ModeBarIcon.Scrolling:
                Line(new Rectangle { Width = 11, Height = 17, RadiusX = 2.5, RadiusY = 2.5 }, 6.5, 3.5);
                P("M10,9 L12,7 L14,9 M10,15 L12,17 L14,15");
                Dot(12, 12);
                break;
            case ModeBarIcon.Delay:
                Line(new Ellipse { Width = 16.4, Height = 16.4 }, 3.8, 3.8);
                P("M12,7.5 V12 L15,14");
                Dot(12, 3.2);
                break;
            case ModeBarIcon.Close:
                P("M6.5,6.5 L17.5,17.5 M17.5,6.5 L6.5,17.5");
                break;
        }
        return new Viewbox { Width = Size, Height = Size, Child = c };
    }
}
