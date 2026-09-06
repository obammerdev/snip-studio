using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SnipStudio.Editor;

public enum DrawTool { Select, Pen, Highlighter, Arrow, Line, Rectangle, Ellipse, Text, Step, Pixelate, Redact, Crop }

public sealed class Annotation
{
    public DrawTool Tool { get; set; }
    public List<Point> Points { get; set; } = [];
    public Color Color { get; set; } = (Color)ColorConverter.ConvertFromString(AnnotationColors.Default);
    public double Width { get; set; } = AnnotationColors.DefaultWidth;
    public double FontSize { get; set; } = 26;
    public string Text { get; set; } = "";
    public int Number { get; set; } = 1;
    public BitmapSource? PixelatedImage { get; set; }
    public Annotation Clone() => new() { Tool = Tool, Points = [.. Points], Color = Color, Width = Width, FontSize = FontSize, Text = Text, Number = Number, PixelatedImage = PixelatedImage };
    public Rect Bounds
    {
        get
        {
            if (Points.Count == 0) return Rect.Empty;
            if (Tool == DrawTool.Text)
            {
                var text = Format(Text, FontSize, Brushes.Black);
                return new Rect(Points[0], new Size(Math.Max(1, text.WidthIncludingTrailingWhitespace), text.Height));
            }
            if (Tool == DrawTool.Step) { double radius = Math.Max(16, FontSize * 0.7); return new Rect(Points[0].X - radius, Points[0].Y - radius, radius * 2, radius * 2); }
            double x = Points.Min(p => p.X), y = Points.Min(p => p.Y);
            return new Rect(x, y, Points.Max(p => p.X) - x, Points.Max(p => p.Y) - y);
        }
    }
    public void Translate(Vector delta) { for (int i = 0; i < Points.Count; i++) Points[i] += delta; }
    public bool HitTest(Point point, double tolerance)
    {
        if (Tool is DrawTool.Pen or DrawTool.Highlighter or DrawTool.Arrow or DrawTool.Line)
        {
            double thickness = (Tool == DrawTool.Highlighter ? Width * 4 : Width) / 2 + tolerance;
            if (Points.Count == 1) return (point - Points[0]).Length <= thickness;
            for (int i = 1; i < Points.Count; i++) if (DistanceToSegment(point, Points[i - 1], Points[i]) <= thickness) return true;
            return false;
        }
        var bounds = Bounds; bounds.Inflate(tolerance, tolerance); return bounds.Contains(point);
    }
    public static double DistanceToSegment(Point p, Point a, Point b)
    {
        var ab = b - a;
        if (ab.LengthSquared == 0) return (p - a).Length;
        double t = Math.Clamp(Vector.Multiply(p - a, ab) / ab.LengthSquared, 0, 1);
        return (p - (a + ab * t)).Length;
    }
    public void Draw(DrawingContext dc)
    {
        if (Points.Count == 0) return;
        var brush = new SolidColorBrush(Color);
        var pen = new Pen(brush, Width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        Point first = Points[0], last = Points[^1];
        switch (Tool)
        {
            case DrawTool.Pen:
            case DrawTool.Highlighter:
                if (Tool == DrawTool.Highlighter) { pen.Brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(90, Color.R, Color.G, Color.B)); pen.Thickness = Width * 4; }
                if (Points.Count == 1) { dc.DrawEllipse(pen.Brush, null, first, pen.Thickness / 2, pen.Thickness / 2); break; }
                var path = new StreamGeometry();
                using (var context = path.Open()) { context.BeginFigure(first, false, false); context.PolyLineTo(Points.Skip(1).ToArray(), true, true); }
                dc.DrawGeometry(null, pen, path); break;
            case DrawTool.Line: dc.DrawLine(pen, first, last); break;
            case DrawTool.Arrow:
                var direction = last - first;
                if (direction.Length < 1) break;
                double head = Math.Min(Math.Max(14, Width * 3.5), direction.Length * 0.65);
                direction.Normalize(); var normal = new Vector(-direction.Y, direction.X);
                dc.DrawLine(pen, first, last - direction * head * 0.45);
                var arrow = new StreamGeometry();
                using (var context = arrow.Open()) { context.BeginFigure(last, true, true); context.LineTo(last - direction * head + normal * head * 0.46, true, false); context.LineTo(last - direction * head - normal * head * 0.46, true, false); }
                dc.DrawGeometry(brush, null, arrow); break;
            case DrawTool.Rectangle: dc.DrawRectangle(null, pen, Bounds); break;
            case DrawTool.Ellipse: dc.DrawEllipse(null, pen, new Point(Bounds.X + Bounds.Width / 2, Bounds.Y + Bounds.Height / 2), Bounds.Width / 2, Bounds.Height / 2); break;
            case DrawTool.Redact: dc.DrawRectangle(Brushes.Black, null, Bounds); break;
            case DrawTool.Pixelate:
                if (PixelatedImage != null) dc.DrawImage(PixelatedImage, Bounds);
                else dc.DrawRectangle(new SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 65, 80, 80)), null, Bounds);
                break;
            case DrawTool.Text:
                var formatted = Format(Text, FontSize, brush);
                // A fine light halo keeps labels readable over both light and dark screenshots.
                dc.DrawGeometry(brush, new Pen(new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 255, 255, 255)), 1.5), formatted.BuildGeometry(first)); break;
            case DrawTool.Step:
                double radius = Math.Max(16, FontSize * 0.7);
                dc.DrawEllipse(brush, new Pen(Brushes.White, 2.5), first, radius, radius);
                var number = Format(Number.ToString(CultureInfo.InvariantCulture), radius * 1.1, Brushes.White);
                dc.DrawText(number, new Point(first.X - number.Width / 2, first.Y - number.Height / 2)); break;
        }
    }
    private static FormattedText Format(string text, double size, Brush brush) => new(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal), size, brush, 1);
}
