using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Snipit.Core;
using Snipit.Native;

namespace Snipit;

/// <summary>Small always-on-top countdown chip, excluded from capture, click-through.</summary>
internal sealed class CountdownChip : Window
{
    private readonly TextBlock _text = new() { Foreground = Brushes.White, FontFamily = new FontFamily("Segoe UI"), FontSize = 16 };
    private readonly MonitorInfo _monitor;

    public CountdownChip(MonitorInfo monitor)
    {
        _monitor = monitor;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; AllowsTransparency = true;
        Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; Topmost = true; Focusable = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        WindowStartupLocation = WindowStartupLocation.Manual; Left = 0; Top = 0;
        Title = "Snipit countdown";
        AutomationProperties.SetLiveSetting(_text, AutomationLiveSetting.Polite);
        Content = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xE6, 0x14, 0x14, 0x14)), CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 8, 14, 8), Child = _text,
        };
    }

    public void SetRemaining(int seconds)
    {
        _text.Text = $"Capturing in {seconds}…   [Esc] cancel";
        AutomationProperties.SetName(_text, $"Capturing in {seconds} seconds");
        if (System.Windows.Automation.Peers.AutomationPeer.ListenerExists(System.Windows.Automation.Peers.AutomationEvents.LiveRegionChanged))
            System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(_text)?.RaiseAutomationEvent(System.Windows.Automation.Peers.AutomationEvents.LiveRegionChanged);
        Reposition();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        NativeMethods.AddExStyle(hwnd, NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE | NativeMethods.WS_EX_LAYERED);
        NativeMethods.SetWindowDisplayAffinity(hwnd, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
        Reposition();
    }

    private void Reposition()
    {
        IntPtr hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        UpdateLayout();
        double s = IsLoaded ? VisualTreeHelper.GetDpi(this).DpiScaleX : _monitor.Scale;
        int w = (int)Math.Ceiling(ActualWidth * s), h = (int)Math.Ceiling(ActualHeight * s);
        var b = _monitor.Bounds;
        NativeMethods.SetWindowPos(hwnd, NativeMethods.HWND_TOPMOST, b.Right - w - 24, b.Y + 24, w, h, NativeMethods.SWP_NOACTIVATE);
    }
}

internal static class DelayTimer
{
    /// <summary>
    /// Counts down (§3D.4). Esc aborts. Returns false when cancelled. Needs a started <see cref="InputHook"/>.
    /// The chip is excluded from capture and closed before this returns.
    /// </summary>
    public static async Task<bool> RunAsync(int seconds, InputHook hook, MonitorInfo where, CancellationToken ct)
    {
        if (seconds <= 0) return true;
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var chip = new CountdownChip(where);
        hook.ClearHandlers();
        hook.KeyDown = vk =>
        {
            if (vk != 0x1B) return false;
            cts.Cancel();
            return true;
        };
        try
        {
            chip.Show();
            for (int s = seconds; s > 0; s--)
            {
                chip.SetRemaining(s);
                await Task.Delay(1000, cts.Token);
            }
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            hook.ClearHandlers();
            chip.Close();
        }
    }
}
