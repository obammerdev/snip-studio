using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using SnipStudio.Controls;

namespace SnipStudio.Diagnostics;

internal static class TrayMenuPreview
{
    internal static void Render(string path)
    {
        using var menu = new TrayMenu(() => { }, () => { }, () => { }, () => { }, () => "Ctrl + Alt + Shift + S", () => true);
        menu.CreateControl(); menu.PrepareLayout(); menu.Items[1].Select();
        using var bitmap = new Bitmap(menu.Width, menu.Height);
        menu.DrawToBitmap(bitmap, new Rectangle(Point.Empty, menu.Size));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bitmap.Save(path, ImageFormat.Png);
        if (menu.Items.Cast<System.Windows.Forms.ToolStripItem>().Any(i => i.Bounds.Bottom > menu.ClientSize.Height))
            throw new System.InvalidOperationException("Tray menu items must fit without clipping.");
    }
}
