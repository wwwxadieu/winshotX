using System.Windows;
using System.Windows.Media.Imaging;
using WinShootX.Models;
using WinShootX.Services;

namespace WinShootX.Views;

/// <summary>
/// Thanh công cụ nổi hiện ngay sau khi chụp — tương đương "floating capture bar" của CleanShot X.
/// Tự đóng nếu người dùng không tương tác gì (giống hành vi bản gốc) — xem <see cref="_autoCloseTimer"/>.
/// </summary>
public partial class CaptureBarWindow : Window
{
    private readonly CaptureResult _capture;
    private readonly SettingsService _settings;
    private readonly System.Windows.Threading.DispatcherTimer _autoCloseTimer;

    public CaptureBarWindow(CaptureResult capture, SettingsService settings)
    {
        InitializeComponent();
        _capture = capture;
        _settings = settings;
        Thumbnail.Source = capture.Image;

        // Đặt thanh công cụ gần góc dưới-phải vùng vừa chụp, kẹp trong biên màn hình.
        PositionNearCapture();

        _autoCloseTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(8),
        };
        _autoCloseTimer.Tick += (_, _) => Close();
        _autoCloseTimer.Start();

        MouseEnter += (_, _) => _autoCloseTimer.Stop();
        MouseLeave += (_, _) => _autoCloseTimer.Start();

        // Lưu ý: việc copy-to-clipboard sau khi chụp đã được App.xaml.cs xử lý ngay khi có
        // CaptureResult (áp dụng cho mọi luồng, kể cả khi mở thẳng Annotator) — không lặp lại ở đây.
    }

    private void PositionNearCapture()
    {
        var bounds = _capture.SourceBounds;
        Left = Math.Min(bounds.Right - 320, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 340);
        Top = Math.Min(bounds.Bottom + 12, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 80);
    }

    private void OnAnnotateClick(object sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        var editor = new AnnotationEditorWindow(_capture.Image);
        editor.Show();
        Close();
    }

    private void OnPinClick(object sender, RoutedEventArgs e)
    {
        var pinned = new PinnedWindow(_capture.Image);
        pinned.Show();
        Close();
    }

    private void OnOcrClick(object sender, RoutedEventArgs e)
    {
        _ = RunOcrAsync();
    }

    private async Task RunOcrAsync()
    {
        var text = await OcrService.RecognizeTextAsync(_capture.Image);
        if (!string.IsNullOrWhiteSpace(text))
        {
            Clipboard.SetText(text);
            MessageBox.Show(this, "Đã copy văn bản nhận diện được vào clipboard.", "OCR",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show(this, "Không tìm thấy văn bản trong ảnh.", "OCR",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var path = FileService.SavePng(_capture.Image, _settings.Current.SaveDirectory);
        Close();
        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
    }

    private void OnCopyClick(object sender, RoutedEventArgs e)
    {
        ClipboardService.CopyImage(_capture.Image);
        Close();
    }

    private void OnShareClick(object sender, RoutedEventArgs e)
    {
        // TODO(Giai đoạn 4): gọi IShareProvider.UploadAsync(_capture.Image) rồi copy link trả về.
        // Xem ShareService.cs để biết interface đề xuất và lý do cần chọn backend trước khi code.
        MessageBox.Show(this,
            "Tính năng chia sẻ cloud sẽ được bổ sung ở Giai đoạn 4 sau khi chọn backend lưu trữ (xem PRD mục 7).",
            "Chia sẻ", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
