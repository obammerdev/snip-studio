using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SnipStudio.Editor;

public sealed class EditorSurface : FrameworkElement
{
    private ImageDocument? _document;
    private DrawTool _tool = DrawTool.Pen;
    private Annotation? _pending, _original;
    private DocumentState? _beforeMove;
    private Point _start;
    private bool _dragging, _moved;
    private int _selected = -1;
    private Rect? _crop;
    public Color Color { get; set; } = (Color)ColorConverter.ConvertFromString(AnnotationColors.Default);
    public double StrokeWidth { get; set; } = AnnotationColors.DefaultWidth;
    public double TextSize { get; set; } = 26;
    public double ViewScale { get; set; } = 1;
    public int SelectedIndex => _selected;
    internal bool IsInteracting => _dragging;
    public DrawTool Tool
    {
        get => _tool;
        set { CancelInteraction(); _tool = value; _selected = -1; Cursor = value == DrawTool.Select ? Cursors.Arrow : value == DrawTool.Text ? Cursors.IBeam : Cursors.Cross; InvalidateVisual(); SelectionChanged?.Invoke(); }
    }
    public event Action? SelectionChanged;
    public event Action<Point, Annotation?>? TextRequested;
    public event Action? CropCompleted;
    public event Action<Point>? PointerMoved;
    public ImageDocument? Document
    {
        get => _document;
        set
        {
            CancelInteraction();
            if (_document != null) _document.Changed -= OnChanged;
            _document = value; _selected = -1;
            if (_document != null) _document.Changed += OnChanged;
            OnChanged();
        }
    }
    public EditorSurface() { Focusable = true; ClipToBounds = true; Cursor = Cursors.Cross; RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor); }
    private void OnChanged()
    {
        Width = _document?.Image.PixelWidth ?? 1; Height = _document?.Image.PixelHeight ?? 1;
        if (_selected >= (_document?.Annotations.Count ?? 0)) _selected = -1;
        InvalidateVisual(); SelectionChanged?.Invoke();
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (_document == null) return;
        dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, Width, Height));
        dc.DrawImage(_document.Image, new Rect(0, 0, Width, Height));
        foreach (var annotation in _document.Annotations) annotation.Draw(dc);
        _pending?.Draw(dc);
        if (_selected >= 0 && _selected < _document.Annotations.Count && !_dragging)
        {
            var rect = _document.Annotations[_selected].Bounds; rect.Inflate(6 / ViewScale, 6 / ViewScale);
            var pen = new Pen(new SolidColorBrush(System.Windows.Media.Color.FromRgb(20, 125, 112)), 1.5 / ViewScale) { DashStyle = DashStyles.Dash };
            dc.DrawRectangle(null, new Pen(Brushes.White, 3 / ViewScale), rect); dc.DrawRectangle(null, pen, rect);
        }
        if (_crop is Rect crop)
        {
            var dim = new SolidColorBrush(System.Windows.Media.Color.FromArgb(120, 18, 35, 38));
            dc.DrawRectangle(dim, null, new Rect(0, 0, Width, crop.Top));
            dc.DrawRectangle(dim, null, new Rect(0, crop.Bottom, Width, Math.Max(0, Height - crop.Bottom)));
            dc.DrawRectangle(dim, null, new Rect(0, crop.Top, crop.Left, crop.Height));
            dc.DrawRectangle(dim, null, new Rect(crop.Right, crop.Top, Math.Max(0, Width - crop.Right), crop.Height));
            dc.DrawRectangle(null, new Pen(Brushes.White, 1.5 / ViewScale), crop);
        }
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (_document == null) return;
        Focus(); _start = Clamp(e.GetPosition(this)); _moved = false;
        if (Tool == DrawTool.Select)
        {
            _selected = -1;
            for (int i = _document.Annotations.Count - 1; i >= 0; i--) if (_document.Annotations[i].HitTest(_start, 6 / ViewScale)) { _selected = i; break; }
            SelectionChanged?.Invoke(); InvalidateVisual();
            if (_selected < 0) return;
            if (e.ClickCount == 2 && _document.Annotations[_selected].Tool == DrawTool.Text) { TextRequested?.Invoke(_start, _document.Annotations[_selected]); e.Handled = true; return; }
            _original = _document.Annotations[_selected].Clone(); _beforeMove = _document.Snapshot();
        }
        else if (Tool == DrawTool.Text) { TextRequested?.Invoke(_start, null); e.Handled = true; return; }
        else if (Tool == DrawTool.Step)
        {
            _document.Add(new Annotation { Tool = Tool, Color = Color, FontSize = TextSize, Points = [_start], Number = _document.Annotations.Where(a => a.Tool == DrawTool.Step).Select(a => a.Number).DefaultIfEmpty(0).Max() + 1 }); e.Handled = true; return;
        }
        else if (Tool == DrawTool.Crop) _crop = new Rect(_start, _start);
        else _pending = new Annotation { Tool = Tool, Color = Color, Width = StrokeWidth, FontSize = TextSize, Points = [_start, _start] };
        _dragging = true; CaptureMouse(); e.Handled = true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        var point = Clamp(e.GetPosition(this)); PointerMoved?.Invoke(point);
        if (!_dragging || _document == null) return;
        _moved |= (point - _start).Length > 0.5;
        if (Tool == DrawTool.Select && _selected >= 0 && _original != null)
        {
            var moved = _original.Clone(); moved.Translate(point - _start); _document.Annotations[_selected] = moved;
        }
        else
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                var delta = point - _start;
                if (Tool is DrawTool.Rectangle or DrawTool.Ellipse or DrawTool.Crop)
                {
                    double side = Math.Min(Math.Abs(delta.X), Math.Abs(delta.Y)); point = new Point(_start.X + Math.Sign(delta.X) * side, _start.Y + Math.Sign(delta.Y) * side);
                }
                else if (Tool is DrawTool.Line or DrawTool.Arrow)
                {
                    double angle = Math.Round(Math.Atan2(delta.Y, delta.X) / (Math.PI / 4)) * Math.PI / 4;
                    point = Clamp(_start + new Vector(Math.Cos(angle), Math.Sin(angle)) * delta.Length);
                }
            }
            if (Tool == DrawTool.Crop) _crop = new Rect(_start, point);
            else if (_pending != null)
            {
                if (Tool is DrawTool.Pen or DrawTool.Highlighter) { if ((point - _pending.Points[^1]).Length > 0.5) _pending.Points.Add(point); }
                else _pending.Points[^1] = point;
            }
        }
        InvalidateVisual(); e.Handled = true;
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!_dragging || _document == null) return;
        _dragging = false; ReleaseMouseCapture();
        if (Tool == DrawTool.Select && _moved && _beforeMove != null) _document.CommitFrom(_beforeMove);
        else if (Tool == DrawTool.Crop && _crop is Rect crop && crop.Width >= 2 && crop.Height >= 2) { _document.Crop(crop); CropCompleted?.Invoke(); }
        else if (_pending != null)
        {
            bool valid = Tool is DrawTool.Pen or DrawTool.Highlighter || _pending.Bounds.Width >= 1 || _pending.Bounds.Height >= 1;
            if (Tool is DrawTool.Rectangle or DrawTool.Ellipse or DrawTool.Pixelate or DrawTool.Redact) valid = _pending.Bounds.Width >= 2 && _pending.Bounds.Height >= 2;
            if (valid)
            {
                if (Tool == DrawTool.Pixelate)
                {
                    var rect = ImageDocument.PixelBounds(_pending.Bounds, _document.Image.PixelWidth, _document.Image.PixelHeight);
                    _pending.Points = [new Point(rect.X, rect.Y), new Point(rect.X + rect.Width, rect.Y + rect.Height)];
                    _pending.PixelatedImage = _document.Pixelate(_pending.Bounds);
                }
                _document.Add(_pending);
            }
        }
        _pending = null; _crop = null; _beforeMove = null; _original = null; InvalidateVisual(); e.Handled = true;
    }
    protected override void OnLostMouseCapture(MouseEventArgs e) { if (_dragging) CancelInteraction(); base.OnLostMouseCapture(e); }
    public void CancelInteraction()
    {
        if (_dragging && Tool == DrawTool.Select && _original != null && _document != null && _selected >= 0) _document.Annotations[_selected] = _original;
        _dragging = false; _pending = null; _original = null; _beforeMove = null; _crop = null;
        if (IsMouseCaptured) ReleaseMouseCapture(); InvalidateVisual();
    }
    public void ClearSelection() { CancelInteraction(); _selected = -1; InvalidateVisual(); SelectionChanged?.Invoke(); }
    public void DeleteSelection() { if (_selected >= 0) { int index = _selected; _selected = -1; _document?.Remove(index); } }
    public void ApplyStyleToSelection()
    {
        if (_selected < 0 || _document == null) return;
        var selected = _document.Annotations[_selected];
        if (selected.Color == Color && selected.Width == StrokeWidth && selected.FontSize == TextSize) return;
        _document.Commit(() => { var annotation = _document.Annotations[_selected]; annotation.Color = Color; annotation.Width = StrokeWidth; annotation.FontSize = TextSize; });
    }
    private Point Clamp(Point point) => new(Math.Clamp(point.X, 0, Width), Math.Clamp(point.Y, 0, Height));
}
