using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace WinShootX.Views;

/// <summary>Vẽ mũi tên (thân line + đầu tam giác) làm một Path duy nhất, dễ add/remove khỏi canvas cho undo/redo.</summary>
internal static class ArrowShape
{
    public static Path Create(Point start, Point end, Brush color)
    {
        const double headLength = 14;
        const double headWidth = 9;

        var direction = end - start;
        if (direction.Length < 0.01) direction = new Vector(1, 0);
        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);

        var headBase = end - direction * headLength;
        var left = headBase + normal * headWidth;
        var right = headBase - normal * headWidth;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            // Thân mũi tên (dừng trước đầu mũi tên để không bị đè lên).
            ctx.BeginFigure(start, false, false);
            ctx.LineTo(headBase, true, false);

            // Đầu mũi tên hình tam giác.
            ctx.BeginFigure(end, true, true);
            ctx.LineTo(left, true, false);
            ctx.LineTo(right, true, false);
        }
        geometry.Freeze();

        return new Path
        {
            Data = geometry,
            Stroke = color,
            Fill = color,
            StrokeThickness = 3,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
    }
}
