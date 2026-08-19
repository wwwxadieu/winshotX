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
│   ├─ ScrollingCaptureService.cs  # STUB Giai đoạn 2 — có outline thuật toán chi tiết trong file
│   ├─ ScreenRecordingService.cs   # STUB Giai đoạn 3 — có outline triển khai chi tiết trong file
│   └─ ShareService.cs             # STUB Giai đoạn 4 — interface IShareProvider, cần chọn backend
├─ Views/
│   ├─ RegionSelectorWindow    # overlay trong suốt full-screen để kéo chọn vùng
│   ├─ CaptureBarWindow        # thanh công cụ nổi sau khi chụp
│   ├─ AnnotationEditorWindow  # canvas chú thích (arrow/rect/ellipse/freehand/text/highlight/blur/step/crop)
│   ├─ ArrowShape.cs           # helper vẽ hình mũi tên (Path geometry)
│   ├─ PinnedWindow            # ảnh ghim always-on-top, kéo-thả, resize, đổi opacity
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
| Chụp cuộn | Stub có outline thuật toán, chưa code |
| Quay màn hình | Stub có outline triển khai, chưa code |
| Chia sẻ cloud | Stub interface, cần chốt backend trước (xem PRD mục 8) |

## CI/CD

`.github/workflows/build.yml`: build + kiểm tra biên dịch trên `windows-latest` ở mỗi push, chạy app
với `--screenshot-demo` (xem `App.ScreenshotDemo.cs`) để chụp ảnh demo UI thật từ chính app rồi đăng
làm artifact. `.github/workflows/release.yml`: khi push tag `vX.Y.Z`, publish self-contained win-x64,
đóng gói bằng `vpk` (Velopack) thành installer + delta/full nupkg, và đăng lên GitHub Releases —
đây cũng chính là nguồn mà auto-update trong app đọc để kiểm tra bản mới.

## Việc cần làm tiếp theo (ưu tiên theo thứ tự)

1. Viết `ScrollingCaptureService`, `ScreenRecordingService`, `ShareService` theo outline đã có
   trong từng file — đây là các khối việc lớn nhất còn lại, mỗi cái nên làm riêng một nhánh/PR.
2. Ký code (code signing) cho installer nếu muốn tránh cảnh báo SmartScreen khi phân phối rộng
   (xem PRD mục 8) — hiện tại release chưa ký, người dùng sẽ thấy cảnh báo Windows Defender SmartScreen.
3. Cân nhắc thêm test tự động (unit test cho `Services/`, vốn không phụ thuộc WPF Window nên dễ test).
