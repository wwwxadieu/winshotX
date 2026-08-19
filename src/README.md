# Win ShootX — Code Scaffold

Scaffold cho Giai đoạn 0 + phần lớn Giai đoạn 1 (MVP) theo PRD (`../PRD.md`). App chụp màn hình
kiểu CleanShot X cho Windows 11, viết bằng C# / .NET 8 WPF.

## Yêu cầu để build

- Windows 10 (2004/build 19041) trở lên hoặc Windows 11.
- Visual Studio 2022 (17.8+) với workload **.NET desktop development**, hoặc .NET 8 SDK + editor bất kỳ.
- Mở `WinShootX.sln`, restore NuGet packages, build.

> Lưu ý: dự án này **không build được trên Linux/macOS** vì WPF và các WinRT API
> (`Windows.Media.Ocr`) chỉ chạy trên Windows. Scaffold được viết trong môi trường Linux nên
> **chưa được build/chạy thử thực tế** — hãy build lần đầu trên máy Windows và báo lại nếu gặp lỗi
> biên dịch (nhiều khả năng chỉ là lỗi nhỏ do gõ tay, không phải lỗi kiến trúc).

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
| Bộ icon vector dùng chung (không emoji) | Đã code — xem PRD mục 7 và `Views/Controls/` |
| Blur/Pixelate trong trình chú thích | Đã code nhưng là "miếng dán" hiệu ứng thị giác (BlurEffect), chưa xử lý pixel thật — xem TODO trong `AnnotationEditorWindow.xaml.cs` |
| Crop | Vẽ được khung crop nhưng **chưa** áp dụng crop thật vào ảnh xuất — cần nối thêm logic cắt `BitmapSource`/canvas trước khi flatten |
| Pin to Screen | Đã code |
| Lịch sử chụp gần đây | Đã code (chỉ trong phiên hiện tại, chưa lưu ổ đĩa) |
| Cài đặt (hotkey, thư mục lưu, tuỳ chọn) | Đã code (riêng "khởi động cùng Windows" chưa nối registry — xem TODO trong `SettingsWindow.xaml.cs`) |
| OCR | Đã code (Windows.Media.Ocr) |
| Chụp cuộn | Stub có outline thuật toán, chưa code |
| Quay màn hình | Stub có outline triển khai, chưa code |
| Chia sẻ cloud | Stub interface, cần chốt backend trước (xem PRD mục 8) |

## Việc cần làm tiếp theo (ưu tiên theo thứ tự)

1. Build thử trên Windows, sửa các lỗi biên dịch nhỏ nếu có (chưa chạy thử thực tế trong phiên này).
2. Thêm `Assets/app.ico` cho file .exe (hiển thị trong Explorer/Alt-Tab) — nên xuất từ cùng bộ vector
   trong `Views/Controls/Icons.xaml` (vd icon `IconCamera`) để đồng nhất hình ảnh; tray icon lúc chạy
   đã tự render từ Geometry đó nên không cần file .ico riêng cho tray.
3. Hoàn thiện Crop: sau khi người dùng kéo khung crop và xác nhận, cắt cả `BaseImage` lẫn mọi
   annotation đã vẽ theo đúng vùng chọn trước khi flatten.
4. Nối "Khởi động cùng Windows" với registry key `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
5. Viết `ScrollingCaptureService`, `ScreenRecordingService`, `ShareService` theo outline đã có
   trong từng file — đây là các khối việc lớn nhất còn lại, mỗi cái nên làm riêng một nhánh/PR.
6. Đóng gói cài đặt: MSIX (khuyến nghị, tích hợp tốt với Windows 11, auto-update qua Store hoặc
   sideload) hoặc Inno Setup installer truyền thống.
