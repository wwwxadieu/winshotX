using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinShootX.Services;

/// <summary>
/// Chụp cuộn: bắt đầu từ cửa sổ đang nằm dưới con trỏ chuột (người dùng đưa chuột vào vùng cần chụp
/// trước khi gọi), tự động cuộn xuống bằng SendInput (mô phỏng lăn chuột thật nên hoạt động với hầu
/// hết app/trình duyệt mà không cần biết cấu trúc control bên trong), chụp từng frame bằng
/// <see cref="ScreenCaptureService"/>, rồi ghép các frame lại thành 1 ảnh dài — loại bỏ phần overlap
/// (chồng lấn) giữa 2 frame liên tiếp bằng cách so khớp hash theo hàng ngang (row hash).
///
/// Giới hạn đã biết (chấp nhận được cho MVP, xem PRD mục 6 Giai đoạn 2):
/// - Sticky header/footer (thanh menu cố định khi cuộn) sẽ bị lặp lại ở mỗi frame vì thuật toán không
///   phân biệt được "nội dung cố định" với "nội dung mới thật sự" nếu chúng giống hệt nhau theo hàng.
/// - Trang có animation/nội dung động (quảng cáo, video) có thể khiến so khớp overlap sai lệch.
/// </summary>
public sealed class ScrollingCaptureService
{
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(POINT point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr hWnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint numInputs, INPUT[] inputs, int inputSize);

    private const uint GA_ROOT = 2;
    private const uint INPUT_MOUSE = 0;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const int WHEEL_DELTA = 120;

    private const int MaxFrames = 60;
    private const int MaxTotalHeightPx = 20000;
    private const int MinOverlapPx = 24;
    private const int SettleDelayMs = 140;

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public MOUSEINPUT Mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int Dx, Dy;
        public int MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    private readonly ScreenCaptureService _captureService = new();

    /// <summary>Số frame đã chụp — dùng để hiện tiến trình cho người dùng.</summary>
    public event Action<int>? ProgressChanged;

    /// <summary>Trả về ảnh đã ghép, hoặc null nếu không tìm được cửa sổ hợp lệ dưới con trỏ chuột.</summary>
    public async Task<BitmapSource?> CaptureAsync(CancellationToken cancellationToken = default)
    {
        var region = GetTargetWindowRegion();
        if (region is not { Width: > 0, Height: > 0 } targetRegion)
            return null;

        var segments = new List<Segment>();
        ulong[]? previousRowHashes = null;
        PixelFormat? format = null;
        int totalHeight = 0;

        for (int frameIndex = 0; frameIndex < MaxFrames; frameIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var frame = _captureService.CaptureRegion(targetRegion).Image;
            format ??= frame.Format;
            var (pixels, stride) = ToPixelBuffer(frame);
            var rowHashes = ComputeRowHashes(pixels, frame.PixelHeight, stride);

            int skipTopRows = 0;
            if (previousRowHashes != null)
            {
                int overlap = FindOverlap(previousRowHashes, rowHashes);
                if (overlap >= frame.PixelHeight - MinOverlapPx)
                {
                    // Frame mới gần như trùng hệt frame trước — đã cuộn tới cuối trang, dừng lại.
                    ProgressChanged?.Invoke(frameIndex + 1);
                    break;
                }
                skipTopRows = overlap;
            }

            int keptHeight = frame.PixelHeight - skipTopRows;
            if (keptHeight > 0)
            {
                segments.Add(new Segment(pixels, stride, skipTopRows, keptHeight));
                totalHeight += keptHeight;
            }

            ProgressChanged?.Invoke(frameIndex + 1);
            previousRowHashes = rowHashes;

            if (totalHeight >= MaxTotalHeightPx) break;

            ScrollDown(notches: 3);
            await Task.Delay(SettleDelayMs, cancellationToken);
        }

        return segments.Count == 0 || format == null ? null : Stitch(segments, targetRegion.Width, totalHeight, format.Value);
    }

    private static Int32Rect? GetTargetWindowRegion()
    {
        if (!GetCursorPos(out var cursor)) return null;
        var hwnd = WindowFromPoint(cursor);
        if (hwnd == IntPtr.Zero) return null;
        hwnd = GetAncestor(hwnd, GA_ROOT);
        if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out var rect)) return null;

        return new Int32Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    private static void ScrollDown(int notches)
    {
        var input = new INPUT
        {
            Type = INPUT_MOUSE,
            Mi = new MOUSEINPUT { MouseData = -notches * WHEEL_DELTA, Flags = MOUSEEVENTF_WHEEL },
        };
        SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>());
    }

    private static (byte[] Pixels, int Stride) ToPixelBuffer(BitmapSource frame)
    {
        int stride = (frame.PixelWidth * frame.Format.BitsPerPixel + 7) / 8;
        var pixels = new byte[stride * frame.PixelHeight];
        frame.CopyPixels(pixels, stride, 0);
        return (pixels, stride);
    }

    private static ulong[] ComputeRowHashes(byte[] pixels, int height, int stride)
    {
        var hashes = new ulong[height];
        for (int y = 0; y < height; y++)
        {
            ulong hash = 14695981039346656037; // FNV-1a offset basis
            int rowStart = y * stride;
            for (int x = 0; x < stride; x++)
            {
                hash ^= pixels[rowStart + x];
                hash *= 1099511628211; // FNV-1a prime
            }
            hashes[y] = hash;
        }
        return hashes;
    }

    /// <summary>Tìm số hàng lớn nhất mà đuôi của <paramref name="previousRows"/> khớp với đầu của
    /// <paramref name="newRows"/> — đây chính là phần nội dung bị lặp lại do cuộn chưa hết 1 khung
    /// hình. Quét từ overlap lớn nhất có thể xuống dần để luôn nhận kết quả đúng nhất (giá trị lớn
    /// nhất thoả) thay vì dừng ở lần khớp tình cờ đầu tiên với overlap nhỏ.</summary>
    private static int FindOverlap(ulong[] previousRows, ulong[] newRows)
    {
        int maxOverlap = Math.Min(previousRows.Length, newRows.Length);
        for (int overlap = maxOverlap; overlap >= MinOverlapPx; overlap--)
        {
            int previousStart = previousRows.Length - overlap;
            bool match = true;
            for (int i = 0; i < overlap; i++)
            {
                if (previousRows[previousStart + i] != newRows[i]) { match = false; break; }
            }
            if (match) return overlap;
        }
        return 0;
    }

    private static BitmapSource Stitch(List<Segment> segments, int width, int totalHeight, PixelFormat format)
    {
        var result = new WriteableBitmap(width, totalHeight, 96, 96, format, null);
        int destY = 0;
        foreach (var segment in segments)
        {
            var rect = new Int32Rect(0, destY, width, segment.Height);
            int sourceOffset = segment.SkipTopRows * segment.Stride;
            int length = segment.Height * segment.Stride;
            var slice = new byte[length];
            Buffer.BlockCopy(segment.Pixels, sourceOffset, slice, 0, length);
            result.WritePixels(rect, slice, segment.Stride, 0);
            destY += segment.Height;
        }
        result.Freeze();
        return result;
    }

    private readonly record struct Segment(byte[] Pixels, int Stride, int SkipTopRows, int Height);
}
