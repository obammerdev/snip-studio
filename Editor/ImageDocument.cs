using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SnipStudio.Editor;

public sealed record DocumentState(BitmapSource Image, List<Annotation> Annotations, Guid Revision);
public sealed class ImageDocument
{
    private readonly List<DocumentState> _undo = [], _redo = [];
    public BitmapSource Image { get; private set; }
    public List<Annotation> Annotations { get; private set; } = [];
    public Guid Revision { get; private set; } = Guid.NewGuid();
    public Guid SavedRevision { get; set; }
    public bool IsDirty => Revision != SavedRevision;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public event Action? Changed;
    public ImageDocument(BitmapSource image) { Image = Normalize(image); SavedRevision = Revision; }
    public DocumentState Snapshot() => new(Image, Annotations.Select(a => a.Clone()).ToList(), Revision);
    public void Commit(Action change) { var before = Snapshot(); change(); CommitFrom(before); }
    public void CommitFrom(DocumentState before)
    {
        _undo.Add(before);
        if (_undo.Count > 100) _undo.RemoveAt(0);
        _redo.Clear(); Revision = Guid.NewGuid(); Changed?.Invoke();
    }
    private void Restore(DocumentState state) { Image = state.Image; Annotations = state.Annotations.Select(a => a.Clone()).ToList(); Revision = state.Revision; Changed?.Invoke(); }
    public void Undo() { if (!CanUndo) return; _redo.Add(Snapshot()); var state = _undo[^1]; _undo.RemoveAt(_undo.Count - 1); Restore(state); }
    public void Redo() { if (!CanRedo) return; _undo.Add(Snapshot()); var state = _redo[^1]; _redo.RemoveAt(_redo.Count - 1); Restore(state); }
    public void Add(Annotation annotation) => Commit(() => Annotations.Add(annotation));
    public void Remove(int index) { if (index >= 0 && index < Annotations.Count) Commit(() => Annotations.RemoveAt(index)); }
    public BitmapSource Render()
    {
        if (Annotations.Count == 0) return Image;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, Image.PixelWidth, Image.PixelHeight)));
            dc.DrawImage(Image, new Rect(0, 0, Image.PixelWidth, Image.PixelHeight));
            foreach (var annotation in Annotations) annotation.Draw(dc);
            dc.Pop();
        }
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        var bitmap = new RenderTargetBitmap(Image.PixelWidth, Image.PixelHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
    public void Crop(Rect requested)
    {
        var rect = PixelBounds(requested, Image.PixelWidth, Image.PixelHeight);
        if (rect.Width < 2 || rect.Height < 2) return;
        var flattened = Render(); var cropped = new CroppedBitmap(flattened, rect); cropped.Freeze();
        Commit(() => { Image = Normalize(cropped); Annotations.Clear(); });
    }
    public BitmapSource Pixelate(Rect requested)
    {
        var rect = PixelBounds(requested, Image.PixelWidth, Image.PixelHeight);
        if (rect.Width < 1 || rect.Height < 1) throw new ArgumentException("Select an area to pixelate.");
        var source = new FormatConvertedBitmap(new CroppedBitmap(Render(), rect), PixelFormats.Bgra32, null, 0);
        int stride = rect.Width * 4;
        var pixels = new byte[stride * rect.Height]; source.CopyPixels(pixels, stride, 0);
        const int block = 14;
        for (int y = 0; y < rect.Height; y += block)
        for (int x = 0; x < rect.Width; x += block)
        {
            int right = Math.Min(x + block, rect.Width), bottom = Math.Min(y + block, rect.Height);
            long b = 0, g = 0, r = 0, a = 0; int count = (right - x) * (bottom - y);
            for (int yy = y; yy < bottom; yy++) for (int xx = x; xx < right; xx++) { int i = yy * stride + xx * 4; b += pixels[i]; g += pixels[i + 1]; r += pixels[i + 2]; a += pixels[i + 3]; }
            for (int yy = y; yy < bottom; yy++) for (int xx = x; xx < right; xx++) { int i = yy * stride + xx * 4; pixels[i] = (byte)(b / count); pixels[i + 1] = (byte)(g / count); pixels[i + 2] = (byte)(r / count); pixels[i + 3] = (byte)(a / count); }
        }
        var result = BitmapSource.Create(rect.Width, rect.Height, 96, 96, PixelFormats.Bgra32, null, pixels, stride); result.Freeze(); return result;
    }
    public static Int32Rect PixelBounds(Rect rect, int width, int height)
    {
        int x = Math.Clamp((int)Math.Floor(rect.Left), 0, width), y = Math.Clamp((int)Math.Floor(rect.Top), 0, height);
        int right = Math.Clamp((int)Math.Ceiling(rect.Right), x, width), bottom = Math.Clamp((int)Math.Ceiling(rect.Bottom), y, height);
        return new Int32Rect(x, y, right - x, bottom - y);
    }
    public static BitmapSource Normalize(BitmapSource image)
    {
        if (image.PixelWidth < 1 || image.PixelHeight < 1 || (long)image.PixelWidth * image.PixelHeight > 140_000_000) throw new ArgumentException("The image is empty or too large to edit.");
        // Retain pixel dimensions, not a file's arbitrary print-DPI metadata.
        var converted = new FormatConvertedBitmap(image, PixelFormats.Pbgra32, null, 0);
        int stride = image.PixelWidth * 4; var bytes = new byte[stride * image.PixelHeight]; converted.CopyPixels(bytes, stride, 0);
        var normalized = BitmapSource.Create(image.PixelWidth, image.PixelHeight, 96, 96, PixelFormats.Pbgra32, null, bytes, stride); normalized.Freeze(); return normalized;
    }
}
