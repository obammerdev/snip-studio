using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace SnipStudio.Services;

public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;
    public static PixelRect Between(int x1, int y1, int x2, int y2) => new(Math.Min(x1, x2), Math.Min(y1, y2), Math.Abs(x2 - x1), Math.Abs(y2 - y1));
    public PixelRect Intersect(PixelRect other)
    {
        int x = Math.Max(X, other.X), y = Math.Max(Y, other.Y);
        return new(x, y, Math.Max(0, Math.Min(Right, other.Right) - x), Math.Max(0, Math.Min(Bottom, other.Bottom) - y));
    }
}
public sealed record DisplayInfo(PixelRect Bounds, bool Primary, string Name);
public sealed record DesktopWindow(PixelRect Bounds, string Title);
public sealed record DesktopFrame(BitmapSource Image, PixelRect Bounds, IReadOnlyList<DisplayInfo> Displays, IReadOnlyList<DesktopWindow> Windows);

public static class NativeDesktop
{
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int Left, Top, Right, Bottom; public readonly PixelRect Pixels => new(Left, Top, Right - Left, Bottom - Top); }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct MONITORINFOEX
    { public int Size; public RECT Monitor, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device; }
    [StructLayout(LayoutKind.Sequential)] private struct CURSORINFO { public int Size; public int Flags; public IntPtr Cursor; public POINT Position; }
    [StructLayout(LayoutKind.Sequential)] private struct ICONINFO { public bool Icon; public uint HotspotX, HotspotY; public IntPtr Mask, Color; }
    private delegate bool MonitorEnum(IntPtr monitor, IntPtr dc, ref RECT rect, IntPtr data);
    private delegate bool WindowEnum(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorEnum callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFOEX info);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowEnum callback, IntPtr data);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out RECT rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int length);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out RECT value, int size);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] private static extern int DwmGetWindowCloaked(IntPtr window, int attribute, out int value, int size);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr handle);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint operation);
    [DllImport("user32.dll")] private static extern bool GetCursorInfo(ref CURSORINFO info);
    [DllImport("user32.dll")] private static extern bool GetIconInfo(IntPtr icon, out ICONINFO info);
    [DllImport("user32.dll")] private static extern bool DrawIconEx(IntPtr dc, int x, int y, IntPtr icon, int width, int height, int step, IntPtr brush, int flags);
    public static List<DisplayInfo> Displays()
    {
        var result = new List<DisplayInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr handle, IntPtr dc, ref RECT rect, IntPtr data) =>
        {
            var info = new MONITORINFOEX { Size = Marshal.SizeOf<MONITORINFOEX>(), Device = "" };
            if (GetMonitorInfo(handle, ref info)) result.Add(new(info.Monitor.Pixels, (info.Flags & 1) != 0, info.Device));
            return true;
        }, IntPtr.Zero);
        if (result.Count == 0) throw new InvalidOperationException("No active displays were found.");
        return result;
    }
    public static PixelRect Union(IReadOnlyList<DisplayInfo> displays)
    {
        int x = displays.Min(d => d.Bounds.X), y = displays.Min(d => d.Bounds.Y);
        return new(x, y, displays.Max(d => d.Bounds.Right) - x, displays.Max(d => d.Bounds.Bottom) - y);
    }
    public static DisplayInfo CursorDisplay()
    {
        GetCursorPos(out var point);
        var displays = Displays();
        return displays.FirstOrDefault(d => d.Bounds.Contains(point.X, point.Y)) ?? displays.First(d => d.Primary);
    }
    public static PixelRect WindowBounds(IntPtr handle) { GetWindowRect(handle, out var rect); return rect.Pixels; }
    public static DesktopFrame Capture(bool includeCursor)
    {
        var displays = Displays();
        var bounds = Union(displays);
        if ((long)bounds.Width * bounds.Height > 140_000_000) throw new InvalidOperationException("This desktop is too large for one capture. Temporarily disable a display and try again.");
        using var bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Black);
            // Graphics.CopyFromScreen rejects combined raster flags. Native BitBlt includes layered windows.
            var sourceDc = GetDC(IntPtr.Zero);
            if (sourceDc == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var targetDc = graphics.GetHdc();
                try { if (!BitBlt(targetDc, 0, 0, bounds.Width, bounds.Height, sourceDc, bounds.X, bounds.Y, 0x40CC0020)) throw new Win32Exception(Marshal.GetLastWin32Error()); }
                finally { graphics.ReleaseHdc(targetDc); }
            }
            finally { ReleaseDC(IntPtr.Zero, sourceDc); }
            if (includeCursor) DrawCursor(graphics, bounds);
        }
        var handle = bitmap.GetHbitmap();
        BitmapSource image;
        try { image = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); image.Freeze(); }
        finally { DeleteObject(handle); }
        var windows = new List<DesktopWindow>();
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window) || IsIconic(window)) return true;
            DwmGetWindowCloaked(window, 14, out int cloaked, sizeof(int));
            if (cloaked != 0) return true;
            var title = new StringBuilder(512);
            if (GetWindowText(window, title, title.Capacity) == 0) return true;
            if (DwmGetWindowAttribute(window, 9, out RECT rect, Marshal.SizeOf<RECT>()) != 0) GetWindowRect(window, out rect);
            var visible = rect.Pixels.Intersect(bounds);
            if (visible.Width > 10 && visible.Height > 10) windows.Add(new(visible, title.ToString()));
            return true;
        }, IntPtr.Zero);
        return new(image, bounds, displays, windows);
    }
    private static void DrawCursor(Graphics graphics, PixelRect bounds)
    {
        var cursor = new CURSORINFO { Size = Marshal.SizeOf<CURSORINFO>() };
        if (!GetCursorInfo(ref cursor) || cursor.Flags != 1 || !GetIconInfo(cursor.Cursor, out var icon)) return;
        try
        {
            var dc = graphics.GetHdc();
            try { DrawIconEx(dc, cursor.Position.X - bounds.X - (int)icon.HotspotX, cursor.Position.Y - bounds.Y - (int)icon.HotspotY, cursor.Cursor, 0, 0, 0, IntPtr.Zero, 3); }
            finally { graphics.ReleaseHdc(dc); }
        }
        finally { if (icon.Mask != IntPtr.Zero) DeleteObject(icon.Mask); if (icon.Color != IntPtr.Zero) DeleteObject(icon.Color); }
    }
    public static BitmapSource Crop(DesktopFrame frame, PixelRect selection)
    {
        var rect = selection.Intersect(frame.Bounds);
        if (rect.Width < 1 || rect.Height < 1) throw new ArgumentException("Select an area inside the desktop.");
        var crop = new CroppedBitmap(frame.Image, new Int32Rect(rect.X - frame.Bounds.X, rect.Y - frame.Bounds.Y, rect.Width, rect.Height));
        crop.Freeze(); return crop;
    }
}
