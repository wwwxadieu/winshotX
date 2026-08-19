using System.Windows;
using System.Windows.Media.Imaging;

namespace WinShootX.Services;

/// <summary>
/// Xử lý pixel thật (không phải hiệu ứng thị giác WPF) cho vùng che nhạy cảm trong trình chú thích —
/// dùng khi flatten ảnh xuất ra (copy/save) để đảm bảo nội dung gốc không thể khôi phục lại.
/// Chỉ thao tác trên vùng <see cref="Int32Rect"/> được chỉ định, không đụng phần còn lại của ảnh.
/// </summary>
public static class PixelEffects
{
    public static void Pixelate(WriteableBitmap bitmap, Int32Rect region, int blockSize)
    {
        region = Clamp(region, bitmap.PixelWidth, bitmap.PixelHeight);
        if (region.Width <= 0 || region.Height <= 0) return;

        int w = region.Width, h = region.Height, stride = w * 4;
        var pixels = new byte[h * stride];
        bitmap.CopyPixels(region, pixels, stride, 0);

        for (int by = 0; by < h; by += blockSize)
        {
            int bh = Math.Min(blockSize, h - by);
            for (int bx = 0; bx < w; bx += blockSize)
            {
                int bw = Math.Min(blockSize, w - bx);

                long sumB = 0, sumG = 0, sumR = 0, sumA = 0;
                int count = bw * bh;
                for (int y = by; y < by + bh; y++)
                {
                    int rowStart = y * stride + bx * 4;
                    for (int x = 0; x < bw; x++)
                    {
                        int idx = rowStart + x * 4;
                        sumB += pixels[idx]; sumG += pixels[idx + 1]; sumR += pixels[idx + 2]; sumA += pixels[idx + 3];
                    }
                }

                byte avgB = (byte)(sumB / count), avgG = (byte)(sumG / count), avgR = (byte)(sumR / count), avgA = (byte)(sumA / count);
                for (int y = by; y < by + bh; y++)
                {
                    int rowStart = y * stride + bx * 4;
                    for (int x = 0; x < bw; x++)
                    {
                        int idx = rowStart + x * 4;
                        pixels[idx] = avgB; pixels[idx + 1] = avgG; pixels[idx + 2] = avgR; pixels[idx + 3] = avgA;
                    }
                }
            }
        }

        bitmap.WritePixels(region, pixels, stride, 0);
    }

    public static void BoxBlur(WriteableBitmap bitmap, Int32Rect region, int radius)
    {
        region = Clamp(region, bitmap.PixelWidth, bitmap.PixelHeight);
        if (region.Width <= 0 || region.Height <= 0 || radius <= 0) return;

        int w = region.Width, h = region.Height, stride = w * 4;
        var src = new byte[h * stride];
        bitmap.CopyPixels(region, src, stride, 0);
        var dst = new byte[src.Length];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                long sumB = 0, sumG = 0, sumR = 0, sumA = 0;
                int count = 0;
                for (int dy = -radius; dy <= radius; dy++)
                {
                    int sy = y + dy;
                    if (sy < 0 || sy >= h) continue;
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int sx = x + dx;
                        if (sx < 0 || sx >= w) continue;
                        int idx = sy * stride + sx * 4;
                        sumB += src[idx]; sumG += src[idx + 1]; sumR += src[idx + 2]; sumA += src[idx + 3];
                        count++;
                    }
                }

                int outIdx = y * stride + x * 4;
                dst[outIdx] = (byte)(sumB / count);
                dst[outIdx + 1] = (byte)(sumG / count);
                dst[outIdx + 2] = (byte)(sumR / count);
                dst[outIdx + 3] = (byte)(sumA / count);
            }
        }

        bitmap.WritePixels(region, dst, stride, 0);
    }

    private static Int32Rect Clamp(Int32Rect r, int maxWidth, int maxHeight)
    {
        int x = Math.Clamp(r.X, 0, maxWidth);
        int y = Math.Clamp(r.Y, 0, maxHeight);
        int w = Math.Clamp(r.Width, 0, maxWidth - x);
        int h = Math.Clamp(r.Height, 0, maxHeight - y);
        return new Int32Rect(x, y, w, h);
    }
}
