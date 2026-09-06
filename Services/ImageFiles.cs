using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SnipStudio.Services;

public static class ImageFiles
{
    public static BitmapSource Load(string path, int thumbnailWidth = 0)
    {
        using var stream = File.OpenRead(path);
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        if (thumbnailWidth > 0) image.DecodePixelWidth = thumbnailWidth;
        image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
    }
    public static void Save(BitmapSource image, string path)
    {
        string extension = Path.GetExtension(path).ToLowerInvariant();
        BitmapEncoder encoder = extension switch { ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 }, ".bmp" => new BmpBitmapEncoder(), _ => new PngBitmapEncoder() };
        if (encoder is JpegBitmapEncoder)
        {
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) { var bounds = new Rect(0, 0, image.PixelWidth, image.PixelHeight); dc.DrawRectangle(Brushes.White, null, bounds); dc.DrawImage(image, bounds); }
            var white = new RenderTargetBitmap(image.PixelWidth, image.PixelHeight, 96, 96, PixelFormats.Pbgra32); white.Render(visual); white.Freeze(); image = white;
        }
        encoder.Frames.Add(BitmapFrame.Create(image));
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = File.Create(temp)) encoder.Save(stream);
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static async Task CopyAsync(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var png = new MemoryStream(); encoder.Save(png); png.Position = 0;
        var data = new DataObject(); data.SetImage(image); data.SetData("PNG", png);
        for (int attempt = 0; ; attempt++)
        {
            try { Clipboard.SetDataObject(data, true); return; }
            catch (ExternalException) when (attempt < 5) { await Task.Delay(60 * (attempt + 1)); }
        }
    }
}
