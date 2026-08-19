namespace WinShootX.Models;

/// <summary>Các công cụ trong trình chú thích, tương đương bộ công cụ của CleanShot X Annotation Editor.</summary>
public enum AnnotationTool
{
    None,
    Arrow,
    Rectangle,
    Ellipse,
    Freehand,
    Text,
    Highlight,
    Blur,
    Pixelate,
    StepNumber,
    Crop,
}

/// <summary>Một thao tác chú thích đã áp dụng lên canvas — dùng cho undo/redo.</summary>
public sealed class AnnotationItem
{
    public AnnotationTool Tool { get; init; }
    public System.Windows.Shapes.Shape? VisualShape { get; set; }
    public System.Windows.UIElement? VisualElement { get; set; }
}
