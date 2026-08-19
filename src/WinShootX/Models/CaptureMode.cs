namespace WinShootX.Models;

/// <summary>Kiểu chụp mà người dùng chọn.</summary>
public enum CaptureMode
{
    Region,
    FullScreen,
    Window,
}

/// <summary>Kết quả một lần chụp: ảnh bitmap + toạ độ gốc trên desktop (để phục vụ pin/annotate).</summary>
public sealed class CaptureResult
{
    public required System.Windows.Media.Imaging.BitmapSource Image { get; init; }
    public System.Windows.Rect SourceBounds { get; init; }
    public DateTime CapturedAtUtc { get; init; } = DateTime.UtcNow;
}
