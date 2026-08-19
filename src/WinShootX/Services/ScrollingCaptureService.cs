using System.Windows.Media.Imaging;

namespace WinShootX.Services;

/// <summary>
/// [Giai đoạn 2 — chưa triển khai] Chụp cuộn: tự động cuộn cửa sổ đích và ghép nhiều frame
/// thành 1 ảnh dài, tương đương "Scrolling Capture" của CleanShot X.
///
/// Thuật toán đề xuất:
/// 1. Người dùng chọn cửa sổ/vùng cần chụp cuộn (tái dùng RegionSelectorWindow hoặc chọn window).
/// 2. Lặp: chụp 1 frame bằng ScreenCaptureService.CaptureRegion(vùng đã chọn).
/// 3. Gửi lệnh cuộn xuống cửa sổ đích — 2 lựa chọn:
///    a) SendInput mô phỏng phím Page Down / lăn chuột (đơn giản, hoạt động với hầu hết app/trình duyệt).
///    b) SendMessage(hwnd, WM_VSCROLL, ...) cho các control hỗ trợ (chính xác hơn nhưng không phải app nào cũng nhận).
/// 4. Đợi 1 khoảng ngắn (vd 80-150ms) để nội dung render xong rồi chụp frame tiếp theo.
/// 5. Tìm điểm chồng lấn (overlap) giữa frame (n) và frame (n+1): so khớp dải pixel ở mép dưới
///    frame (n) với dải pixel ở mép trên frame (n+1) — dùng hash theo hàng ngang (row hash) để
///    tìm nhanh, tránh so khớp pixel-by-pixel toàn ảnh (chậm).
/// 6. Ghép các frame theo chiều dọc, cắt bỏ phần overlap trùng lặp.
/// 7. Dừng khi: (a) cuộn tới cuối trang (frame mới giống hệt frame trước — không có overlap mới),
///    hoặc (b) đạt giới hạn chiều cao tối đa (ngăn ảnh quá lớn gây tràn bộ nhớ), hoặc (c) người dùng
///    bấm dừng thủ công.
///
/// Điểm khó cần lưu ý khi triển khai:
/// - Sticky header/footer (thanh menu cố định khi cuộn) sẽ bị lặp lại ở mỗi frame nếu không phát hiện
///   và loại trừ riêng — có thể cho người dùng đánh dấu vùng "cố định" cần bỏ qua khi ghép.
/// - Trang có animation/nội dung động (quảng cáo, video) làm thuật toán so khớp overlap sai lệch.
/// - Đa DPI: toạ độ vùng chụp cần nhất quán theo pixel vật lý xuyên suốt vòng lặp.
/// </summary>
public sealed class ScrollingCaptureService
{
    public event Action<int>? ProgressChanged; // số frame đã chụp, dùng để hiện tiến trình cho người dùng

    public Task<BitmapSource> CaptureAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "Scrolling capture thuộc Giai đoạn 2 của roadmap — xem outline thuật toán trong XML doc ở trên.");
    }
}
