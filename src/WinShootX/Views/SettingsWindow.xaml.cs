using System.Windows;
using Microsoft.Win32;
using WinShootX.Services;

namespace WinShootX.Views;

public partial class SettingsWindow : Window
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "WinShootX";

    private readonly SettingsService _settings;

    public SettingsWindow(SettingsService settings)
    {
        InitializeComponent();
        _settings = settings;

        var s = _settings.Current;
        RegionHotkeyBox.Text = s.RegionCaptureHotkey;
        FullScreenHotkeyBox.Text = s.FullScreenCaptureHotkey;
        WindowHotkeyBox.Text = s.WindowCaptureHotkey;
        SaveDirBox.Text = s.SaveDirectory;
        CopyAfterCaptureCheck.IsChecked = s.CopyToClipboardAfterCapture;
        OpenAnnotatorCheck.IsChecked = s.OpenAnnotatorAfterCapture;
        LaunchAtStartupCheck.IsChecked = s.LaunchAtStartup;
        ShutterSoundCheck.IsChecked = s.PlayShutterSound;
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
