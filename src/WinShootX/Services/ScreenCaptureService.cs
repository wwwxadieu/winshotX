using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using WinShootX.Models;

namespace WinShootX.Services;

/// <summary>
/// Chụp pixel màn hình bằng GDI BitBlt — cách tiếp cận đã được ShareX/Greenshot dùng ổn định
/// nhiều năm trên Windows. Yêu cầu app.manifest khai báo Per-Monitor V2 DPI awareness để toạ độ
/// khớp chính xác trên hệ nhiều màn hình có tỷ lệ scale khác nhau.
/// </summary>
public sealed class ScreenCaptureService
{
    [DllImport("user32.dll")] private static extern IntPtr GetDesktopWindow();
    [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int width, int height, IntPtr hdcSrc, int xSrc, int ySrc, int rop);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr hObject);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    private const int SRCCOPY = 0x00CC0020;
    private const uint PW_RENDERFULLCONTENT = 0x00000002; // cần cho app dùng DirectX/UWP (vd trình duyệt hiện đại)

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    /// <summary>Chụp một vùng chữ nhật theo toạ độ virtual-screen (đơn vị pixel vật lý).</summary>
    public CaptureResult CaptureRegion(Int32Rect region)
    {
        IntPtr desktopHwnd = GetDesktopWindow();
        IntPtr desktopDc = GetWindowDC(desktopHwnd);
        IntPtr memDc = CreateCompatibleDC(desktopDc);
        IntPtr bitmap = CreateCompatibleBitmap(desktopDc, region.Width, region.Height);
        IntPtr oldBitmap = SelectObject(memDc, bitmap);

        try
        {
            BitBlt(memDc, 0, 0, region.Width, region.Height, desktopDc, region.X, region.Y, SRCCOPY);

            var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bitmapSource.Freeze();

            return new CaptureResult
            {
                Image = bitmapSource,
                SourceBounds = new Rect(region.X, region.Y, region.Width, region.Height),
            };
        }
        finally
        {
            SelectObject(memDc, oldBitmap);
            DeleteObject(bitmap);
            DeleteDC(memDc);
            ReleaseDC(desktopHwnd, desktopDc);
        }
    }

    /// <summary>Chụp toàn bộ virtual screen (tất cả màn hình ghép lại).</summary>
    public CaptureResult CaptureFullVirtualScreen()
    {
        double left = SystemParameters.VirtualScreenLeft;
        double top = SystemParameters.VirtualScreenTop;
        double width = SystemParameters.VirtualScreenWidth;
        double height = SystemParameters.VirtualScreenHeight;

        return CaptureRegion(new Int32Rect((int)left, (int)top, (int)width, (int)height));
    }

    /// <summary>Chụp cửa sổ đang active bằng PrintWindow (bắt được cả nội dung bị che khuất/DirectX).</summary>
    public CaptureResult CaptureForegroundWindow()
    {
        IntPtr hwnd = GetForegroundWindow();
        GetWindowRect(hwnd, out var rect);
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        IntPtr screenDc = GetWindowDC(GetDesktopWindow());
        IntPtr memDc = CreateCompatibleDC(screenDc);
        IntPtr bitmap = CreateCompatibleBitmap(screenDc, width, height);
        IntPtr oldBitmap = SelectObject(memDc, bitmap);

        try
        {
            PrintWindow(hwnd, memDc, PW_RENDERFULLCONTENT);

            var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            bitmapSource.Freeze();

            return new CaptureResult
            {
                Image = bitmapSource,
                SourceBounds = new Rect(rect.Left, rect.Top, width, height),
            };
        }
        finally
        {
            SelectObject(memDc, oldBitmap);
            DeleteObject(bitmap);
            DeleteDC(memDc);
            ReleaseDC(GetDesktopWindow(), screenDc);
        }
    }
}
