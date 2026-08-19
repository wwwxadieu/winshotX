using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinShootX.Services;
using WinShootX.Views.Controls;

namespace WinShootX.Views;

/// <summary>
/// Ảnh "ghim" nổi luôn ở trên cùng desktop — tính năng đặc trưng "Pin to Screen" của CleanShot X.
/// Kéo để di chuyển, kéo góc để resize (ResizeMode="CanResizeWithGrip"), chuột phải để mở menu
/// (copy, đổi độ trong suốt, gỡ ghim).
/// </summary>
public partial class PinnedWindow : Window
{
    public PinnedWindow(BitmapSource image)
    {
        InitializeComponent();
        PinnedImage.Source = image;

        Width = Math.Min(image.PixelWidth, SystemParameters.WorkArea.Width * 0.6);
        Height = image.PixelHeight * (Width / image.PixelWidth);

        Left = SystemParameters.WorkArea.Right - Width - 40;
        Top = SystemParameters.WorkArea.Top + 40;
    }

    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            // Double-click: gỡ ghim (đóng cửa sổ) — thao tác nhanh giống bản gốc.
            Close();
            return;
        }
        DragMove();
    }

    private void OnContextMenu(object sender, MouseButtonEventArgs e)
    {
        var menu = new ContextMenu();

        var copyItem = new MenuItem { Header = "Copy ảnh", Icon = IconHelper.Create((Geometry)FindResource("IconCopy")) };
        copyItem.Click += (_, _) => ClipboardService.CopyImage((BitmapSource)PinnedImage.Source);
        menu.Items.Add(copyItem);

        var opacityMenu = new MenuItem { Header = "Độ trong suốt" };
        foreach (var percent in new[] { 100, 75, 50, 25 })
        {
            var item = new MenuItem { Header = $"{percent}%" };
            item.Click += (_, _) => Opacity = percent / 100.0;
            opacityMenu.Items.Add(item);
        }
        menu.Items.Add(opacityMenu);

        var closeItem = new MenuItem { Header = "Gỡ ghim", Icon = IconHelper.Create((Geometry)FindResource("IconClose")) };
        closeItem.Click += (_, _) => Close();
        menu.Items.Add(closeItem);

        menu.IsOpen = true;
    }
}
