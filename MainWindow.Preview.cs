using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnipStudio.Editor;

namespace SnipStudio;

public partial class MainWindow
{
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
            window.ReleaseIdlePreviews();
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
            window._exiting = true; window._historyTimer.Stop(); window._statusTimer.Stop(); window._trayIdleTimer.Stop();
            if (window._document != null) window._document.Changed -= window.DocumentChanged;
            window.Editor.Document = null; window.Close();
        }
    }
    internal void ExportPreview(string path)
    {
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
        var bitmap = new RenderTargetBitmap(1980, 1230, 144, 144, PixelFormats.Pbgra32);
        bitmap.Render(content);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var stream = File.Create(path);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); encoder.Save(stream);
        _historyTimer.Stop(); _statusTimer.Stop();
    }
}
