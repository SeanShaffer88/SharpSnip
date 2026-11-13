using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Snipit;

internal static class ClipboardWriter
{
    /// <summary>Put plain text on the clipboard (retrying while another process holds it). Returns false if it stayed locked.</summary>
    public static bool SetText(string text)
    {
        try
        {
            for (int attempt = 0; ; attempt++)
            {
                try { Clipboard.SetDataObject(text, true); return true; }
                catch (System.Runtime.InteropServices.COMException) when (attempt < 4) { Thread.Sleep(15); }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Clipboard text write failed", ex);
            return false;
        }
    }

    /// <summary>Put the image on the clipboard as PNG + DIB (§4.5). Returns false if the clipboard stayed locked.</summary>
    public static bool SetImage(BitmapSource image)
    {
        try
        {
            var png = new MemoryStream();
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(image));
            enc.Save(png);
            png.Position = 0;

            var data = new DataObject();
            data.SetImage(image);          // CF_DIB / CF_BITMAP
            data.SetData("PNG", png);      // lossless, keeps transparency for apps that understand it
            // The clipboard can be briefly locked by another process; retry a few times.
            for (int attempt = 0; ; attempt++)
            {
                try { Clipboard.SetDataObject(data, true); return true; }
                catch (System.Runtime.InteropServices.COMException) when (attempt < 4) { Thread.Sleep(15); }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Clipboard write failed", ex);
            return false;
        }
    }
}
