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
        if (thumbnailWidth > 0) return LoadThumbnail(path, thumbnailWidth, 140, out _, out _);
        using var stream = File.OpenRead(path);
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
    }
    public static BitmapSource LoadThumbnail(string path, int maxWidth, int maxHeight, out int width, out int height)
    {
        if (maxWidth < 1 || maxHeight < 1) throw new ArgumentOutOfRangeException(nameof(maxWidth));
        using var stream = File.OpenRead(path);
        var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
        width = frame.PixelWidth; height = frame.PixelHeight;
        double scale = Math.Min(1, Math.Min((double)maxWidth / width, (double)maxHeight / height));
        int targetWidth = Math.Max(1, (int)Math.Floor(width * scale));
        int targetHeight = Math.Max(1, (int)Math.Floor(height * scale));
        stream.Position = 0;
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = targetWidth; image.DecodePixelHeight = targetHeight;
        image.StreamSource = stream; image.EndInit(); image.Freeze();
        // Keep only the small decoded pixels, not the file decoder or its source buffers.
        var converted = new FormatConvertedBitmap(image, PixelFormats.Pbgra32, null, 0);
        int stride = image.PixelWidth * 4;
        var pixels = new byte[stride * image.PixelHeight]; converted.CopyPixels(pixels, stride, 0);
        var thumbnail = BitmapSource.Create(image.PixelWidth, image.PixelHeight, 96, 96, PixelFormats.Pbgra32, null, pixels, stride);
        thumbnail.Freeze(); return thumbnail;
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
