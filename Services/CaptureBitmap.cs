using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SnipStudio.Services;

// A top-down DIB owned only by the short-lived capture window.
internal sealed class CaptureBitmap : IDisposable
{
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
    {
        public uint Size; public int Width, Height; public ushort Planes, BitCount;
        public uint Compression, ImageSize; public int XPels, YPels; public uint Used, Important;
    }
    private IntPtr _dc, _bitmap, _previous, _pixels;
    internal int Width { get; }
    internal int Height { get; }
    internal CaptureBitmap(BitmapSource source, bool dim)
    {
        Width = source.PixelWidth; Height = source.PixelHeight;
        try
        {
            _dc = CreateCompatibleDC(IntPtr.Zero);
            if (_dc == IntPtr.Zero) throw new Win32Exception();
            var info = new BitmapInfo { Size = (uint)Marshal.SizeOf<BitmapInfo>(), Width = Width, Height = -Height, Planes = 1, BitCount = 32 };
            _bitmap = CreateDIBSection(_dc, ref info, 0, out _pixels, IntPtr.Zero, 0);
            if (_bitmap == IntPtr.Zero || _pixels == IntPtr.Zero) throw new Win32Exception();
            _previous = SelectObject(_dc, _bitmap);
            if (_previous == IntPtr.Zero || _previous == new IntPtr(-1)) throw new Win32Exception();
            int stride = checked(Width * 4);
            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgr32, null, 0);
            converted.CopyPixels(System.Windows.Int32Rect.Empty, _pixels, checked(stride * Height), stride);
            if (dim)
            {
                using var view = new Bitmap(Width, Height, stride, System.Drawing.Imaging.PixelFormat.Format32bppRgb, _pixels);
                using var graphics = Graphics.FromImage(view);
                using var brush = new SolidBrush(System.Drawing.Color.FromArgb(125, 10, 22, 26));
                graphics.FillRectangle(brush, 0, 0, Width, Height);
                graphics.Flush();
            }
        }
        catch { Dispose(); throw; }
    }
    internal void CopyTo(IntPtr target, Rectangle rectangle)
    {
        var area = Rectangle.Intersect(rectangle, new Rectangle(0, 0, Width, Height));
        if (area.Width > 0 && area.Height > 0 && !BitBlt(target, area.X, area.Y, area.Width, area.Height, _dc, area.X, area.Y, 0x00CC0020))
            throw new Win32Exception();
    }
    public void Dispose()
    {
        if (_previous != IntPtr.Zero && _previous != new IntPtr(-1)) { SelectObject(_dc, _previous); _previous = IntPtr.Zero; }
        if (_bitmap != IntPtr.Zero) { DeleteObject(_bitmap); _bitmap = IntPtr.Zero; }
        if (_dc != IntPtr.Zero) { DeleteDC(_dc); _dc = IntPtr.Zero; }
        _pixels = IntPtr.Zero;
    }
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateCompatibleDC(IntPtr source);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern IntPtr SelectObject(IntPtr dc, IntPtr value);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr value);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint operation);
}
