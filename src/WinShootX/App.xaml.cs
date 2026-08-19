using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Hardcodet.Wpf.TaskbarNotification;
using Velopack;
using Velopack.Sources;
using WinShootX.Models;
using WinShootX.Services;
using WinShootX.Views;
using WinShootX.Views.Controls;

namespace WinShootX;

/// <summary>
/// Điểm khởi động: dựng tray icon, đăng ký hotkey toàn cục, điều phối luồng
/// chụp → hiện thanh công cụ nổi. Đây là "nhạc trưởng" nối các Services/Views lại với nhau.
/// </summary>
public partial class App : Application
{
    // URL repo GitHub công khai chứa các bản Release đã đóng gói qua Velopack (vpk) — nguồn auto-update.
    private const string UpdateFeedUrl = "https://github.com/wwwxadieu/winshotX";

    private TaskbarIcon? _trayIcon;
    private HotkeyManager? _hotkeyManager;
    // Phím tắt đã khai báo trong Cài đặt nhưng không đăng ký được lúc khởi động (bị app khác chiếm
    // dụng) — SettingsWindow đọc tập này để đánh dấu trực quan ô nào đang "hỏng", thay vì chỉ dựa vào
    // balloon tip thoáng qua lúc khởi động mà người dùng dễ bỏ lỡ.
    private readonly HashSet<string> _conflictingHotkeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly ScreenCaptureService _captureService = new();
    private readonly ScrollingCaptureService _scrollingCaptureService = new();
    private readonly ScreenRecordingService _recordingService = new();
    private readonly SettingsService _settingsService = new();
    private readonly HistoryService _historyService = new();

    public App()
    {
        // Bắt buộc gọi càng sớm càng tốt, trước bất kỳ code nào khác: Velopack chặn và xử lý riêng
        // các lần khởi động app do chính installer gọi để chạy hook cài đặt/gỡ cài đặt/cập nhật
        // (--veloapp-install, --veloapp-updated, --veloapp-uninstall, ...) rồi thoát ngay — không để
        // các hook đó vô tình chạy trúng luồng khởi động bình thường (đăng ký hotkey, hiện tray icon...).
        VelopackApp.Build().Run();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Chế độ nội bộ dùng bởi CI (xem .github/workflows/build.yml): dựng sẵn các cửa sổ chính với
        // dữ liệu mẫu rồi chụp lại thành PNG để có ảnh demo UI thật (không phải mockup vẽ tay) cho mỗi
        // bản build — không chạy trong bản phát hành thông thường. Xem App.ScreenshotDemo.cs.
        if (e.Args.Length >= 2 && e.Args[0] == "--screenshot-demo")
        {
            _settingsService.Load();
            RunScreenshotDemo(e.Args[1]);
            Shutdown();
            return;
        }

        _settingsService.Load();

        SetupTrayIcon();
        SetupHotkeys();

        _ = CheckForUpdatesAsync(showUpToDateNotice: false);
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "Win ShootX",
            // Render trực tiếp từ IconCamera (Views/Controls/Icons.xaml) thay vì dùng icon hệ thống —
            // giữ đúng quy tắc "toàn bộ icon trong app đều là vector tự vẽ, không icon rời rạc/emoji".
            // Icon .exe hiển thị trong Explorer/Alt-Tab vẫn cần 1 file .ico riêng (xem TODO trong csproj)
            // vì đó là giới hạn của định dạng PE, không render runtime được như tray icon.
            Icon = RenderGeometryToTrayIcon((Geometry)FindResource("IconCamera")),
        };

        var menu = new System.Windows.Controls.ContextMenu();

        AddMenuItem(menu, "Chụp vùng chọn", "IconCrop", (_, _) => StartCapture(CaptureMode.Region));
        AddMenuItem(menu, "Chụp toàn màn hình", "IconFullScreen", (_, _) => StartCapture(CaptureMode.FullScreen));
        AddMenuItem(menu, "Chụp cửa sổ hiện tại", "IconWindowCapture", (_, _) => StartCapture(CaptureMode.Window));
        AddMenuItem(menu, "Chụp cuộn trang", "IconScroll", (_, _) => _ = StartScrollingCaptureAsync());
        AddMenuItem(menu, "Quay màn hình", "IconRecord", (_, _) => StartScreenRecording());
        menu.Items.Add(new System.Windows.Controls.Separator());
        AddMenuItem(menu, "Lịch sử chụp gần đây", "IconHistory", (_, _) => new HistoryWindow(_historyService).Show());
        AddMenuItem(menu, "Cài đặt...", "IconSettings", (_, _) => new SettingsWindow(_settingsService, _conflictingHotkeys).Show());
        AddMenuItem(menu, "Kiểm tra cập nhật...", "IconUpdate", (_, _) => _ = CheckForUpdatesAsync(showUpToDateNotice: true));
        menu.Items.Add(new System.Windows.Controls.Separator());
        AddMenuItem(menu, "Thoát", "IconExit", (_, _) => Shutdown());

        _trayIcon.ContextMenu = menu;
        _trayIcon.TrayLeftMouseUp += (_, _) => StartCapture(CaptureMode.Region);
    }

    private void AddMenuItem(System.Windows.Controls.ContextMenu menu, string header, string iconKey, RoutedEventHandler handler)
    {
        var item = new System.Windows.Controls.MenuItem
        {
            Header = header,
            Icon = IconHelper.Create((Geometry)FindResource(iconKey)),
        };
        item.Click += handler;
        menu.Items.Add(item);
    }

    /// <summary>Render 1 Geometry vector (từ Icons.xaml) thành System.Drawing.Icon để dùng làm tray icon —
    /// tránh phải có sẵn file .ico riêng cho icon khay hệ thống, giữ mọi icon trong app cùng một nguồn vector.</summary>
    private static System.Drawing.Icon RenderGeometryToTrayIcon(Geometry geometry, int size = 32)
    {
        var visual = new DrawingVisual();
        using (var ctx = visual.RenderOpen())
        {
            var bounds = geometry.Bounds;
            double scale = (size - 6) / Math.Max(bounds.Width, bounds.Height);
            var group = new TransformGroup();
            group.Children.Add(new TranslateTransform(-bounds.Left, -bounds.Top));
            group.Children.Add(new ScaleTransform(scale, scale));
            group.Children.Add(new TranslateTransform(3, 3));

            ctx.PushTransform(group);
            // Nét trắng trên nền trong suốt: khớp taskbar tối mặc định của Windows 11. Nếu người dùng
            // dùng theme taskbar sáng, icon sẽ khó nhìn — cân nhắc theo dõi SystemParameters/registry
            // AppsUseLightTheme để đổi màu nét cho phù hợp (TODO, không chặn MVP).
            ctx.DrawGeometry(Brushes.Transparent, new Pen(Brushes.White, 1.8), geometry);
            ctx.Pop();
        }

        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var stream = new System.IO.MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;

        // Lưu ý: GetHicon() cấp 1 HICON GDI cần DestroyIcon để giải phóng hoàn toàn — chấp nhận được ở
        // đây vì chỉ gọi 1 lần lúc khởi động và sống suốt vòng đời app (giải phóng khi process thoát).
        using var bitmap = new System.Drawing.Bitmap(stream);
        return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    private void SetupHotkeys()
    {
        _hotkeyManager = new HotkeyManager();
        var s = _settingsService.Current;

        TryRegister(s.RegionCaptureHotkey, () => StartCapture(CaptureMode.Region));
        TryRegister(s.FullScreenCaptureHotkey, () => StartCapture(CaptureMode.FullScreen));
        TryRegister(s.WindowCaptureHotkey, () => StartCapture(CaptureMode.Window));
        TryRegister(s.ScrollingCaptureHotkey, () => _ = StartScrollingCaptureAsync());
        TryRegister(s.ScreenRecordingHotkey, StartScreenRecording);
    }

    private void TryRegister(string hotkey, Action callback)
    {
        if (_hotkeyManager != null && !_hotkeyManager.Register(hotkey, callback))
        {
            // Hotkey có thể đã bị app khác chiếm dụng — thông báo thay vì fail âm thầm, và ghi nhớ lại
            // để SettingsWindow đánh dấu rõ ô nào đang lỗi (xem _conflictingHotkeys).
            _conflictingHotkeys.Add(hotkey);
            _trayIcon?.ShowBalloonTip("Win ShootX",
                $"Không thể đăng ký phím tắt '{hotkey}' (có thể đã được app khác dùng). Đổi trong Cài đặt.",
                BalloonIcon.Warning);
        }
    }

    private void StartCapture(CaptureMode mode)
    {
        switch (mode)
        {
            case CaptureMode.Region:
                var selector = new RegionSelectorWindow();
                selector.RegionSelected += region =>
                {
                    var result = _captureService.CaptureRegion(region);
                    OnCaptureCompleted(result);
                };
                selector.Show();
                break;

            case CaptureMode.FullScreen:
                OnCaptureCompleted(_captureService.CaptureFullVirtualScreen());
                break;

            case CaptureMode.Window:
                OnCaptureCompleted(_captureService.CaptureForegroundWindow());
                break;
        }
    }

    private void OnCaptureCompleted(CaptureResult result)
    {
        _historyService.Add(result);

        if (_settingsService.Current.PlayShutterSound)
            System.Media.SystemSounds.Exclamation.Play(); // TODO: thay bằng file âm thanh shutter riêng trong Assets

        if (_settingsService.Current.CopyToClipboardAfterCapture)
            ClipboardService.CopyImage(result.Image);

        if (_settingsService.Current.OpenAnnotatorAfterCapture)
        {
            new AnnotationEditorWindow(result.Image).Show();
        }
        else
        {
            new CaptureBarWindow(result, _settingsService).Show();
        }
    }

    /// <summary>Chụp cuộn: yêu cầu người dùng đã đưa chuột vào cửa sổ/trang cần chụp trước khi gọi
    /// (qua hotkey hoặc tray menu) — xem <see cref="ScrollingCaptureService"/> để biết vì sao.</summary>
    private async Task StartScrollingCaptureAsync()
    {
        try
        {
            var image = await _scrollingCaptureService.CaptureAsync();
            if (image == null)
            {
                _trayIcon?.ShowBalloonTip("Win ShootX",
                    "Không tìm thấy cửa sổ hợp lệ dưới con trỏ chuột. Đưa chuột vào trang cần chụp cuộn rồi thử lại.",
                    BalloonIcon.Warning);
                return;
            }

            OnCaptureCompleted(new CaptureResult
            {
                Image = image,
                SourceBounds = new Rect(0, 0, image.PixelWidth, image.PixelHeight),
            });
        }
        catch (Exception ex)
        {
            _trayIcon?.ShowBalloonTip("Win ShootX", $"Chụp cuộn thất bại: {ex.Message}", BalloonIcon.Error);
        }
    }

    /// <summary>Quay màn hình: người dùng kéo chọn vùng quay (tái dùng RegionSelectorWindow), sau đó
    /// thanh điều khiển nổi (<see cref="RecordingControlWindow"/>) hiện ra cho tới khi bấm Dừng.</summary>
    private void StartScreenRecording()
    {
        if (_recordingService.IsRecording)
        {
            _trayIcon?.ShowBalloonTip("Win ShootX", "Đang quay màn hình rồi.", BalloonIcon.Info);
            return;
        }

        var selector = new RegionSelectorWindow();
        selector.RegionSelected += async region =>
        {
            var regionRect = new Rect(region.X, region.Y, region.Width, region.Height);
            try
            {
                await _recordingService.StartAsync(region, new RecordingOptions(), _settingsService.Current.SaveDirectory);
            }
            catch (Exception ex)
            {
                _trayIcon?.ShowBalloonTip("Win ShootX", $"Không bắt đầu quay được: {ex.Message}", BalloonIcon.Error);
                return;
            }

            var controlWindow = new RecordingControlWindow(_recordingService, regionRect);
            controlWindow.RecordingStopped += OnRecordingStopped;
            controlWindow.Show();
        };
        selector.Show();
    }

    private async void OnRecordingStopped(string mp4Path)
    {
        var choice = MessageBox.Show(
            $"Đã lưu video:\n{mp4Path}\n\nXuất thêm bản GIF?",
            "Quay màn hình hoàn tất", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (choice == MessageBoxResult.Yes)
        {
            try
            {
                var gifPath = await _recordingService.ExportGifAsync(mp4Path);
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{gifPath}\"");
                return;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Xuất GIF thất bại: {ex.Message}", "Win ShootX", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{mp4Path}\"");
    }

    /// <summary>
    /// Auto-update: kiểm tra bản mới trên GitHub Releases của repo, tải về nền, rồi tự khởi động lại
    /// để áp dụng. Chạy 1 lần lúc khởi động (âm thầm, không có gì báo lỗi nếu offline/đã mới nhất) và
    /// có thể gọi lại thủ công qua tray menu "Kiểm tra cập nhật...".
    /// </summary>
    private async Task CheckForUpdatesAsync(bool showUpToDateNotice)
    {
        try
        {
            var mgr = new UpdateManager(new GithubSource(UpdateFeedUrl, accessToken: null, prerelease: false));

            // Chạy trực tiếp từ build (F5/Debug) hay bản portable chưa qua installer Velopack: bỏ qua,
            // tránh crash vì không có metadata cài đặt (Velopack chỉ hoạt động sau khi cài qua installer).
            if (!mgr.IsInstalled)
            {
                if (showUpToDateNotice)
                    _trayIcon?.ShowBalloonTip("Win ShootX",
                        "Bản chạy hiện tại không phải bản đã cài qua installer nên không thể tự cập nhật.",
                        BalloonIcon.Info);
                return;
            }

            var newVersion = await mgr.CheckForUpdatesAsync();
            if (newVersion == null)
            {
                if (showUpToDateNotice)
                    _trayIcon?.ShowBalloonTip("Win ShootX", "Bạn đang dùng phiên bản mới nhất.", BalloonIcon.Info);
                return;
            }

            await mgr.DownloadUpdatesAsync(newVersion);

            _trayIcon?.ShowBalloonTip("Win ShootX",
                $"Đã tải bản cập nhật {newVersion.TargetFullRelease.Version} — đang khởi động lại để áp dụng...",
                BalloonIcon.Info);

            // Cho người dùng vài giây để đọc thông báo trước khi tự thoát + cài bản mới + mở lại app.
            await Task.Delay(TimeSpan.FromSeconds(4));
            mgr.ApplyUpdatesAndRestart(newVersion);
        }
        catch
        {
            // Offline, GitHub không truy cập được, hoặc repo chưa có Release nào — bỏ qua âm thầm,
            // không làm gián đoạn luồng dùng app chính (chụp/chú thích vẫn phải hoạt động bình thường).
            if (showUpToDateNotice)
                _trayIcon?.ShowBalloonTip("Win ShootX", "Không thể kiểm tra cập nhật lúc này.", BalloonIcon.Warning);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyManager?.Dispose();
        _trayIcon?.Dispose();
        base.OnExit(e);
    }
}
