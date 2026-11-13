using System.IO;
using System.Windows.Media.Imaging;
using SharpSnip.Core;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace SharpSnip;

/// <summary>
/// Text recognition with the OCR engine that ships inside Windows
/// (Windows.Media.Ocr — no package, fully offline). The engine recognises;
/// reading-order assembly lives in tested Core (<see cref="OcrText"/>), and
/// sensitive-span detection in <see cref="Redactor"/>.
/// Returns (null, reason) when no OCR language is installed.
/// </summary>
internal static class OcrService
{
    public static async Task<(string? Text, string? Error)> RecognizeAsync(BitmapSource image)
    {
        var (words, error) = await RecognizeWordsAsync(image);
        return words is null ? (null, error) : (OcrText.Assemble(words), null);
    }

    public static async Task<(IReadOnlyList<Core.OcrWord>? Words, string? Error)> RecognizeWordsAsync(BitmapSource image)
    {
        var engine = OcrEngine.TryCreateFromUserProfileLanguages();
        if (engine is null)
            return (null, "No OCR language is installed. Add one under Windows Settings → Language, then try again.");

        byte[] png;
        using (var ms = new MemoryStream())
        {
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
            enc.Save(ms);
            png = ms.ToArray();
        }

        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream))
        {
            writer.WriteBytes(png);
            await writer.StoreAsync();
            writer.DetachStream();
        }
        stream.Seek(0);
        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(stream);
        using var software = await decoder.GetSoftwareBitmapAsync();
        var result = await engine.RecognizeAsync(software);

        var words = result.Lines
            .SelectMany(l => l.Words)
            .Select(w => new Core.OcrWord(w.Text, new RectPx(
                (int)Math.Round(w.BoundingRect.X), (int)Math.Round(w.BoundingRect.Y),
                Math.Max(1, (int)Math.Round(w.BoundingRect.Width)), Math.Max(1, (int)Math.Round(w.BoundingRect.Height)))))
            .ToList();
        return (words, null);
    }
}
