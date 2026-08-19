using System.IO;
using System.Windows.Media.Imaging;

namespace WinShootX.Services;

public static class FileService
{
    /// <summary>Lưu ảnh ra PNG (mặc định, không mất chi tiết — phù hợp screenshot chứa text/UI).</summary>
    public static string SavePng(BitmapSource image, string directory, string? fileNameWithoutExt = null)
    {
        Directory.CreateDirectory(directory);
        var name = fileNameWithoutExt ?? $"WinShootX_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}";
        var path = Path.Combine(directory, name + ".png");

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
        encoder.Save(stream);
        return path;
    }
}
