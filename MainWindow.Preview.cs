using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnipStudio.Editor;

namespace SnipStudio;

public partial class MainWindow
{
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
