using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SharpSnip.Core;
using SharpSnip.Native;

namespace SharpSnip;

/// <summary>
/// Live annotation: a transparent topmost window over one monitor that you
/// draw on with the mouse. Because it is an ordinary window, screen capture
/// and recording see the strokes as they happen. Keys: 1–5 change the pen
/// colour, C clears, Esc finishes.
/// </summary>
internal sealed class DrawOverlayWindow : Window
{
    private static readonly Color[] Pens = { Colors.Red, Colors.Yellow, Colors.LimeGreen, Colors.DodgerBlue, Colors.White };
    private readonly Canvas _canvas = new() { Background = Brushes.Transparent };
    private readonly MonitorInfo _monitor;
    private Polyline? _stroke;
    private int _penIndex;

    public DrawOverlayWindow(MonitorInfo monitor)
    {
        _monitor = monitor;
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = 0; Top = 0;
        Width = monitor.Bounds.Width / monitor.Scale;
        Height = monitor.Bounds.Height / monitor.Scale;
        Cursor = Cursors.Pen;
        Content = _canvas;

        _canvas.MouseDown += (_, e) =>
        {
            if (e.ChangedButton != MouseButton.Left) return;
            _stroke = new Polyline
            {
                Stroke = new SolidColorBrush(Pens[_penIndex]),
                StrokeThickness = 3.5,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };
            _stroke.Points.Add(e.GetPosition(_canvas));
            _canvas.Children.Add(_stroke);
            _canvas.CaptureMouse();
        };
        _canvas.MouseMove += (_, e) => { if (_stroke is not null && e.LeftButton == MouseButtonState.Pressed) _stroke.Points.Add(e.GetPosition(_canvas)); };
        _canvas.MouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Left) { _stroke = null; _canvas.ReleaseMouseCapture(); } };
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) Close();
            else if (e.Key == Key.C) _canvas.Children.Clear();
            else if (e.Key >= Key.D1 && e.Key <= Key.D5) _penIndex = e.Key - Key.D1;
        };
        SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, monitor.Bounds.X, monitor.Bounds.Y,
                monitor.Bounds.Width, monitor.Bounds.Height, 0);
        };
    }
}
