using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnipStudio.Services;

namespace SnipStudio;

public sealed class CaptureOverlay : Window
{
    private readonly DesktopFrame _frame;
    private readonly string _mode;
    private readonly OverlaySurface _surface;
    private PixelRect _selection;
    private NativeDesktop.POINT _start, _pointer;
    private bool _dragging;
    private string _windowTitle = "";
    public BitmapSource? Result { get; private set; }

    public CaptureOverlay(DesktopFrame frame, string mode)
    {
        _frame = frame; _mode = mode;
        Title = "Snip Studio · Select capture";
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = App.IsTestMode; Topmost = true;
        Background = Brushes.Black; Cursor = Cursors.Cross; Focusable = true;
        _surface = new OverlaySurface(this); Content = _surface;
        NativeDesktop.GetCursorPos(out _pointer);
        PreviewMouseLeftButtonDown += BeginSelection;
        PreviewMouseMove += MoveSelection;
        PreviewMouseLeftButtonUp += EndSelection;
        PreviewMouseRightButtonDown += (_, _) => Close();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } if (e.Key == Key.Enter && _selection.Width > 1) Finish(); };
        Loaded += (_, _) => { Position(); Activate(); Focus(); UpdateHover(); Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ContextIdle, () => Trace("ready")); };
    }
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e); Position();
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook((IntPtr h, int m, IntPtr w, IntPtr l, ref bool handled) =>
        {
            // One physical-pixel desktop surface. Keep the window spanning the entire virtual desktop.
            if (m == 0x02E0) { handled = true; Position(); }
            return IntPtr.Zero;
        });
    }
    private void Position() => NativeDesktop.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), _frame.Bounds.X, _frame.Bounds.Y, _frame.Bounds.Width, _frame.Bounds.Height, 0x0040);
    private void BeginSelection(object sender, MouseButtonEventArgs e)
    {
        _pointer = EventPosition(e);
        Trace("down", e.GetPosition(_surface));
        if (_mode != "Region") { UpdateHover(); Finish(); return; }
        _start = _pointer; _dragging = true; Mouse.Capture(_surface);
        _selection = new(_pointer.X, _pointer.Y, 0, 0); _surface.InvalidateVisual(); e.Handled = true;
    }
    private void MoveSelection(object sender, MouseEventArgs e)
    {
        _pointer = EventPosition(e);
        if (_dragging)
        {
            int x = Math.Clamp(_pointer.X, _frame.Bounds.X, _frame.Bounds.Right), y = Math.Clamp(_pointer.Y, _frame.Bounds.Y, _frame.Bounds.Bottom);
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                int side = Math.Min(Math.Abs(x - _start.X), Math.Abs(y - _start.Y));
                x = _start.X + Math.Sign(x - _start.X) * side; y = _start.Y + Math.Sign(y - _start.Y) * side;
            }
            _selection = PixelRect.Between(_start.X, _start.Y, x, y).Intersect(_frame.Bounds);
        }
        else UpdateHover();
        _surface.InvalidateVisual();
    }
    private void EndSelection(object sender, MouseButtonEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false; Mouse.Capture(null);
        if (_selection.Width >= 2 && _selection.Height >= 2) Finish();
        e.Handled = true;
    }
    private NativeDesktop.POINT EventPosition(MouseEventArgs e)
    {
        // Use the position carried by the WPF input event. Polling GetCursorPos can
        // jump to the final position before queued fast-drag events are processed.
        var point = e.GetPosition(_surface);
        return new NativeDesktop.POINT
        {
            X = _frame.Bounds.X + (int)Math.Round(point.X * _frame.Bounds.Width / Math.Max(1, _surface.ActualWidth)),
            Y = _frame.Bounds.Y + (int)Math.Round(point.Y * _frame.Bounds.Height / Math.Max(1, _surface.ActualHeight))
        };
    }
    private void UpdateHover()
    {
        if (_mode == "Screen") _selection = (_frame.Displays.FirstOrDefault(d => d.Bounds.Contains(_pointer.X, _pointer.Y)) ?? _frame.Displays[0]).Bounds;
        if (_mode == "Window")
        {
            var window = _frame.Windows.FirstOrDefault(w => w.Bounds.Contains(_pointer.X, _pointer.Y));
            _selection = window?.Bounds ?? new PixelRect(); _windowTitle = window?.Title ?? "Move over a window";
        }
        _surface.InvalidateVisual();
    }
    private void Finish()
    {
        if (_selection.Width < 2 || _selection.Height < 2) return;
        Trace("finish");
        Result = NativeDesktop.Crop(_frame, _selection); Close();
    }
    private void Trace(string action, Point? point = null)
    {
        if (!App.IsTestMode) return;
        NativeDesktop.GetCursorPos(out var native);
        var data = new { action, point, nativeX = native.X, nativeY = native.Y, pointerX = _pointer.X, pointerY = _pointer.Y, selection = _selection, surfaceWidth = _surface.ActualWidth, surfaceHeight = _surface.ActualHeight, width = ActualWidth, height = ActualHeight, dpi = VisualTreeHelper.GetDpi(this).DpiScaleX, window = NativeDesktop.WindowBounds(new WindowInteropHelper(this).Handle), frame = _frame.Bounds };
        System.IO.File.AppendAllText(System.IO.Path.Combine(App.DataDirectory, "capture-diagnostics.jsonl"), System.Text.Json.JsonSerializer.Serialize(data) + Environment.NewLine);
    }
    protected override void OnClosed(EventArgs e) { Mouse.Capture(null); base.OnClosed(e); }

    private sealed class OverlaySurface(CaptureOverlay owner) : FrameworkElement
    {
        private static readonly Brush Dim = new SolidColorBrush(Color.FromArgb(125, 10, 22, 26));
        private static readonly Brush Mint = new SolidColorBrush(Color.FromRgb(124, 239, 202));
        protected override void OnRender(DrawingContext dc)
        {
            double sx = ActualWidth / owner._frame.Bounds.Width, sy = ActualHeight / owner._frame.Bounds.Height;
            if (sx <= 0 || sy <= 0) return;
            var full = new Rect(0, 0, ActualWidth, ActualHeight);
            dc.DrawImage(owner._frame.Image, full);
            var p = owner._selection;
            var r = new Rect((p.X - owner._frame.Bounds.X) * sx, (p.Y - owner._frame.Bounds.Y) * sy, p.Width * sx, p.Height * sy);
            if (p.Width < 1 || p.Height < 1) dc.DrawRectangle(Dim, null, full);
            else
            {
                dc.DrawRectangle(Dim, null, new Rect(0, 0, ActualWidth, Math.Max(0, r.Top)));
                dc.DrawRectangle(Dim, null, new Rect(0, r.Bottom, ActualWidth, Math.Max(0, ActualHeight - r.Bottom)));
                dc.DrawRectangle(Dim, null, new Rect(0, r.Top, Math.Max(0, r.Left), r.Height));
                dc.DrawRectangle(Dim, null, new Rect(r.Right, r.Top, Math.Max(0, ActualWidth - r.Right), r.Height));
                dc.DrawRectangle(null, new Pen(Mint, 1.5), r);
                string dimensions = $"{p.Width:N0} × {p.Height:N0} px";
                var text = Text(dimensions, 12, Brushes.White);
                double x = Math.Clamp(r.Left, 8, Math.Max(8, ActualWidth - text.Width - 24));
                double y = r.Top > 40 ? r.Top - 34 : r.Bottom + 8;
                y = Math.Clamp(y, 8, Math.Max(8, ActualHeight - 32));
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(18, 38, 40)), null, new Rect(x, y, text.Width + 20, 27), 5, 5);
                dc.DrawText(text, new Point(x + 10, y + 5));
            }
            var display = owner._frame.Displays.FirstOrDefault(d => d.Bounds.Contains(owner._pointer.X, owner._pointer.Y)) ?? owner._frame.Displays[0];
            string action = owner._mode switch { "Window" => "Click a window to capture", "Screen" => "Click a display to capture", _ => "Drag to capture an area" };
            string hint = owner._mode == "Region" ? "Shift  square    ·    Esc  cancel" : "Esc or right-click  cancel";
            var heading = Text(action, 16, Brushes.White);
            var sub = Text(hint, 11, new SolidColorBrush(Color.FromRgb(175, 199, 194)));
            double width = Math.Max(heading.Width, sub.Width) + 52;
            double left = (display.Bounds.X - owner._frame.Bounds.X + display.Bounds.Width / 2d) * sx - width / 2;
            double top = (display.Bounds.Y - owner._frame.Bounds.Y) * sy + 28;
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(245, 18, 38, 40)), new Pen(new SolidColorBrush(Color.FromRgb(54, 88, 81)), 1), new Rect(left, top, width, 78), 12, 12);
            dc.DrawText(heading, new Point(left + (width - heading.Width) / 2, top + 15));
            dc.DrawText(sub, new Point(left + (width - sub.Width) / 2, top + 43));
            if (owner._mode == "Window" && !string.IsNullOrEmpty(owner._windowTitle))
            {
                var title = Text(owner._windowTitle.Length > 65 ? owner._windowTitle[..65] + "…" : owner._windowTitle, 12, Brushes.White);
                dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(235, 18, 38, 40)), null, new Rect(left, top + 88, Math.Max(width, title.Width + 24), 32), 7, 7);
                dc.DrawText(title, new Point(left + 12, top + 96));
            }
        }
        private FormattedText Text(string value, double size, Brush color) => new(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, color, VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }
}
