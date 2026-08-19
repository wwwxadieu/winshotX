namespace WinShootX.Services;

/// <summary>
/// [Giai đoạn 3 — chưa triển khai] Quay màn hình xuất MP4/GIF, tương đương tính năng quay của CleanShot X.
///
/// Hướng triển khai đề xuất:
/// 1. Dùng WinRT API `Windows.Graphics.Capture` (GraphicsCaptureItem + Direct3D11CaptureFramePool) để
///    lấy frame liên tục từ 1 màn hình hoặc 1 cửa sổ — API này tăng tốc bằng GPU, hiệu năng tốt hơn
///    nhiều so với BitBlt lặp lại (BitBlt phù hợp chụp ảnh tĩnh, không phù hợp quay video 30-60fps).
///    Cần người dùng cấp quyền chọn nguồn quay qua `GraphicsCapturePicker` (giống Windows Game Bar).
/// 2. Encode frame thành video bằng Media Foundation (`Windows.Media.Transcoding` hoặc
///    `MediaFrameSourceGroup` + `MediaComposition`), xuất H.264/MP4. Đây là phần phức tạp nhất —
///    cân nhắc dùng thư viện wrapper (vd FFmpeg.AutoGen hoặc gọi ffmpeg.exe như subprocess) nếu
///    Media Foundation API thuần quá cồng kềnh để triển khai đúng deadline.
/// 3. Xuất GIF: downsample xuống ~10-15fps, áp dụng palette quantization (giảm số màu, vd dùng
///    thuật toán octree hoặc median-cut) để giữ dung lượng file hợp lý — GIF không nén tốt như video.
/// 4. UI: cửa sổ chọn vùng quay (tái dùng RegionSelectorWindow), thanh điều khiển nổi khi đang quay
///    (dừng/tạm dừng/huỷ, đồng hồ đếm thời gian, tuỳ chọn có/không thu tiếng hệ thống + micro).
/// 5. Thu âm thanh (tuỳ chọn): `Windows.Media.Capture.MediaCapture` cho audio loopback (âm thanh hệ
///    thống) — cần bật `AudioCategory.Other` và loopback capture, khác API với capture màn hình.
///
/// Rủi ro: Windows Graphics Capture yêu cầu Windows 10 version 2004 (build 19041) trở lên — đã khớp
/// với TargetFramework hiện tại của project, nhưng cần kiểm tra máy người dùng cuối đạt tối thiểu bản này.
/// </summary>
public sealed class ScreenRecordingService
{
    public bool IsRecording { get; private set; }

    public event Action<TimeSpan>? RecordingTimeChanged;

    public Task StartAsync(RecordingOptions options, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException(
            "Quay màn hình thuộc Giai đoạn 3 của roadmap — xem outline triển khai trong XML doc ở trên.");
    }

    public Task<string> StopAsync()
    {
        throw new NotImplementedException();
    }
}

public sealed class RecordingOptions
{
    public bool CaptureSystemAudio { get; set; }
    public bool CaptureMicrophone { get; set; }
    public bool ExportAsGif { get; set; }
}
