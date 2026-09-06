using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace SnipStudio.Controls;

public sealed class Icon : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(Icon), new FrameworkPropertyMetadata("capture", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(nameof(Stroke), typeof(Brush), typeof(Icon), new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromRgb(188, 201, 220)), FrameworkPropertyMetadataOptions.AffectsRender));
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public Brush Stroke { get => (Brush)GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    private static readonly Dictionary<string, string> Paths = new()
    {
        ["capture"] = "M8,3 L3,3 3,8 M16,3 L21,3 21,8 M21,16 L21,21 16,21 M8,21 L3,21 3,16 M8,12 L11,15 17,9",
        ["plus"] = "M12,5 L12,19 M5,12 L19,12",
        ["select"] = "M5,3 L5,19 10,15 14,21 17,19 13,13 20,12 Z",
        ["pen"] = "M4,20 L5,14 16,3 21,8 10,19 Z M13,6 L18,11 M5,14 L10,19",
        ["highlighter"] = "M4,17 L13,8 18,13 9,22 Z M13,8 L17,4 22,9 18,13 M3,21 L11,21",
        ["arrow"] = "M4,20 L20,4 M8,4 L20,4 20,16",
        ["line"] = "M4,20 L20,4",
        ["rectangle"] = "M4,5 L20,5 20,19 4,19 Z",
        ["ellipse"] = "M21,12 A9,7 0 1 1 3,12 A9,7 0 1 1 21,12",
        ["text"] = "M4,5 L20,5 M12,5 L12,20 M8,20 L16,20 M4,5 L4,8 M20,5 L20,8",
        ["step"] = "M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12 M10,9 L12,7 12,17 M9,17 L15,17",
        ["pixelate"] = "M3,3 L10,3 10,10 3,10 Z M14,3 L21,3 21,10 14,10 Z M3,14 L10,14 10,21 3,21 Z M14,14 L17,14 17,17 14,17 Z M19,19 L21,19 21,21 19,21 Z",
        ["redact"] = "M4,5 L20,5 20,19 4,19 Z M7,9 L17,9 M7,12 L17,12 M7,15 L17,15",
        ["crop"] = "M6,2 L6,18 22,18 M2,6 L18,6 18,22",
        ["undo"] = "M8,5 L3,10 8,15 M3,10 L14,10 C22,10 22,21 14,21",
        ["redo"] = "M16,5 L21,10 16,15 M21,10 L10,10 C2,10 2,21 10,21",
        ["copy"] = "M8,8 L20,8 20,21 8,21 Z M16,8 L16,3 3,3 3,16 8,16",
        ["save"] = "M4,3 L17,3 21,7 21,21 3,21 3,3 Z M8,3 L8,10 16,10 16,3 M7,21 L7,14 17,14 17,21",
        ["open"] = "M3,19 L3,5 10,5 12,8 20,8 20,11 M3,19 L7,11 22,11 18,19 Z",
        ["pin"] = "M8,3 L18,3 M10,3 L10,10 6,14 20,14 16,10 16,3 M13,14 L13,22",
        ["settings"] = "M12,8 A4,4 0 1 1 12,16 A4,4 0 1 1 12,8 M9,3 L15,3 16,6 19,7 21,12 19,17 16,18 15,21 9,21 8,18 5,17 3,12 5,7 8,6 Z",
        ["clock"] = "M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12 M12,6 L12,12 16,14",
        ["monitor"] = "M3,4 L21,4 21,17 3,17 Z M12,17 L12,21 M7,21 L17,21",
        ["help"] = "M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12 M9,9 C9,4 17,6 15,10 L12,13 M12,17 L12,17.2",
        ["trash"] = "M3,6 L21,6 M9,6 L9,3 15,3 15,6 M6,6 L7,21 17,21 18,6 M10,10 L10,17 M14,10 L14,17",
        ["fit"] = "M8,3 L3,3 3,8 M16,3 L21,3 21,8 M21,16 L21,21 16,21 M8,21 L3,21 3,16",
        ["history"] = "M3,10 C4,1 19,1 21,10 C24,22 6,26 3,16 M3,4 L3,10 9,10 M12,7 L12,13 16,15",
        ["chevron"] = "M6,9 L12,15 18,9"
    };
    protected override Size MeasureOverride(Size availableSize) => new(double.IsNaN(Width) ? 20 : Width, double.IsNaN(Height) ? 20 : Height);
    protected override void OnRender(DrawingContext dc)
    {
        if (!Paths.TryGetValue(Kind.ToLowerInvariant(), out string? data)) data = Paths["capture"];
        dc.PushTransform(new ScaleTransform(ActualWidth / 24, ActualHeight / 24));
        dc.DrawGeometry(null, new Pen(Stroke, 1.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round }, Geometry.Parse(data)); dc.Pop();
    }
}
