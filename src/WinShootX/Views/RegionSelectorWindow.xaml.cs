using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WinShootX.Views;

/// <summary>
/// Overlay trong suốt phủ toàn bộ virtual screen để người dùng kéo chọn vùng chụp.
/// Trả về vùng chọn theo toạ độ pixel vật lý qua sự kiện <see cref="RegionSelected"/>.
/// </summary>
public partial class RegionSelectorWindow : Window
{
    public event Action<Int32Rect>? RegionSelected;
    public event Action? Cancelled;

    private Point? _startPoint;
    private readonly double _dpiScale;

    public RegionSelectorWindow()
    {
        InitializeComponent();

        // Phủ đúng toàn bộ virtual screen (gộp mọi màn hình).
        Left = SystemParameters.VirtualScreenLeft;
        Top = SystemParameters.VirtualScreenTop;
        Width = SystemParameters.VirtualScreenWidth;
        Height = SystemParameters.VirtualScreenHeight;

        // Lấy DPI scale để quy đổi toạ độ WPF (device-independent) sang pixel vật lý khi chụp.
        _dpiScale = VisualTreeHelper.GetDpi(this).DpiScaleX;

        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        KeyDown += OnKeyDown;
        Loaded += (_, _) => Activate();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Cancelled?.Invoke();
            Close();
        }
    }

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        _startPoint = e.GetPosition(this);
        SelectionRect.Visibility = Visibility.Visible;
        SizeLabel.Visibility = Visibility.Visible;
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (_startPoint is not { } start) return;
        var current = e.GetPosition(this);

        double x = Math.Min(start.X, current.X);
        double y = Math.Min(start.Y, current.Y);
        double w = Math.Abs(current.X - start.X);
        double h = Math.Abs(current.Y - start.Y);

        Canvas.SetLeft(SelectionRect, x);
        Canvas.SetTop(SelectionRect, y);
        SelectionRect.Width = w;
        SelectionRect.Height = h;

        SizeLabelText.Text = $"{(int)(w * _dpiScale)} × {(int)(h * _dpiScale)}";
        Canvas.SetLeft(SizeLabel, x);
        Canvas.SetTop(SizeLabel, Math.Max(0, y - 26));
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_startPoint is not { } start) return;
        var end = e.GetPosition(this);

        double x = Math.Min(start.X, end.X);
        double y = Math.Min(start.Y, end.Y);
        double w = Math.Abs(end.X - start.X);
        double h = Math.Abs(end.Y - start.Y);

        _startPoint = null;

        if (w < 3 || h < 3)
        {
            // Vùng chọn quá nhỏ (coi như click nhầm) — huỷ thay vì trả về ảnh 0px.
            Cancelled?.Invoke();
            Close();
            return;
        }

        // Quy đổi sang toạ độ pixel vật lý trên virtual screen để ScreenCaptureService dùng trực tiếp với BitBlt.
        int physX = (int)((Left + x) * _dpiScale);
        int physY = (int)((Top + y) * _dpiScale);
        int physW = (int)(w * _dpiScale);
        int physH = (int)(h * _dpiScale);

        RegionSelected?.Invoke(new Int32Rect(physX, physY, physW, physH));
        Close();
    }
}
