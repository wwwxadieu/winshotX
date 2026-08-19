using System.Windows.Media;
using System.Windows.Shapes;

namespace WinShootX.Views.Controls;

/// <summary>
/// Tạo Path icon vector dùng ở những chỗ không thể dùng IconButton/IconToggleButton — cụ thể là
/// MenuItem.Icon (context menu chuột phải, tray menu) vốn nhận UIElement bất kỳ chứ không có sẵn
/// khái niệm "icon property" như control tự viết của chúng ta. Cùng style stroke-icon với
/// IconControls.xaml để nhất quán trong toàn app — không dùng emoji ở đây.
///
/// Lưu ý màu mặc định: ContextMenu (tray menu, menu chuột phải của PinnedWindow) dùng chrome hệ
/// thống nền sáng mặc định (không được style lại như các cửa sổ nền tối khác trong app), nên icon ở
/// đây mặc định màu tối (#1F2937) chứ không phải trắng như IconButton trên nền tối.
/// </summary>
public static class IconHelper
{
    private static readonly Brush DefaultStroke = new SolidColorBrush(Color.FromRgb(0x1F, 0x29, 0x37));

    public static Path Create(Geometry data, double size = 14, Brush? stroke = null)
    {
        return new Path
        {
            Data = data,
            Stroke = stroke ?? DefaultStroke,
            StrokeThickness = 1.6,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            Fill = Brushes.Transparent,
            Stretch = Stretch.Uniform,
            Width = size,
            Height = size,
        };
    }
}
