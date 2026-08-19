using System.Windows;
using WinShootX.Services;

namespace WinShootX.Views;

/// <summary>Thanh điều khiển nổi hiện trong lúc quay màn hình — hiện thời gian đã quay + nút Dừng.
/// Tương đương thanh điều khiển quay của CleanShot X (không có Pause: xem lý do trong
/// <see cref="ScreenRecordingService"/> — gdigrab của ffmpeg không hỗ trợ tạm dừng/tiếp tục gọn gàng).</summary>
public partial class RecordingControlWindow : Window
{
    private readonly ScreenRecordingService _recordingService;

    public event Action<string>? RecordingStopped;

    public RecordingControlWindow(ScreenRecordingService recordingService, Rect region)
    {
        InitializeComponent();
        _recordingService = recordingService;
        _recordingService.RecordingTimeChanged += OnRecordingTimeChanged;

        Left = Math.Min(region.Right - 160, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 180);
        Top = Math.Max(SystemParameters.VirtualScreenTop + 8, region.Top - 48);
    }

    private void OnRecordingTimeChanged(TimeSpan elapsed) =>
        Dispatcher.Invoke(() => ElapsedText.Text = elapsed.ToString(@"mm\:ss"));

    private async void OnStopClick(object sender, RoutedEventArgs e)
    {
        _recordingService.RecordingTimeChanged -= OnRecordingTimeChanged;
        var outputPath = await _recordingService.StopAsync();
        RecordingStopped?.Invoke(outputPath);
        Close();
    }
}
