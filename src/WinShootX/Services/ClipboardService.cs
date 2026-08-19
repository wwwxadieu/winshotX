using System.Windows;
using System.Windows.Media.Imaging;

namespace WinShootX.Services;

public static class ClipboardService
{
    public static void CopyImage(BitmapSource image)
    {
        // Retry ngắn vì Clipboard Win32 API đôi khi bận (bị app khác giữ lock trong vài ms).
        const int maxAttempts = 5;
        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                Clipboard.SetImage(image);
                return;
            }
            catch (System.Runtime.InteropServices.COMException) when (i < maxAttempts - 1)
            {
                System.Threading.Thread.Sleep(50);
            }
        }
    }
}
