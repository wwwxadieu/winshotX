# Win ShootX — Code Scaffold

Scaffold cho Giai đoạn 0 + phần lớn Giai đoạn 1 (MVP) theo PRD (`../PRD.md`). App chụp màn hình
kiểu CleanShot X cho Windows 11, viết bằng C# / .NET 8 WPF.

## Yêu cầu để build

- Windows 10 (2004/build 19041) trở lên hoặc Windows 11.
- Visual Studio 2022 (17.8+) với workload **.NET desktop development**, hoặc .NET 8 SDK + editor bất kỳ.
- Mở `WinShootX.sln`, restore NuGet packages, build.

> Lưu ý: **chạy** app này (hotkey thật, WinRT OCR, DPI thật...) chỉ hoạt động trên Windows. Việc
> **build** thì kiểm chứng được cả trên Linux/macOS bằng cách restore/build với
> `/p:EnableWindowsTargeting=true` (SDK .NET 8 tải sẵn reference assembly của WPF/WinRT qua NuGet) —
> đây chính là cách CI (`.github/workflows/build.yml`) xác nhận code biên dịch được trước khi build
> thật (và chụp ảnh demo UI) trên `windows-latest`.

## Chạy thử nhanh

1. Mở solution trong Visual Studio, nhấn F5 (hoặc `dotnet run` trong thư mục `WinShootX/` bằng
   Developer PowerShell trên Windows).
2. App không có cửa sổ chính — chạy nền dưới icon khay hệ thống (system tray).
3. Click trái icon tray hoặc bấm `Ctrl+Shift+4` để chụp vùng chọn; kéo chuột để chọn vùng, thả ra
   để chụp. `Esc` để huỷ.
4. Sau khi chụp: nếu bật "Tự động mở trình chú thích" (mặc định), cửa sổ Annotation Editor mở ngay;
   nếu không, sẽ hiện thanh công cụ nổi gần vùng vừa chụp.

## Cấu trúc thư mục

```
WinShootX/
├─ App.xaml(.cs)              # entry point: tray icon, đăng ký hotkey, điều phối luồng chụp
├─ app.manifest                # khai báo Per-Monitor V2 DPI awareness (bắt buộc cho đa màn hình)
├─ Models/
│   ├─ CaptureMode.cs          # enum CaptureMode + class CaptureResult
│   ├─ AnnotationTool.cs       # enum AnnotationTool + class AnnotationItem
│   └─ CaptureSettings.cs      # cài đặt người dùng (save qua SettingsService)
├─ Services/
│   ├─ HotkeyManager.cs        # RegisterHotKey P/Invoke, parse chuỗi "Ctrl+Shift+4"
│   ├─ ScreenCaptureService.cs # BitBlt (region/full screen) + PrintWindow (cửa sổ)
│   ├─ ClipboardService.cs
│   ├─ FileService.cs          # lưu PNG
│   ├─ SettingsService.cs      # load/save JSON trong %AppData%\WinShootX
│   ├─ HistoryService.cs       # danh sách chụp gần đây (in-memory, xem TODO để lưu bền vững)
│   ├─ OcrService.cs           # ĐÃ triển khai — Windows.Media.Ocr
│   ├─ PixelEffects.cs         # box blur / mosaic pixel thật, dùng bởi AnnotationEditorWindow
│   ├─ ScrollingCaptureService.cs  # ĐÃ triển khai — cuộn qua SendInput + ghép frame bằng row-hash overlap
│   ├─ ScreenRecordingService.cs   # ĐÃ triển khai — quay MP4/GIF qua ffmpeg subprocess (gdigrab)
│   └─ ShareService.cs             # STUB Giai đoạn 4 — interface IShareProvider, cần chọn backend
├─ Views/
│   ├─ RegionSelectorWindow    # overlay trong suốt full-screen để kéo chọn vùng (dùng chung cho chụp lẫn quay)
│   ├─ CaptureBarWindow        # thanh công cụ nổi sau khi chụp
│   ├─ AnnotationEditorWindow  # canvas chú thích (arrow/rect/ellipse/freehand/text/highlight/blur/step/crop)
│   ├─ ArrowShape.cs           # helper vẽ hình mũi tên (Path geometry)
│   ├─ PinnedWindow            # ảnh ghim always-on-top, kéo-thả, resize, đổi opacity
│   ├─ RecordingControlWindow  # thanh nổi hiện lúc quay màn hình (thời gian + nút Dừng)
│   ├─ SettingsWindow          # form cài đặt
│   ├─ HistoryWindow           # danh sách ảnh chụp gần đây trong phiên
│   └─ Controls/
│       ├─ Icons.xaml          # NGUỒN DUY NHẤT cho toàn bộ icon (Geometry vector) — không emoji, xem PRD mục 7
│       ├─ IconControls.xaml   # style mặc định cho IconButton/IconToggleButton
│       ├─ IconButton.cs / IconToggleButton.cs  # custom control icon + nhãn chữ
│       └─ IconHelper.cs       # dựng Path icon cho MenuItem.Icon (tray menu, context menu)
└─ Assets/                     # (trống) — thêm app.ico + shutter.wav vào đây, xem TODO trong code
```

## Trạng thái tính năng

| Tính năng | Trạng thái |
|---|---|
| Chụp vùng chọn / toàn màn hình / cửa sổ | Đã code (BitBlt/PrintWindow) |
| Hotkey toàn cục | Đã code |
| Thanh công cụ nổi sau khi chụp | Đã code |
| Trình chú thích (arrow/rect/ellipse/freehand/text/highlight/step) | Đã code |
| Bộ icon vector dùng chung (không emoji) | Đã code — xem PRD mục 7 và `Views/Controls/`. `Assets/app.ico` xuất từ cùng bộ vector (`IconCamera`) cho icon .exe |
| Blur/Pixelate trong trình chú thích | Đã code — xử lý pixel thật (`Services/PixelEffects.cs`: box blur / mosaic) khi flatten ảnh xuất, không còn là hiệu ứng thị giác tạm |
| Crop | Đã code — áp dụng crop thật vào cả `BaseImage` lẫn mọi annotation khi flatten (xem `RenderFlattened` trong `AnnotationEditorWindow.xaml.cs`) |
| Pin to Screen | Đã code |
| Lịch sử chụp gần đây | Đã code (chỉ trong phiên hiện tại, chưa lưu ổ đĩa) |
| Cài đặt (hotkey, thư mục lưu, tuỳ chọn, khởi động cùng Windows) | Đã code — "Khởi động cùng Windows" nối với `HKCU\...\Run` |
| OCR | Đã code (Windows.Media.Ocr) |
| Auto-update | Đã code — `Velopack` kiểm tra/tải/áp dụng bản mới từ GitHub Releases của repo lúc khởi động + tray menu "Kiểm tra cập nhật..." |
| Chụp cuộn | Đã code (`ScrollingCaptureService`) — đưa chuột vào cửa sổ/trang cần chụp rồi bấm `Ctrl+Shift+6` hoặc tray menu "Chụp cuộn trang". Xem giới hạn đã biết (sticky header, nội dung động) trong doc comment của service |
| Quay màn hình | Đã code (`ScreenRecordingService`) — `Ctrl+Shift+7` hoặc tray menu "Quay màn hình" để chọn vùng và bắt đầu quay, xuất MP4 (qua ffmpeg gdigrab) + tuỳ chọn xuất thêm GIF sau khi dừng. **Cần cài `ffmpeg.exe` riêng** (không bundle sẵn trong installer — xem TODO bên dưới) và đặt trong PATH hoặc cùng thư mục WinShootX.exe. Chưa hỗ trợ thu âm thanh (báo lỗi rõ nếu bật `RecordingOptions.CaptureSystemAudio/CaptureMicrophone`) |
| Chia sẻ cloud | Stub interface, cần chốt backend trước (xem PRD mục 8) — bỏ qua ở giai đoạn này theo quyết định của chủ dự án |

## CI/CD

`.github/workflows/build.yml`: build + kiểm tra biên dịch trên `windows-latest` ở mỗi push, chạy app
với `--screenshot-demo` (xem `App.ScreenshotDemo.cs`) để chụp ảnh demo UI thật từ chính app rồi đăng
làm artifact. `.github/workflows/release.yml`: khi push tag `vX.Y.Z` (hoặc chạy thủ công qua
workflow_dispatch), publish self-contained win-x64, đóng gói bằng `vpk` (Velopack) thành installer +
delta/full nupkg, và đăng lên GitHub Releases — đây cũng chính là nguồn mà auto-update trong app đọc
để kiểm tra bản mới.

**Ký code (code signing):** `release.yml` có sẵn bước ký cho installer, nhưng chỉ chạy khi đã cấu
hình 2 secret `WINDOWS_PFX_BASE64` (nội dung file `.pfx` encode base64) và `WINDOWS_PFX_PASSWORD`
trong Settings → Secrets and variables → Actions của repo trên GitHub. Chưa cấu hình thì bước này tự
bỏ qua (release ra bình thường, chưa ký — Windows SmartScreen sẽ cảnh báo khi cài cho tới khi có
cert thật). Dùng `--signParams` của `vpk` (gọi `signtool.exe` nội bộ). *Lưu ý: chưa có cert thật để
kiểm thử end-to-end trong phiên làm việc tạo ra bước này — cần xác nhận lại ở lần ký đầu tiên.*
Chưa có chứng chỉ? Cân nhắc Azure Trusted Signing (~$9.99/tháng, không cần mua EV cert đắt đỏ,
SmartScreen tin cậy nhanh hơn OV cert thường) — `vpk pack` cũng hỗ trợ thẳng qua flag
`--azureTrustedSignFile`.

## Việc cần làm tiếp theo (ưu tiên theo thứ tự)

1. Cung cấp `WINDOWS_PFX_BASE64` + `WINDOWS_PFX_PASSWORD` (hoặc chuyển sang Azure Trusted Signing)
   để bật ký code thật cho release — xem mục CI/CD ở trên.
2. Bundle sẵn `ffmpeg.exe` trong installer (hiện người dùng phải tự cài) — cân nhắc dùng
   `Xabe.FFmpeg.Downloader` để tự tải ffmpeg lúc cài đặt/lần chạy đầu, hoặc đóng gói trực tiếp binary
   đã kiểm định vào `Assets/` nếu chấp nhận tăng dung lượng installer.
3. `ShareService` (Cloud Sharing) — cần chốt backend trước khi code (xem PRD mục 8).
4. Cân nhắc thêm test tự động (unit test cho `Services/`, vốn không phụ thuộc WPF Window nên dễ test)
   — đặc biệt hữu ích cho `PixelEffects` và thuật toán row-hash overlap trong `ScrollingCaptureService`.
