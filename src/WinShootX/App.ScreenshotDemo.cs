using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using WinShootX.Models;
using WinShootX.Services;
using WinShootX.Views;

namespace WinShootX;

/// <summary>
/// Công cụ chụp ảnh demo UI dùng nội bộ bởi CI (đối số dòng lệnh "--screenshot-demo &lt;thư mục&gt;",
/// xem OnStartup trong App.xaml.cs). Dựng một "màn hình mẫu" tổng hợp (không phải ảnh chụp thật, vì
/// máy CI không có gì đáng chụp trên desktop) và mở lần lượt các cửa sổ chính của app với dữ liệu mẫu
/// gắn trên đó, rồi render từng cửa sổ ra PNG — cho ra ảnh demo đúng bằng chính UI thật của app, không
/// phải mockup vẽ tay. Không dùng trong luồng chạy bình thường của người dùng cuối.
/// </summary>
public partial class App
{
    private void RunScreenshotDemo(string outDir)
    {
        Directory.CreateDirectory(outDir);

        var sample = CreateSampleSceneBitmap(1280, 800);
        var sampleCapture = new CaptureResult { Image = sample, SourceBounds = new Rect(120, 90, sample.PixelWidth, sample.PixelHeight) };

        var editor = new AnnotationEditorWindow(sample);
        editor.Show();
        PumpLayout(editor);
        AddDemoAnnotations(editor);
        PumpLayout(editor);
        SaveWindowScreenshot(editor, System.IO.Path.Combine(outDir, "01-annotation-editor.png"));
        editor.Close();

        var bar = new CaptureBarWindow(sampleCapture, _settingsService);
        bar.Show();
        PumpLayout(bar);
        SaveWindowScreenshot(bar, System.IO.Path.Combine(outDir, "02-capture-bar.png"));
        bar.Close();

        var demoHistory = new HistoryService();
        demoHistory.Add(sampleCapture);
        demoHistory.Add(new CaptureResult { Image = sample, SourceBounds = sampleCapture.SourceBounds, CapturedAtUtc = DateTime.UtcNow.AddMinutes(-12) });
        demoHistory.Add(new CaptureResult { Image = sample, SourceBounds = sampleCapture.SourceBounds, CapturedAtUtc = DateTime.UtcNow.AddHours(-2) });
        var historyWindow = new HistoryWindow(demoHistory);
        historyWindow.Show();
        PumpLayout(historyWindow);
        SaveWindowScreenshot(historyWindow, System.IO.Path.Combine(outDir, "03-history.png"));
        historyWindow.Close();

        var settingsWindow = new SettingsWindow(_settingsService);
        settingsWindow.Show();
        PumpLayout(settingsWindow);
        SaveWindowScreenshot(settingsWindow, System.IO.Path.Combine(outDir, "04-settings.png"));
        settingsWindow.Close();

        var pinned = new PinnedWindow(sample);
        pinned.Show();
        PumpLayout(pinned);
        SaveWindowScreenshot(pinned, System.IO.Path.Combine(outDir, "05-pinned.png"));
        pinned.Close();

        var recordingControl = new RecordingControlWindow(new ScreenRecordingService(), new Rect(200, 200, 500, 320));
        recordingControl.Show();
        recordingControl.ElapsedText.Text = "01:24"; // giả lập đang quay được 1:24 để ảnh demo có nội dung, không phải 00:00
        PumpLayout(recordingControl);
        SaveWindowScreenshot(recordingControl, System.IO.Path.Combine(outDir, "06-recording.png"));
        recordingControl.Close();
    }

    private static void PumpLayout(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
    }

    private static void SaveWindowScreenshot(Window window, string path)
    {
        int width = Math.Max(1, (int)Math.Ceiling(window.ActualWidth));
        int height = Math.Max(1, (int)Math.Ceiling(window.ActualHeight));

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(window);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    /// <summary>Thêm sẵn vài chú thích tiêu biểu (mũi tên, khung, highlight, số bước, blur) lên canvas
    /// để ảnh demo thể hiện được bộ công cụ chú thích thay vì chỉ hiện ảnh trắng.</summary>
    private static void AddDemoAnnotations(AnnotationEditorWindow editor)
    {
        var canvas = editor.AnnotationCanvas;

        var formOutline = new Rectangle
        {
            Width = 380, Height = 230, Stroke = Brushes.OrangeRed, StrokeThickness = 3, Fill = Brushes.Transparent,
        };
        Canvas.SetLeft(formOutline, 48); Canvas.SetTop(formOutline, 260);
        canvas.Children.Add(formOutline);

        var headingHighlight = new Rectangle
        {
            Width = 320, Height = 48, Fill = new SolidColorBrush(Color.FromArgb(90, 255, 235, 59)),
        };
        Canvas.SetLeft(headingHighlight, 56); Canvas.SetTop(headingHighlight, 82);
        canvas.Children.Add(headingHighlight);

        canvas.Children.Add(ArrowShape.Create(new Point(270, 570), new Point(150, 486), Brushes.DodgerBlue));

        var ctaNote = new TextBlock
        {
            Text = "Nút CTA chính", Foreground = Brushes.DodgerBlue, FontSize = 18, FontWeight = FontWeights.SemiBold,
        };
        Canvas.SetLeft(ctaNote, 258); Canvas.SetTop(ctaNote, 562);
        canvas.Children.Add(ctaNote);

        AddStepBadge(canvas, new Point(28, 332), 1);
        AddStepBadge(canvas, new Point(28, 410), 2);

        var emailFieldRegion = new Rect(60, 312, 340, 40);
        var blurBrush = new VisualBrush(editor.BaseImage)
        {
            Stretch = Stretch.None, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = emailFieldRegion,
        };
        var blurPatch = new Rectangle
        {
            Tag = "Blur", Width = emailFieldRegion.Width, Height = emailFieldRegion.Height,
            Fill = blurBrush, Effect = new BlurEffect { Radius = 14 },
        };
        Canvas.SetLeft(blurPatch, emailFieldRegion.X); Canvas.SetTop(blurPatch, emailFieldRegion.Y);
        canvas.Children.Add(blurPatch);
    }

    private static void AddStepBadge(Canvas canvas, Point origin, int number)
    {
        const double size = 28;
        var ellipse = new Ellipse { Width = size, Height = size, Fill = Brushes.MediumPurple };
        var label = new TextBlock
        {
            Text = number.ToString(CultureInfo.InvariantCulture), Foreground = Brushes.White,
            FontWeight = FontWeights.Bold, FontSize = 14, Width = size, Height = size, TextAlignment = TextAlignment.Center,
        };
        var group = new Grid();
        group.Children.Add(ellipse);
        group.Children.Add(label);
        Canvas.SetLeft(group, origin.X - size / 2);
        Canvas.SetTop(group, origin.Y - size / 2);
        canvas.Children.Add(group);
    }

    /// <summary>Dựng một "màn hình mẫu" (trang đăng ký tài khoản giả lập) để làm nội dung demo cho
    /// AnnotationEditorWindow/CaptureBarWindow/PinnedWindow/HistoryWindow — máy CI không có nội dung
    /// thật đáng chụp trên desktop nên cần cảnh giả lập có bố cục giống ảnh chụp thật.</summary>
    private static BitmapSource CreateSampleSceneBitmap(int width, int height)
    {
        var typeface = new Typeface("Segoe UI");
        var visual = new DrawingVisual();

        using (var ctx = visual.RenderOpen())
        {
            ctx.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));

            var headerBrush = new SolidColorBrush(Color.FromRgb(0x25, 0x3C, 0xDB));
            ctx.DrawRectangle(headerBrush, null, new Rect(0, 0, width, 64));
            ctx.DrawText(Text("Win ShootX Demo — Trang đăng ký tài khoản", 20, Brushes.White, typeface), new Point(24, 20));

            ctx.DrawText(Text("Tạo tài khoản mới", 30, Brushes.Black, typeface), new Point(60, 108));

            var lineBrush = new SolidColorBrush(Color.FromRgb(0xE4, 0xE6, 0xEC));
            for (int i = 0; i < 3; i++)
                ctx.DrawRectangle(lineBrush, null, new Rect(60, 170 + i * 22, 480 - i * 60, 12));

            ctx.DrawText(Text("Email", 15, Brushes.Gray, typeface), new Point(60, 288));
            ctx.DrawRoundedRectangle(Brushes.White, new Pen(Brushes.LightGray, 1), new Rect(60, 312, 340, 40), 6, 6);
            ctx.DrawText(Text("nguyen.van.a@example.com", 16, Brushes.Black, typeface), new Point(72, 323));

            ctx.DrawText(Text("Mật khẩu", 15, Brushes.Gray, typeface), new Point(60, 366));
            ctx.DrawRoundedRectangle(Brushes.White, new Pen(Brushes.LightGray, 1), new Rect(60, 390, 340, 40), 6, 6);
            ctx.DrawText(Text("••••••••••", 16, Brushes.Black, typeface), new Point(72, 401));

            ctx.DrawRoundedRectangle(headerBrush, null, new Rect(60, 486, 170, 46), 8, 8);
            ctx.DrawText(Text("Đăng ký", 17, Brushes.White, typeface), new Point(104, 500));

            var codeBg = new SolidColorBrush(Color.FromRgb(0x1E, 0x1F, 0x24));
            ctx.DrawRoundedRectangle(codeBg, null, new Rect(width - 460, 150, 400, 260), 10, 10);
            var codeColor = new SolidColorBrush(Color.FromRgb(0x9C, 0xDC, 0xFE));
            string code = "var app = new WinShootX();\n\napp.Capture(Region);\napp.Annotate();\napp.PinToScreen();\napp.Share();";
            ctx.DrawText(Text(code, 15, codeColor, new Typeface("Consolas")), new Point(width - 436, 178));
        }

        var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;

        static FormattedText Text(string text, double size, Brush brush, Typeface tf) => new(
            text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, size, brush, 1.0);
    }
}
