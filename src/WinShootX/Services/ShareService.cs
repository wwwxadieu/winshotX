using System.Windows.Media.Imaging;

namespace WinShootX.Services;

/// <summary>
/// [Giai đoạn 4 — cần chốt backend trước khi code] Upload ảnh/video vừa chụp lên đâu đó và trả về
/// link chia sẻ, tương đương "Cloud Sharing" của CleanShot X (bản gốc dùng dịch vụ riêng của họ).
///
/// Vì đây là quyết định sản phẩm (không chỉ kỹ thuật) — chi phí vận hành, quyền riêng tư ảnh người
/// dùng, giới hạn băng thông/lưu trữ — thiết kế dưới dạng interface để cắm nhiều backend khác nhau
/// mà không phải sửa lại UI (CaptureBarWindow chỉ cần gọi IShareProvider.UploadAsync).
///
/// Vài lựa chọn backend khả thi:
///  - Tự host: 1 API endpoint đơn giản (S3-compatible storage như Cloudflare R2/Backblaze B2 — rẻ,
///    không phí egress hoặc rất thấp) + DB lưu metadata (ngày hết hạn, lượt xem).
///  - Dùng thẳng dịch vụ có sẵn: Imgur API, hoặc lưu vào thư mục OneDrive/Google Drive của người
///    dùng rồi lấy link chia sẻ qua API của dịch vụ đó (không cần tự vận hành server).
/// </summary>
public interface IShareProvider
{
    Task<Uri> UploadAsync(BitmapSource image, CancellationToken cancellationToken = default);
}

/// <summary>Fallback hiện tại: "chia sẻ" bằng cách lưu file cục bộ — dùng khi chưa chọn backend cloud.</summary>
public sealed class LocalFolderShareProvider : IShareProvider
{
    private readonly string _directory;

    public LocalFolderShareProvider(string directory) => _directory = directory;

    public Task<Uri> UploadAsync(BitmapSource image, CancellationToken cancellationToken = default)
    {
        var path = FileService.SavePng(image, _directory);
        return Task.FromResult(new Uri(path));
    }
}
