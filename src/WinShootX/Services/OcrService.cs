using System.IO;
using System.Text;
using System.Windows.Media.Imaging;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace WinShootX.Services;

/// <summary>
/// Nhận diện văn bản trong ảnh bằng Windows.Media.Ocr — engine OCR có sẵn trong Windows 10/11,
/// chạy offline, hỗ trợ nhiều ngôn ngữ theo gói ngôn ngữ đã cài trên máy (Settings → Time & Language).
/// Yêu cầu csproj target windows10.0.19041.0 trở lên (đã cấu hình).
/// </summary>
public static class OcrService
{
    public static async Task<string> RecognizeTextAsync(BitmapSource image, string? languageTag = null)
    {
        var softwareBitmap = await ToSoftwareBitmapAsync(image);

        var engine = languageTag != null
            ? OcrEngine.TryCreateFromLanguage(new Language(languageTag))
            : OcrEngine.TryCreateFromUserProfileLanguages();

        if (engine == null)
        {
            // Không có gói ngôn ngữ OCR phù hợp đã cài trên máy.
            return string.Empty;
        }

        var result = await engine.RecognizeAsync(softwareBitmap);

        var sb = new StringBuilder();
        foreach (var line in result.Lines)
            sb.AppendLine(line.Text);

        return sb.ToString().Trim();
    }

    private static async Task<SoftwareBitmap> ToSoftwareBitmapAsync(BitmapSource image)
    {
        // Encode ảnh WPF thành PNG trong bộ nhớ rồi decode lại bằng WinRT BitmapDecoder —
        // cách đơn giản nhất để cầu nối giữa System.Windows.Media và Windows.Graphics.Imaging
        // mà không cần thao tác trực tiếp trên buffer pixel.
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
        using var memoryStream = new MemoryStream();
        encoder.Save(memoryStream);
        memoryStream.Position = 0;

        using var randomAccessStream = new InMemoryRandomAccessStream();
        using (var outputStream = randomAccessStream.GetOutputStreamAt(0))
        {
            var writer = new DataWriter(outputStream);
            writer.WriteBytes(memoryStream.ToArray());
            await writer.StoreAsync();
            await outputStream.FlushAsync();
            writer.DetachStream();
        }

        var decoder = await Windows.Graphics.Imaging.BitmapDecoder.CreateAsync(randomAccessStream);
        return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
    }
}
