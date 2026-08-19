using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using WinShootX.Models;
using WinShootX.Services;

namespace WinShootX.Views;

/// <summary>
/// Trình chú thích: vẽ mũi tên/hình/chữ/highlight/blur lên canvas đè trên ảnh gốc, rồi flatten
/// ra bitmap khi lưu/copy. Đơn giản hoá so với CleanShot X ở chỗ blur là một "miếng dán" hiệu ứng
/// BlurEffect chứ chưa xử lý pixel thật — đủ dùng cho MVP, có thể nâng cấp sau (xem TODO ở OnToolChecked/Blur).
/// </summary>
public partial class AnnotationEditorWindow : Window
{
    public ObservableCollection<Brush> SwatchColors { get; } = new()
    {
        Brushes.Red, Brushes.OrangeRed, Brushes.Gold, Brushes.LimeGreen,
        Brushes.DodgerBlue, Brushes.MediumPurple, Brushes.White, Brushes.Black,
    };

    private readonly BitmapSource _sourceImage;
    private AnnotationTool _currentTool = AnnotationTool.Arrow;
    private Brush _currentColor = Brushes.Red;

    private readonly List<UIElement> _undoStack = new();
    private readonly List<UIElement> _redoStack = new();

    private Point? _dragStart;
    private Shape? _dragShape;
    private Polyline? _freehandLine;
    private int _stepCounter = 1;
    private Rectangle? _cropRect;

    public AnnotationEditorWindow(BitmapSource sourceImage)
    {
        InitializeComponent();
        DataContext = this;
        _sourceImage = sourceImage;

        BaseImage.Source = sourceImage;
        AnnotationCanvas.Width = sourceImage.PixelWidth / sourceImage.DpiX * 96.0;
        AnnotationCanvas.Height = sourceImage.PixelHeight / sourceImage.DpiY * 96.0;

        ToolArrow.IsChecked = true;
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        // Esc huỷ khung crop đang chọn (không cắt gì cả) — không đóng cửa sổ, tránh mất chú thích đã vẽ.
        if (e.Key == Key.Escape && _cropRect != null)
        {
            AnnotationCanvas.Children.Remove(_cropRect);
            _cropRect = null;
            e.Handled = true;
        }
    }

    private void OnToolChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton { Tag: string tag } tb) return;

        // Chỉ 1 tool được chọn tại 1 thời điểm (giả lập RadioButton group bằng cách bỏ check các nút khác).
        foreach (var child in ((StackPanel)tb.Parent).Children)
        {
            if (child is ToggleButton other && other != tb)
                other.IsChecked = false;
        }

        _currentTool = Enum.Parse<AnnotationTool>(tag);
    }

    private void OnSwatchClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Background: Brush brush })
            _currentColor = brush;
    }

    private void OnCanvasMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(AnnotationCanvas);

        switch (_currentTool)
        {
            case AnnotationTool.Arrow:
            case AnnotationTool.Rectangle:
            case AnnotationTool.Ellipse:
            case AnnotationTool.Highlight:
            case AnnotationTool.Blur:
            case AnnotationTool.Pixelate:
            case AnnotationTool.Crop:
                _dragShape = CreateShapeForCurrentTool(_dragStart.Value);
                AnnotationCanvas.Children.Add(_dragShape);
                break;

            case AnnotationTool.Freehand:
                _freehandLine = new Polyline
                {
                    Stroke = _currentColor,
                    StrokeThickness = 3,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                };
                _freehandLine.Points.Add(_dragStart.Value);
                AnnotationCanvas.Children.Add(_freehandLine);
                break;

            case AnnotationTool.Text:
                AddTextBox(_dragStart.Value);
                _dragStart = null;
                break;

            case AnnotationTool.StepNumber:
                AddStepBadge(_dragStart.Value);
                _dragStart = null;
                break;
        }
    }

    private Shape CreateShapeForCurrentTool(Point origin)
    {
        Shape shape = _currentTool switch
        {
            AnnotationTool.Ellipse => new Ellipse(),
            AnnotationTool.Highlight => new Rectangle { Fill = new SolidColorBrush(Color.FromArgb(90, 255, 235, 59)) },
            AnnotationTool.Blur or AnnotationTool.Pixelate => CreateBlurPatch(),
            AnnotationTool.Crop => new Rectangle
            {
                Tag = "Crop",
                Stroke = Brushes.White, StrokeThickness = 1.5, StrokeDashArray = new DoubleCollection { 4, 2 },
                Fill = new SolidColorBrush(Color.FromArgb(60, 0, 0, 0)),
            },
            _ => new Rectangle(), // Rectangle, Arrow (mũi tên vẽ line+head khi mouse up, tạm dùng khung khi đang kéo)
        };

        if (_currentTool is AnnotationTool.Rectangle)
        {
            shape.Stroke = _currentColor;
            shape.StrokeThickness = 3;
            shape.Fill = Brushes.Transparent;
        }
        else if (_currentTool is AnnotationTool.Ellipse)
        {
            shape.Stroke = _currentColor;
            shape.StrokeThickness = 3;
            shape.Fill = Brushes.Transparent;
        }

        Canvas.SetLeft(shape, origin.X);
        Canvas.SetTop(shape, origin.Y);
        return shape;
    }

    /// <summary>
    /// Bản xem trước khi đang kéo chuột: VisualBrush chụp lại vùng ảnh gốc bên dưới + BlurEffect làm
    /// mờ tạm để người dùng thấy ngay hiệu ứng. Khi flatten (<see cref="RenderFlattened"/>), phần
    /// preview này bị thay bằng xử lý pixel thật (box blur / mosaic) trên chính ảnh đã render — xem
    /// <see cref="PixelEffects"/> — để đảm bảo vùng che không thể khôi phục lại khi ảnh được chia sẻ.
    /// </summary>
    private Rectangle CreateBlurPatch()
    {
        var brush = new VisualBrush(BaseImage) { Stretch = Stretch.None, ViewboxUnits = BrushMappingMode.Absolute };
        var rect = new Rectangle
        {
            Tag = _currentTool == AnnotationTool.Pixelate ? "Pixelate" : "Blur",
            Fill = brush,
            Effect = new BlurEffect { Radius = _currentTool == AnnotationTool.Pixelate ? 2 : 14 },
        };
        return rect;
    }

    private void OnCanvasMouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStart is not { } start) return;
        var current = e.GetPosition(AnnotationCanvas);

        if (_freehandLine != null)
        {
            _freehandLine.Points.Add(current);
            return;
        }

        if (_dragShape == null) return;

        double x = Math.Min(start.X, current.X);
        double y = Math.Min(start.Y, current.Y);
        double w = Math.Abs(current.X - start.X);
        double h = Math.Abs(current.Y - start.Y);

        Canvas.SetLeft(_dragShape, x);
        Canvas.SetTop(_dragShape, y);
        _dragShape.Width = w;
        _dragShape.Height = h;

        if (_dragShape.Fill is VisualBrush vb)
        {
            // Đồng bộ viewbox để "miếng dán" blur luôn phản chiếu đúng vùng ảnh gốc bên dưới nó.
            vb.Viewbox = new Rect(x, y, w, h);
        }
    }

    private void OnCanvasMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragStart is not { } start) { _dragStart = null; return; }
        var end = e.GetPosition(AnnotationCanvas);

        if (_currentTool == AnnotationTool.Arrow && _dragShape != null)
        {
            AnnotationCanvas.Children.Remove(_dragShape);
            var arrow = ArrowShape.Create(start, end, _currentColor);
            AnnotationCanvas.Children.Add(arrow);
            PushUndo(arrow);
        }
        else if (_currentTool == AnnotationTool.Crop && _dragShape is Rectangle cropRect)
        {
            if (cropRect.Width < 8 || cropRect.Height < 8)
            {
                AnnotationCanvas.Children.Remove(cropRect);
            }
            else
            {
                // Chỉ giữ 1 khung crop tại 1 thời điểm — khung mới thay thế khung cũ.
                if (_cropRect != null)
                    AnnotationCanvas.Children.Remove(_cropRect);
                _cropRect = cropRect;
            }
        }
        else if (_freehandLine != null)
        {
            PushUndo(_freehandLine);
        }
        else if (_dragShape != null)
        {
            if (_dragShape.Width < 2 || _dragShape.Height < 2)
                AnnotationCanvas.Children.Remove(_dragShape); // kéo quá nhỏ, huỷ
            else
                PushUndo(_dragShape);
        }

        _dragStart = null;
        _dragShape = null;
        _freehandLine = null;
    }

    private void AddTextBox(Point origin)
    {
        var textBox = new TextBox
        {
            Foreground = _currentColor,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0), // ghi đè Padding mặc định của style TextBox toàn app (xem
                                         // TextBoxStyle.xaml) — chữ phải bắt đầu sát điểm click, không lùi vào trong.
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            MinWidth = 60,
        };
        Canvas.SetLeft(textBox, origin.X);
        Canvas.SetTop(textBox, origin.Y);
        AnnotationCanvas.Children.Add(textBox);
        textBox.Focus();
        textBox.LostFocus += (_, _) => PushUndo(textBox);
    }

    private void AddStepBadge(Point origin)
    {
        const double size = 28;
        var ellipse = new Ellipse { Width = size, Height = size, Fill = _currentColor };
        var label = new TextBlock
        {
            Text = (_stepCounter++).ToString(),
            Foreground = Brushes.White,
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Width = size,
            Height = size,
            TextAlignment = TextAlignment.Center,
        };
        var group = new Grid();
        group.Children.Add(ellipse);
        group.Children.Add(label);
        Canvas.SetLeft(group, origin.X - size / 2);
        Canvas.SetTop(group, origin.Y - size / 2);
        AnnotationCanvas.Children.Add(group);
        PushUndo(group);
    }

    private void PushUndo(UIElement element)
    {
        _undoStack.Add(element);
        _redoStack.Clear();
    }

    private void OnUndoClick(object sender, RoutedEventArgs e)
    {
        if (_undoStack.Count == 0) return;
        var last = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        AnnotationCanvas.Children.Remove(last);
        _redoStack.Add(last);
    }

    private void OnRedoClick(object sender, RoutedEventArgs e)
    {
        if (_redoStack.Count == 0) return;
        var item = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        AnnotationCanvas.Children.Add(item);
        _undoStack.Add(item);
    }

    /// <summary>
    /// Flatten ảnh gốc + toàn bộ annotation thành 1 bitmap duy nhất. Vùng Blur/Pixelate được xử lý
    /// pixel thật (không phải hiệu ứng thị giác tạm) qua <see cref="PixelEffects"/>, và nếu có khung
    /// crop thì ảnh xuất ra được cắt đúng theo khung đó — cả ảnh gốc lẫn mọi chú thích đã vẽ.
    /// </summary>
    private BitmapSource RenderFlattened()
    {
        var grid = (Grid)AnnotationCanvas.Parent;
        var bounds = new Rect(0, 0, grid.ActualWidth, grid.ActualHeight);

        // Ẩn khung crop (chỉ là chỉ báo UI, không phải nội dung ảnh) trước khi render.
        var cropWasVisible = _cropRect?.Visibility;
        if (_cropRect != null) _cropRect.Visibility = Visibility.Collapsed;

        var rtb = new RenderTargetBitmap((int)bounds.Width, (int)bounds.Height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(grid);

        if (_cropRect != null && cropWasVisible.HasValue) _cropRect.Visibility = cropWasVisible.Value;

        var writable = new WriteableBitmap(rtb);

        foreach (var element in AnnotationCanvas.Children)
        {
            if (element is not Rectangle { Tag: "Blur" or "Pixelate" } rect) continue;

            var region = new Int32Rect(
                (int)Canvas.GetLeft(rect), (int)Canvas.GetTop(rect),
                (int)rect.Width, (int)rect.Height);

            if ((string)rect.Tag == "Pixelate")
                PixelEffects.Pixelate(writable, region, blockSize: 12);
            else
                PixelEffects.BoxBlur(writable, region, radius: 6);
        }

        BitmapSource result = writable;

        if (_cropRect != null)
        {
            var cropRegion = new Int32Rect(
                (int)Canvas.GetLeft(_cropRect), (int)Canvas.GetTop(_cropRect),
                (int)_cropRect.Width, (int)_cropRect.Height);
            cropRegion = ClampToBounds(cropRegion, writable.PixelWidth, writable.PixelHeight);
            if (cropRegion.Width > 0 && cropRegion.Height > 0)
                result = new CroppedBitmap(writable, cropRegion);
        }

        if (result.CanFreeze) result.Freeze();
        return result;
    }

    private static Int32Rect ClampToBounds(Int32Rect rect, int maxWidth, int maxHeight)
    {
        int x = Math.Clamp(rect.X, 0, maxWidth);
        int y = Math.Clamp(rect.Y, 0, maxHeight);
        int w = Math.Clamp(rect.Width, 0, maxWidth - x);
        int h = Math.Clamp(rect.Height, 0, maxHeight - y);
        return new Int32Rect(x, y, w, h);
    }

    private void OnCopyClick(object sender, RoutedEventArgs e) => ClipboardService.CopyImage(RenderFlattened());

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var settings = new SettingsService();
        settings.Load();
        var path = FileService.SavePng(RenderFlattened(), settings.Current.SaveDirectory);
        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
