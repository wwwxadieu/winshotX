using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using WinShootX.Services;

namespace WinShootX.Views;

public partial class SettingsWindow : Window
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "WinShootX";

    private static readonly Brush ConflictBorderBrush = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
    private const string ConflictTooltip =
        "Phím tắt này đang bị ứng dụng khác trên máy chiếm dụng nên không hoạt động — " +
        "đổi sang tổ hợp phím khác rồi bấm Lưu và khởi động lại app.";

    private readonly SettingsService _settings;

    /// <summary>
    /// <paramref name="conflictingHotkeys"/>: các hotkey đã khai báo nhưng không đăng ký được lúc app
    /// khởi động (xem _conflictingHotkeys trong App.xaml.cs) — dùng để đánh dấu trực quan ô nào đang
    /// "hỏng" thay vì chỉ dựa vào balloon tip thoáng qua lúc khởi động.
    /// </summary>
    public SettingsWindow(SettingsService settings, HashSet<string>? conflictingHotkeys = null)
    {
        InitializeComponent();
        _settings = settings;

        var s = _settings.Current;
        RegionHotkeyBox.Text = s.RegionCaptureHotkey;
        FullScreenHotkeyBox.Text = s.FullScreenCaptureHotkey;
        WindowHotkeyBox.Text = s.WindowCaptureHotkey;
        ScrollHotkeyBox.Text = s.ScrollingCaptureHotkey;
        RecordHotkeyBox.Text = s.ScreenRecordingHotkey;
        SaveDirBox.Text = s.SaveDirectory;
        CopyAfterCaptureCheck.IsChecked = s.CopyToClipboardAfterCapture;
        OpenAnnotatorCheck.IsChecked = s.OpenAnnotatorAfterCapture;
        LaunchAtStartupCheck.IsChecked = s.LaunchAtStartup;
        ShutterSoundCheck.IsChecked = s.PlayShutterSound;

        if (conflictingHotkeys is { Count: > 0 })
        {
            MarkIfConflicting(RegionHotkeyBox, conflictingHotkeys);
            MarkIfConflicting(FullScreenHotkeyBox, conflictingHotkeys);
            MarkIfConflicting(WindowHotkeyBox, conflictingHotkeys);
            MarkIfConflicting(ScrollHotkeyBox, conflictingHotkeys);
            MarkIfConflicting(RecordHotkeyBox, conflictingHotkeys);
        }
    }

    private static void MarkIfConflicting(TextBox box, HashSet<string> conflictingHotkeys)
    {
        if (!conflictingHotkeys.Contains(box.Text)) return;

        box.BorderBrush = ConflictBorderBrush;
        box.BorderThickness = new Thickness(1.5);
        box.ToolTip = ConflictTooltip;

        // Đánh dấu chỉ có ý nghĩa với giá trị lúc mở Settings — một khi người dùng gõ giá trị khác,
        // gỡ đánh dấu ngay vì không còn biết tổ hợp mới có xung đột hay không (chỉ xác nhận được sau
        // khi lưu + khởi động lại app).
        box.TextChanged += (_, _) =>
        {
            box.ClearValue(BorderBrushProperty);
            box.ClearValue(BorderThicknessProperty);
            box.ToolTip = null;
        };
    }

    private void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        // OpenFolderDialog có từ .NET 8 (Microsoft.Win32) — không cần thư viện ngoài như trước đây.
        var dialog = new OpenFolderDialog { InitialDirectory = SaveDirBox.Text };
        if (dialog.ShowDialog() == true)
            SaveDirBox.Text = dialog.FolderName;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var s = _settings.Current;
        s.RegionCaptureHotkey = RegionHotkeyBox.Text;
        s.FullScreenCaptureHotkey = FullScreenHotkeyBox.Text;
        s.WindowCaptureHotkey = WindowHotkeyBox.Text;
        s.ScrollingCaptureHotkey = ScrollHotkeyBox.Text;
        s.ScreenRecordingHotkey = RecordHotkeyBox.Text;
        s.SaveDirectory = SaveDirBox.Text;
        s.CopyToClipboardAfterCapture = CopyAfterCaptureCheck.IsChecked == true;
        s.OpenAnnotatorAfterCapture = OpenAnnotatorCheck.IsChecked == true;
        s.LaunchAtStartup = LaunchAtStartupCheck.IsChecked == true;
        s.PlayShutterSound = ShutterSoundCheck.IsChecked == true;
        _settings.Save();
        ApplyStartupRegistration(s.LaunchAtStartup);

        // Lưu ý: hotkey chỉ đăng ký lại khi app khởi động (SetupHotkeys trong App.xaml.cs) — cần
        // restart để áp dụng thay đổi hotkey trong phiên hiện tại.
        MessageBox.Show(this, "Đã lưu cài đặt. Một số thay đổi (hotkey) cần khởi động lại app để áp dụng.",
            "Win ShootX", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e) => Close();

    /// <summary>Thêm/xoá entry autorun của app trong HKCU\...\Run — không cần quyền admin vì chỉ ghi HKCU.</summary>
    private static void ApplyStartupRegistration(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key == null) return;

        if (enabled)
        {
            var exePath = Environment.ProcessPath;
            if (exePath != null)
                key.SetValue(RunValueName, $"\"{exePath}\"");
        }
        else
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
    }
}
