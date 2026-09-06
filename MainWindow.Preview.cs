using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnipStudio.Editor;

namespace SnipStudio;

public partial class MainWindow
{
    internal static void CheckScrollControls(System.Action<bool, string> check)
    {
        static void Layout(FrameworkElement element)
        {
            element.Measure(new Size(320, 220)); element.Arrange(new Rect(0, 0, 320, 220)); element.UpdateLayout();
        }
        var scroll = new ScrollViewer { Style = (Style)Application.Current.FindResource("DarkScrollViewer"), Content = new Border { Width = 900, Height = 800 }, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Layout(scroll);
        var vertical = (ScrollBar)scroll.Template.FindName("PART_VerticalScrollBar", scroll);
        var horizontal = (ScrollBar)scroll.Template.FindName("PART_HorizontalScrollBar", scroll);
        check(vertical.IsVisible == horizontal.IsVisible && vertical.ActualWidth == 14 && horizontal.ActualHeight == 14 && horizontal.ActualWidth > 250,
            $"Themed scrollbars reserve usable vertical and horizontal drag targets ({vertical.ActualWidth} × {horizontal.ActualHeight}; horizontal length {horizontal.ActualWidth})");
        scroll.ScrollToVerticalOffset(180); scroll.ScrollToHorizontalOffset(240); scroll.UpdateLayout();
        check(scroll.VerticalOffset == 180 && scroll.HorizontalOffset == 240 && vertical.Value == 180 && horizontal.Value == 240,
            "Themed scrollbar positions follow both content offsets");
        scroll.ScrollToBottom(); scroll.ScrollToRightEnd(); scroll.UpdateLayout();
        check(scroll.VerticalOffset == scroll.ScrollableHeight && scroll.HorizontalOffset == scroll.ScrollableWidth,
            "Themed content can scroll to the last row and right edge");
        scroll.Content = new Border { Width = 40, Height = 40 }; Layout(scroll);
        check(scroll.ComputedVerticalScrollBarVisibility == Visibility.Collapsed && scroll.ComputedHorizontalScrollBarVisibility == Visibility.Collapsed,
            "Scrollbars disappear when the content fits");
        var text = new TextBox { Style = (Style)Application.Current.FindResource(typeof(TextBox)), Text = string.Join("\n", System.Linq.Enumerable.Repeat("A multiline annotation", 40)), AcceptsReturn = true, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Layout(text); text.ScrollToEnd(); text.UpdateLayout();
        check(text.VerticalOffset > 0 && text.ExtentHeight > text.ViewportHeight,
            "Themed text input still scrolls to the end of multiline annotations");
    }
    internal void ShowTrayMenuPreview()
    {
        var point = PointToScreen(new Point(ActualWidth - 345, 90));
        _trayMenu?.Show(new System.Drawing.Point((int)point.X, (int)point.Y));
    }
    internal static void CheckIdleCleanup(System.Action<bool, string> check)
    {
        var window = new MainWindow(false, integrateWithDesktop: false);
        try
        {
            window.LoadDemo();
            var document = window._document!;
            document.Add(new Annotation { Tool = DrawTool.Ellipse, Points = [new(20, 20), new(80, 80)] });
            var revision = document.Revision;
            window.ReleaseIdleImageResources();
            check(window._history.IsSuspended && ReferenceEquals(window.Editor.Document, document) && document.Revision == revision,
                "Editor idle cleanup releases previews while preserving the active editable document");
            document.Undo();
            check(document.Annotations.Count == 0 && document.CanRedo, "Undo remains available after tray idle cleanup");
            document.Redo();
            check(document.Annotations.Count == 1 && document.Revision == revision, "Redo restores annotations after tray idle cleanup");
            window._history.Resume(); window.RefreshHistory();
            check(!window._history.IsSuspended && window._history.Entries.Count > 0, "Editor sidebar reloads after tray idle cleanup");
        }
        finally
        {
            window._exiting = true; window._historyTimer.Stop(); window._statusTimer.Stop(); window._imageIdleTimer.Stop();
            if (window._document != null) window._document.Changed -= window.DocumentChanged;
            window.Editor.Document = null; window.Close();
        }
    }
    internal void ExportPreview(string path)
    {
        var sample = DemoFactory.Create();
        // Enough generated captures to show the sidebar's real scrolling state.
        foreach (int x in new[] { 48, 388, 728 })
            _history.Add(new CroppedBitmap(sample, new Int32Rect(x, 287, 304, 305)), 30);
        LoadDemo();
        _document!.Add(new Annotation { Tool = DrawTool.Ellipse, Width = 5, Points = [new(704, 287), new(1044, 592)] });
        _document.Add(new Annotation { Tool = DrawTool.Arrow, Width = 5, Points = [new(620, 268), new(766, 359)] });
        _document.Add(new Annotation { Tool = DrawTool.Text, Text = "This one!", FontSize = 28, Points = [new(494, 272)] });
        SetTool(DrawTool.Ellipse);
        FlushHistory();
        SetStatus("Sample image · Ready to annotate");
        ShortcutStatus.Text = "Ctrl + Alt + Shift + S";
        // Render the actual WPF client area without reading or displaying the desktop.
        var content = (FrameworkElement)Content;
        ((Panel)content).Background = Background;
        content.Width = 1320; content.Height = 820;
        content.Measure(new Size(1320, 820));
        content.Arrange(new Rect(0, 0, 1320, 820));
        content.UpdateLayout(); FitImage(); content.UpdateLayout();
        SavePreview(content, path, 1320, 820);
        content.Width = 1040; content.Height = 610;
        content.Measure(new Size(1040, 610)); content.Arrange(new Rect(0, 0, 1040, 610)); content.UpdateLayout(); FitImage(); content.UpdateLayout();
        SavePreview(content, Path.ChangeExtension(path, ".compact.png"), 1040, 610);
        using var source = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("Preview messages") { ParentWindow = new System.IntPtr(-3) });
        using var hotkey = new Services.HotkeyService(source.Handle);
        var settings = new SettingsWindow(_settings, hotkey);
        var preferences = (Panel)settings.Content; preferences.Background = settings.Background;
        preferences.Width = 520; preferences.Height = 520;
        preferences.Measure(new Size(520, 520)); preferences.Arrange(new Rect(0, 0, 520, 520)); preferences.UpdateLayout();
        SavePreview(preferences, Path.ChangeExtension(path, ".preferences.png"), 520, 520); settings.Close();
        _historyTimer.Stop(); _statusTimer.Stop(); _imageIdleTimer.Stop();
    }
    private static void SavePreview(FrameworkElement content, string path, int width, int height)
    {
        var bitmap = new RenderTargetBitmap(width * 3 / 2, height * 3 / 2, 144, 144, PixelFormats.Pbgra32);
        bitmap.Render(content); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream);
    }
}
