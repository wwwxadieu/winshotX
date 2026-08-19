using System.IO;

namespace WinShootX.Models;

/// <summary>Cài đặt người dùng, được load/save qua SettingsService (JSON trong %AppData%\WinShootX).</summary>
public sealed class CaptureSettings
{
    public string SaveDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "WinShootX");

    public bool CopyToClipboardAfterCapture { get; set; } = true;
    // false: sau khi chụp hiện CaptureBarWindow (thanh công cụ nổi, giống CleanShot X) trước — người
    // dùng bấm "Chú thích" trên đó mới mở AnnotationEditorWindow đầy đủ. true (không phải mặc định)
    // bỏ qua thanh công cụ nổi, mở thẳng trình chú thích. Trước đây mặc định là true khiến người dùng
    // không bao giờ thấy được thanh công cụ nổi khi chạy thử lần đầu.
    public bool OpenAnnotatorAfterCapture { get; set; } = false;
    public bool LaunchAtStartup { get; set; } = false;
    public bool PlayShutterSound { get; set; } = true;

    // Hotkeys ở dạng chuỗi mô tả (vd "Ctrl+Shift+4"); parse trong HotkeyManager.
    public string RegionCaptureHotkey { get; set; } = "Ctrl+Shift+4";
    public string FullScreenCaptureHotkey { get; set; } = "Ctrl+Shift+3";
    public string WindowCaptureHotkey { get; set; } = "Ctrl+Shift+5";
    public string ScrollingCaptureHotkey { get; set; } = "Ctrl+Shift+6";
    public string ScreenRecordingHotkey { get; set; } = "Ctrl+Shift+7";

    public int JpegQuality { get; set; } = 95;
}
