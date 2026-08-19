using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace WinShootX.Views.Controls;

/// <summary>
/// Button hiển thị icon vector (Geometry) + nhãn chữ, thay cho việc dùng emoji/ký tự Unicode làm
/// icon. Style mặc định (icon + label, hover, v.v.) định nghĩa trong IconControls.xaml — file này
/// chỉ khai báo các DependencyProperty. Dùng ở mọi nơi cần nút bấm có icon trong toàn ứng dụng.
/// </summary>
public class IconButton : Button
{
    public static readonly DependencyProperty IconDataProperty = DependencyProperty.Register(
        nameof(IconData), typeof(Geometry), typeof(IconButton));

    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(
        nameof(IconSize), typeof(double), typeof(IconButton), new PropertyMetadata(16.0));

    public Geometry? IconData
    {
        get => (Geometry?)GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }
}

/// <summary>Phiên bản ToggleButton của <see cref="IconButton"/> — dùng cho nhóm công cụ chọn 1-trong-N
/// (vd thanh công cụ chú thích: mũi tên/chữ nhật/oval/...).</summary>
public class IconToggleButton : ToggleButton
{
    public static readonly DependencyProperty IconDataProperty = DependencyProperty.Register(
        nameof(IconData), typeof(Geometry), typeof(IconToggleButton));

    public static readonly DependencyProperty IconSizeProperty = DependencyProperty.Register(
        nameof(IconSize), typeof(double), typeof(IconToggleButton), new PropertyMetadata(16.0));

    public Geometry? IconData
    {
        get => (Geometry?)GetValue(IconDataProperty);
        set => SetValue(IconDataProperty, value);
    }

    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }
}
