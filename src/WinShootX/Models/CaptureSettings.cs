using System.IO;

namespace WinShootX.Models;

/// <summary>Cài đặt người dùng, được load/save qua SettingsService (JSON trong %AppData%\WinShootX).</summary>
public sealed class CaptureSettings
{
    public string SaveDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "WinShootX");

    public bool CopyToClipboardAfterCapture { get; set; } = true;
    public bool OpenAnnotatorAfterCapture { get; set; } = true;
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
