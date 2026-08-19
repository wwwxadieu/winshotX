# Win ShootX — Tài liệu đặc tả sản phẩm (PRD)

**Phiên bản:** 0.1 (draft)
**Ngày:** 2026-08-18
**Mục tiêu:** Xây dựng ứng dụng chụp/quay màn hình cho Windows 11, lấy cảm hứng từ CleanShot X (macOS), tập trung vào trải nghiệm nhanh, đẹp, và bộ công cụ chú thích mạnh.

## 1. Tổng quan

CleanShot X nổi tiếng nhờ ba thứ: (1) quy trình chụp cực nhanh với thanh công cụ nổi xuất hiện ngay sau khi chụp, (2) công cụ chú thích tinh tế mà không rối, (3) các tính năng "chất" như chụp cuộn, ảnh ghim nổi trên desktop, và chia sẻ link tức thì. Win ShootX mang lại trải nghiệm tương đương trên Windows 11, dùng các API capture gốc của Windows để đảm bảo hiệu năng và độ nét.

## 2. Đối tượng người dùng

Người dùng cá nhân/kỹ thuật cần chụp màn hình thường xuyên để làm tài liệu, báo lỗi, hướng dẫn sử dụng, chia sẻ nhanh trong nhóm làm việc.

## 3. Danh sách tính năng (map với CleanShot X)

| # | Tính năng | Tương đương CleanShot X | Độ ưu tiên |
|---|---|---|---|
| 1 | Chụp vùng chọn / toàn màn hình / cửa sổ, qua hotkey toàn cục | Capture Area/Screen/Window | MVP |
| 2 | Thanh công cụ nổi sau khi chụp (copy, save, annotate, pin, OCR, share) | Floating capture bar | MVP |
| 3 | Trình chú thích: mũi tên, chữ nhật, oval, bút vẽ tay, text, highlight, số thứ tự, làm mờ/pixelate vùng nhạy cảm, crop, undo/redo | Annotation Editor | MVP |
| 4 | Ảnh ghim nổi trên desktop (luôn ở trên cùng, resize, chỉnh opacity) | Pin to Screen | MVP |
| 5 | Lịch sử chụp gần đây (panel) | History | MVP |
| 6 | Chụp cuộn trang dài (web/tài liệu), tự ghép ảnh | Scrolling Capture | Giai đoạn 2 |
| 7 | Quay màn hình xuất MP4/GIF | Screen Recording | Giai đoạn 3 |
| 8 | Nhận diện văn bản trong ảnh, copy text | OCR | Giai đoạn 4 |
| 9 | Upload nhanh + copy link chia sẻ | Cloud Sharing | Giai đoạn 4 |
| 10 | Cài đặt: hotkey tùy chỉnh, nơi lưu, khởi động cùng Windows | Preferences | MVP |

## 4. Lựa chọn công nghệ

**Đề xuất: C# / .NET 8 + WPF**, kết hợp P/Invoke Win32 (GDI+/`BitBlt`, `PrintWindow`) cho phần capture lõi, và WinRT API (`Windows.Media.Ocr`, `Windows.Graphics.Capture`) cho OCR và quay màn hình.

Lý do:
- Cần tích hợp sâu với Windows: hotkey toàn cục, tray icon, overlay trong suốt full-screen, always-on-top window, truy cập pixel màn hình hiệu năng cao. WPF cho phép custom UI mượt (thanh công cụ nổi, canvas chú thích) mà vẫn P/Invoke Win32 dễ dàng — đây chính là cách ShareX và Greenshot (2 app capture phổ biến nhất trên Windows) đã làm.
- WinUI 3 có Fluent Design đẹp hơn nhưng hệ sinh thái/tooling cho các tác vụ low-level (đa màn hình, DPI scaling phức tạp, legacy Win32 interop) còn non hơn WPF, dễ vướng khi cần control tinh vi.
- Electron/web stack cho UI đẹp nhanh nhưng: (a) nặng máy hơn đáng kể cho một app chạy nền/tray liên tục, (b) global hotkey + BitBlt-level capture + always-on-top overlay phải đi qua native module riêng, mất lợi thế "viết 1 lần" mà vẫn phải code native.
- .NET 8 hỗ trợ target `net8.0-windows10.0.19041.0` để gọi thẳng WinRT API cần cho quay màn hình (Windows Graphics Capture) và OCR (Windows.Media.Ocr) mà không cần thư viện ngoài.

## 5. Kiến trúc tổng quan

```
WinShootX (WPF app, chạy nền dưới tray icon)
├─ Services/          # logic lõi, không phụ thuộc UI
│   ├─ HotkeyManager          — đăng ký hotkey toàn cục (RegisterHotKey)
│   ├─ ScreenCaptureService   — chụp pixel màn hình (BitBlt/PrintWindow), đa màn hình + DPI
│   ├─ ScrollingCaptureService— [Giai đoạn 2] tự cuộn + ghép ảnh
│   ├─ ScreenRecordingService — [Giai đoạn 3] Windows.Graphics.Capture + Media Foundation encode
│   ├─ OcrService             — [Giai đoạn 4] Windows.Media.Ocr
│   ├─ ShareService           — [Giai đoạn 4] upload + copy link (interface, cắm nhiều backend)
│   └─ ClipboardService, SettingsService
├─ Views/             # cửa sổ UI
│   ├─ RegionSelectorWindow   — overlay trong suốt full-screen để kéo chọn vùng
│   ├─ CaptureBarWindow       — thanh công cụ nổi sau khi chụp
│   ├─ AnnotationEditorWindow — canvas chú thích
│   ├─ PinnedWindow           — ảnh ghim always-on-top
│   └─ SettingsWindow
├─ Models/            # CaptureSettings, AnnotationItem, enum các loại tool
└─ App.xaml(.cs)      # khởi tạo tray icon, đăng ký hotkey, entry point
```

Nguyên tắc: `Services/` không tham chiếu WPF Window trực tiếp (dễ test), `Views/` gọi `Services/` qua interface.

## 6. Lộ trình phát triển

**Giai đoạn 0 — Nền tảng (tuần 1)**
Solution scaffold, tray icon, đăng ký hotkey, quyền chạy nền/khởi động cùng Windows.

**Giai đoạn 1 — MVP (tuần 2-4)**
Chụp vùng/toàn màn hình/cửa sổ → thanh công cụ nổi → trình chú thích cơ bản (mũi tên, text, rect, highlight, blur, crop) → copy/save/pin → history panel → settings cơ bản.
*Đây là phần đã được code scaffold trong phiên làm việc này.*

**Giai đoạn 2 — Chụp cuộn (tuần 5-6)**
Thuật toán: gửi lệnh cuộn (SendMessage/SendInput) tới cửa sổ đích theo từng bước nhỏ hơn 1 màn hình, chụp từng frame, dò điểm chồng lấn (overlap) giữa 2 frame liên tiếp bằng so khớp pixel/hash theo hàng ngang, ghép dọc loại bỏ phần trùng.

**Giai đoạn 3 — Quay màn hình (tuần 7-8)**
Dùng `Windows.Graphics.Capture` để lấy frame liên tục hiệu năng cao (GPU-accelerated), encode bằng Media Foundation ra MP4; xuất GIF bằng downsample frame rate + palette quantization.

**Giai đoạn 4 — OCR, chia sẻ cloud, ghim nâng cao (tuần 9-10)**
`Windows.Media.Ocr` cho nhận diện văn bản offline nhiều ngôn ngữ. Chia sẻ: thiết kế `IShareProvider` để cắm nhiều backend (S3/Azure Blob/API riêng của người dùng) vì "cloud" ở đây phụ thuộc dịch vụ mà chủ dự án chọn — cần quyết định trước khi code phần này.

**Giai đoạn 5 — Hoàn thiện (tuần 11+)**
Theming (light/dark), auto-update, đóng gói cài đặt (MSIX hoặc Inno Setup installer), tối ưu đa màn hình/DPI hỗn hợp.

## 7. Quy tắc thiết kế (áp dụng cho mọi tính năng, kể cả thêm sau này)

**Không dùng emoji hoặc ký tự Unicode tượng hình (✏️ 📌 ↗ ✕ ⚙️ ...) làm icon ở bất kỳ đâu trong
ứng dụng** — nút bấm, thanh công cụ, menu (bao gồm tray menu và context menu chuột phải), dialog,
thông báo. Đây là quy tắc đứng (standing rule): áp dụng cho toàn bộ tính năng đã có lẫn mọi tính
năng được thêm ở các giai đoạn sau (chụp cuộn, quay màn hình, chia sẻ cloud, v.v.) — không phải
ngoại lệ một lần cho MVP.

Thay vào đó, toàn bộ icon dùng **vector Geometry tự vẽ**, quản lý tập trung:

- `Views/Controls/Icons.xaml` — nơi DUY NHẤT định nghĩa icon (dạng `Geometry`/`GeometryGroup`, lưới
  24×24, phong cách nét đơn/line-icon). Icon mới cho tính năng mới luôn được thêm vào đây, không
  hardcode Path rải rác trong từng file.
- `Views/Controls/IconButton.cs`, `IconToggleButton.cs` — custom control (kế thừa Button/ToggleButton)
  với `IconData`/`IconSize`, có style mặc định (icon + nhãn chữ, hover, trạng thái chọn) định nghĩa
  trong `IconControls.xaml`, áp dụng tự động toàn app. Dùng 2 control này cho mọi nút bấm có icon
  thay vì `Button` thường.
- `Views/Controls/IconHelper.cs` — dựng `Path` icon cho những chỗ nhận `UIElement` trực tiếp thay vì
  qua control riêng (vd `MenuItem.Icon` trong context menu/tray menu).
- Tray icon của app cũng render trực tiếp từ Geometry (`IconCamera`) lúc khởi động thay vì dùng icon
  hệ thống mặc định hay file .ico rời — xem `RenderGeometryToTrayIcon` trong `App.xaml.cs`. Icon file
  .exe hiển thị trong Explorer/Alt-Tab vẫn cần 1 file `.ico` tĩnh riêng (giới hạn định dạng PE, không
  render runtime được) — nên xuất từ cùng bộ vector gốc để đồng nhất hình ảnh thương hiệu.

Khi thêm tính năng mới cần icon: (1) vẽ Geometry mới trong `Icons.xaml`, (2) dùng qua
`IconButton`/`IconToggleButton`/`IconHelper` — không thêm emoji/text-symbol làm giải pháp tạm.

## 8. Rủi ro / điểm cần quyết định thêm

- **Chia sẻ cloud**: cần chọn backend lưu trữ (dịch vụ của bên thứ 3 hay tự host) — ảnh hưởng tới chi phí vận hành và điều khoản riêng tư.
- **Đa màn hình + DPI hỗn hợp**: Windows cho phép mỗi màn hình có tỷ lệ scale khác nhau; overlay chọn vùng phải tính đúng tọa độ theo từng màn hình để tránh lệch/mờ.
- **Quyền Windows Graphics Capture**: từ Windows 10 2004+ trở lên mới hỗ trợ đầy đủ; cần kiểm tra fallback cho máy cũ hơn nếu cần.
- **Đóng gói & chữ ký code**: nếu muốn tránh cảnh báo SmartScreen khi phân phối, cần chứng chỉ ký code (có chi phí).

## 9. Trạng thái phiên làm việc này

Đã tạo scaffold code cho Giai đoạn 0 + phần lớn Giai đoạn 1 (chi tiết trong README của project code), cùng interface/outline có tài liệu cho Giai đoạn 2-4 để dễ tiếp tục. Bộ icon vector dùng chung (`Views/Controls/Icons.xaml`) đã thay thế toàn bộ emoji/ký tự Unicode từng dùng tạm ở bản nháp đầu.
