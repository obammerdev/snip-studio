using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using SnipStudio.Services;

namespace SnipStudio;

public sealed class CaptureOverlay : Form
{
    private readonly DesktopFrame _frame;
    private readonly string _mode;
    private readonly CaptureBitmap _original, _dimmed;
    private readonly Font _heading = new("Segoe UI", 16, FontStyle.Bold, GraphicsUnit.Pixel);
    private readonly Font _detail = new("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
    private IntPtr _borderBrush = CreateSolidBrush(0x00CAEF7C);
    private PixelRect _selection;
    private Point _start, _pointer;
    private bool _dragging;
    private string _windowTitle = "";
    private long _selectionChanged;
    private readonly List<double> _paintLatencies = [];
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public BitmapSource? Result { get; private set; }
    internal IReadOnlyList<double> PaintLatencies => _paintLatencies;

    public CaptureOverlay(DesktopFrame frame, string mode)
    {
        _frame = frame; _mode = mode;
        try
        {
            if (_borderBrush == IntPtr.Zero) throw new Win32Exception();
            _original = new CaptureBitmap(frame.Image, dim: false);
            _dimmed = new CaptureBitmap(frame.Image, dim: true);
        }
        catch { Dispose(); throw; }
        Text = "Snip Studio · Select capture";
        FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None; ShowInTaskbar = App.IsTestMode; TopMost = true;
        Cursor = Cursors.Cross; KeyPreview = true; BackColor = Color.Black;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque, true);
        // No full-desktop double buffer. Cached DIBs and the native paint clip supply the pixels.
        Bounds = new Rectangle(frame.Bounds.X, frame.Bounds.Y, frame.Bounds.Width, frame.Bounds.Height);
        NativeDesktop.GetCursorPos(out var pointer); _pointer = new(pointer.X, pointer.Y);
    }
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e); Position(); Activate(); Focus(); UpdateHover(); Update(); Trace("ready");
    }
    private void Position() => NativeDesktop.SetWindowPos(Handle, new IntPtr(-1), _frame.Bounds.X, _frame.Bounds.Y, _frame.Bounds.Width, _frame.Bounds.Height, 0x0040);
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x02E0) { Position(); return; } // Stay in physical pixels across mixed-DPI displays.
        base.WndProc(ref m);
    }
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Right) { Close(); return; }
        if (e.Button != MouseButtons.Left) return;
        _pointer = DesktopPoint(e.Location); Trace("down");
        if (_mode != "Region") { UpdateHover(); Finish(); return; }
        _start = _pointer; _dragging = true; Capture = true;
        SetSelection(new(_pointer.X, _pointer.Y, 0, 0));
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var oldHint = HintBounds();
        _pointer = DesktopPoint(e.Location);
        if (_dragging) SetSelection(DragBounds(_start, _pointer, ModifierKeys.HasFlag(Keys.Shift), _frame.Bounds));
        else UpdateHover();
        var hint = HintBounds();
        if (hint != oldHint) { Invalidate(oldHint); Invalidate(hint); }
        // Paint the latest input immediately; avoid queued full-desktop redraws.
        Update();
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_dragging || e.Button != MouseButtons.Left) return;
        _pointer = DesktopPoint(e.Location);
        // The release event is authoritative, including fast drags with no intervening move.
        SetSelection(DragBounds(_start, _pointer, ModifierKeys.HasFlag(Keys.Shift), _frame.Bounds));
        _dragging = false; Capture = false;
        if (_selection.Width >= 2 && _selection.Height >= 2) Finish();
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode == Keys.Escape) { e.Handled = true; Close(); }
        else if (e.KeyCode == Keys.Enter && _selection.Width >= 2 && _selection.Height >= 2) { e.Handled = true; Finish(); }
    }
    private Point DesktopPoint(Point point) => new(point.X + _frame.Bounds.X, point.Y + _frame.Bounds.Y);
    internal static PixelRect DragBounds(Point start, Point end, bool square, PixelRect bounds)
    {
        int x = Math.Clamp(end.X, bounds.X, bounds.Right), y = Math.Clamp(end.Y, bounds.Y, bounds.Bottom);
        if (square) { int side = Math.Min(Math.Abs(x - start.X), Math.Abs(y - start.Y)); x = start.X + Math.Sign(x - start.X) * side; y = start.Y + Math.Sign(y - start.Y) * side; }
        return PixelRect.Between(start.X, start.Y, x, y).Intersect(bounds);
    }
    private void UpdateHover()
    {
        if (_mode == "Screen") SetSelection((_frame.Displays.FirstOrDefault(d => d.Bounds.Contains(_pointer.X, _pointer.Y)) ?? _frame.Displays[0]).Bounds);
        if (_mode == "Window")
        {
            var window = _frame.Windows.FirstOrDefault(w => w.Bounds.Contains(_pointer.X, _pointer.Y));
            var title = window?.Title ?? "Move over a window";
            if (_windowTitle != title) { Invalidate(HintBounds()); _windowTitle = title; }
            SetSelection(window?.Bounds ?? new PixelRect());
        }
    }
    private Rectangle LocalSelection(PixelRect p) => new(p.X - _frame.Bounds.X, p.Y - _frame.Bounds.Y, p.Width, p.Height);
    private void SetSelection(PixelRect selection)
    {
        if (selection == _selection) return;
        var before = LocalSelection(_selection); var after = LocalSelection(selection);
        var oldLabel = SizeLabelBounds(before);
        using var dirty = SelectionDamage(before, after);
        _selection = selection;
        dirty.Union(oldLabel); dirty.Union(SizeLabelBounds(after));
        Invalidate(dirty);
        _selectionChanged = Stopwatch.GetTimestamp();
    }
    internal static Region SelectionDamage(Rectangle before, Rectangle after)
    {
        var dirty = new Region(before); dirty.Xor(after);
        AddBorder(dirty, before); AddBorder(dirty, after); return dirty;
    }
    private static void AddBorder(Region region, Rectangle r)
    {
        if (r.Width < 1 || r.Height < 1) return;
        region.Union(new Rectangle(r.Left - 3, r.Top - 3, r.Width + 6, 6));
        region.Union(new Rectangle(r.Left - 3, r.Bottom - 3, r.Width + 6, 6));
        region.Union(new Rectangle(r.Left - 3, r.Top - 3, 6, r.Height + 6));
        region.Union(new Rectangle(r.Right - 3, r.Top - 3, 6, r.Height + 6));
    }
    private Rectangle SizeLabelBounds(Rectangle r)
    {
        if (r.Width < 1 || r.Height < 1) return Rectangle.Empty;
        const int width = 180, height = 29;
        int x = Math.Clamp(r.Left, 8, Math.Max(8, _frame.Bounds.Width - width - 8));
        int y = Math.Clamp(r.Top > 42 ? r.Top - height - 7 : r.Bottom + 8, 8, Math.Max(8, _frame.Bounds.Height - height - 8));
        return new Rectangle(x, y, width, height);
    }
    private Rectangle HintBounds()
    {
        var display = _frame.Displays.FirstOrDefault(d => d.Bounds.Contains(_pointer.X, _pointer.Y)) ?? _frame.Displays[0];
        const int width = 340;
        return new Rectangle(display.Bounds.X - _frame.Bounds.X + (display.Bounds.Width - width) / 2, display.Bounds.Y - _frame.Bounds.Y + 28, width, _mode == "Window" ? 120 : 78);
    }
    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnPaint(PaintEventArgs e)
    {
        PaintPreview(e.Graphics, e.ClipRectangle);
        if (_selectionChanged != 0)
        {
            if (_paintLatencies.Count < 1000) _paintLatencies.Add(Stopwatch.GetElapsedTime(_selectionChanged).TotalMilliseconds);
            _selectionChanged = 0;
        }
        base.OnPaint(e);
    }
    internal void PaintPreview(Graphics graphics, Rectangle clip)
    {
        var selection = LocalSelection(_selection);
        var dc = graphics.GetHdc();
        try
        {
            _dimmed.CopyTo(dc, clip); _original.CopyTo(dc, Rectangle.Intersect(clip, selection));
            if (selection.Width > 0 && selection.Height > 0)
            {
                var edge = new NativeRect { Left = selection.Left, Top = selection.Top, Right = selection.Right + 1, Bottom = selection.Bottom + 1 };
                FrameRect(dc, ref edge, _borderBrush);
                edge.Left--; edge.Top--; edge.Right++; edge.Bottom++; FrameRect(dc, ref edge, _borderBrush);
            }
        }
        finally { graphics.ReleaseHdc(dc); }
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        if (selection.Width > 0 && selection.Height > 0)
        {
            // GDI+ antialiasing a desktop-sized rectangle causes an expensive readback.
            // The pixel-aligned outline above uses native GDI without that intermediate surface.
            var label = SizeLabelBounds(selection); Panel(graphics, label);
            TextRenderer.DrawText(graphics, $"{_selection.Width:N0} × {_selection.Height:N0} px", _detail, label, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
        var hint = HintBounds(); Panel(graphics, hint);
        string action = _mode switch { "Window" => "Click a window to capture", "Screen" => "Click a display to capture", _ => "Drag to capture an area" };
        string help = _mode == "Region" ? "Shift  square    ·    Esc  cancel" : "Esc or right-click  cancel";
        TextRenderer.DrawText(graphics, action, _heading, new Rectangle(hint.X, hint.Y + 14, hint.Width, 24), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(graphics, help, _detail, new Rectangle(hint.X, hint.Y + 45, hint.Width, 20), Color.FromArgb(175, 199, 194), TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        if (_mode == "Window") TextRenderer.DrawText(graphics, _windowTitle, _detail, new Rectangle(hint.X + 12, hint.Y + 88, hint.Width - 24, 22), Color.White, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
    }
    private static void Panel(Graphics graphics, Rectangle r)
    {
        using var brush = new SolidBrush(Color.FromArgb(18, 38, 40));
        using var path = new GraphicsPath(); const int d = 12;
        path.AddArc(r.Left, r.Top, d, d, 180, 90); path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90); path.CloseFigure();
        graphics.FillPath(brush, path);
    }
    private void Finish()
    {
        if (_selection.Width < 2 || _selection.Height < 2) return;
        Trace("finish"); Result = NativeDesktop.Crop(_frame, _selection); Close();
    }
    internal void SelectForProbe(PixelRect selection, bool finish = false)
    {
        SetSelection(selection.Intersect(_frame.Bounds)); Update();
        if (finish) Finish();
    }
    private void Trace(string action)
    {
        if (!App.IsTestMode) return;
        var data = new { action, pointerX = _pointer.X, pointerY = _pointer.Y, selection = _selection, surfaceWidth = ClientSize.Width, surfaceHeight = ClientSize.Height, dpi = DeviceDpi / 96d, window = NativeDesktop.WindowBounds(Handle), frame = _frame.Bounds };
        System.IO.File.AppendAllText(System.IO.Path.Combine(App.DataDirectory, "capture-diagnostics.jsonl"), System.Text.Json.JsonSerializer.Serialize(data) + Environment.NewLine);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _original?.Dispose(); _dimmed?.Dispose(); _heading.Dispose(); _detail.Dispose();
            if (_borderBrush != IntPtr.Zero) { DeleteObject(_borderBrush); _borderBrush = IntPtr.Zero; }
        }
        base.Dispose(disposing);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] private static extern int FrameRect(IntPtr dc, ref NativeRect rect, IntPtr brush);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateSolidBrush(uint color);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
}
