using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Path = System.Windows.Shapes.Path;

namespace SharpSnip;

internal enum ModeBarIcon { Element, Region, Window, FullScreen, Freehand, Scrolling, Delay, Close }

/// <summary>One mode-bar button: icon kind, label under the icon, and the screen-reader name (with its key).</summary>
internal sealed record ModeBarButton(ModeBarIcon Icon, string Label, string AutomationName);

/// <summary>
/// Vector glyphs for the mode bar, drawn on a 28×28 canvas so the bar matches
/// the picked design (icons above, labels underneath) at any DPI.
/// </summary>
internal static class ModeBarIcons
{
    public static UIElement Create(ModeBarIcon kind, Brush brush)
    {
        var c = new Canvas { Width = 28, Height = 28 };
        const double t = 1.8;
        Shape Stroke(UIElement shape, double left, double top)
        {
            var s = (Shape)shape;
            s.Stroke = brush; s.StrokeThickness = t;
            Canvas.SetLeft(s, left); Canvas.SetTop(s, top);
            c.Children.Add(s);
            return s;
        }
        void Fill(Shape shape, double left, double top)
        {
            shape.Fill = brush;
            Canvas.SetLeft(shape, left); Canvas.SetTop(shape, top);
            c.Children.Add(shape);
        }
        switch (kind)
        {
            case ModeBarIcon.Element:
                Stroke(new Rectangle { Width = 16, Height = 14, RadiusX = 2, RadiusY = 2 }, 3, 4);
                Fill(new Path { Data = Geometry.Parse("M16,14 L24.5,20 L20,20.8 L22.6,25.4 L20.4,26.6 L18,21.9 L16.3,25.2 Z") }, 0, 0);
                break;
            case ModeBarIcon.Region:
                var r = (Rectangle)Stroke(new Rectangle { Width = 19, Height = 16 }, 4.5, 6);
                r.StrokeDashArray = new DoubleCollection { 4, 3 };
                break;
            case ModeBarIcon.Window:
                Stroke(new Rectangle { Width = 20, Height = 18, RadiusX = 2, RadiusY = 2 }, 4, 5);
                Stroke(new Line { X1 = 0, Y1 = 0, X2 = 20, Y2 = 0 }, 4, 11);
                Fill(new Ellipse { Width = 3.4, Height = 3.4 }, 7, 6.8);
                break;
            case ModeBarIcon.FullScreen:
                Stroke(new Path { Data = Geometry.Parse("M10,4 H4 V10 M18,4 H24 V10 M24,18 V24 H18 M10,24 H4 V18"), Fill = null }, 0, 0);
                break;
            case ModeBarIcon.Freehand:
                Stroke(new Path { Data = Geometry.Parse("M5,21 C4,12 10,4.5 15,7.5 C20,10.5 12.5,13.5 14.5,18.5 C16,22.5 22,21.5 23,15.5"), Fill = null }, 0, 0);
                break;
            case ModeBarIcon.Scrolling:
                Stroke(new Rectangle { Width = 11, Height = 22, RadiusX = 2.5, RadiusY = 2.5 }, 8.5, 3);
                Fill(new Path { Data = Geometry.Parse("M14,7.5 L10.8,11.5 H17.2 Z") }, 0, 0);
                Fill(new Path { Data = Geometry.Parse("M14,20.5 L10.8,16.5 H17.2 Z") }, 0, 0);
                break;
            case ModeBarIcon.Delay:
                Stroke(new Ellipse { Width = 19, Height = 19 }, 4.5, 4.5);
                Stroke(new Line { X1 = 0, Y1 = 0, X2 = 0, Y2 = -5.5 }, 14, 14);
                Stroke(new Line { X1 = 0, Y1 = 0, X2 = 4.5, Y2 = 0 }, 14, 14);
                break;
            case ModeBarIcon.Close:
                Stroke(new Line { X1 = 0, Y1 = 0, X2 = 11, Y2 = 11 }, 8.5, 8.5);
                Stroke(new Line { X1 = 0, Y1 = 11, X2 = 11, Y2 = 0 }, 8.5, 8.5);
                break;
        }
        return c;
    }
}
