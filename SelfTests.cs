using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnipStudio.Editor;
using SnipStudio.Services;

namespace SnipStudio;

public static class SelfTests
{
    public static void Run(string directory, bool includeDesktop = true)
    {
        foreach (string name in new[] { "self-test-failed.txt", "self-test-results.json" })
        {
            string path = Path.Combine(directory, name);
            if (File.Exists(path)) File.Delete(path);
        }
        var passed = new List<string>();
        void Check(bool condition, string name) { if (!condition) throw new InvalidOperationException("FAIL: " + name); passed.Add(name); }
        var displays = new[] { new DisplayInfo(new(-1920, 0, 1920, 1080), false, "Left"), new DisplayInfo(new(0, 0, 2560, 1440), true, "Primary"), new DisplayInfo(new(0, -2160, 3840, 2160), false, "Above") };
        Check(NativeDesktop.Union(displays) == new PixelRect(-1920, -2160, 5760, 3600), "Virtual desktop union includes negative monitor origins");
        Check(PixelRect.Between(100, 200, -300, -400) == new PixelRect(-300, -400, 400, 600), "Region supports reverse drags across display boundaries");
        Check(new PixelRect(-200, -100, 500, 400).Intersect(new PixelRect(0, 0, 1920, 1080)) == new PixelRect(0, 0, 300, 300), "Selection clips safely to desktop pixels");
        Check(new PixelRect(0, 0, 100, 100).Intersect(new PixelRect(200, 200, 10, 10)).Width == 0, "Nonoverlapping regions produce empty intersection");
        Check(HotkeyService.IsValid(7, 0x53), "Default capture shortcut is valid");
        Check(!HotkeyService.IsValid(2, 0x43) && !HotkeyService.IsValid(8, 0x53) && !HotkeyService.IsValid(7, 0x7B), "Reject common Ctrl shortcuts, OS shortcuts, and reserved F12");
        Check(HotkeyService.Display(7, 0x53) == "Ctrl + Alt + Shift + S", "Shortcut label matches registration");
        var bytes = new byte[100 * 80 * 4];
        for (int i = 0; i < bytes.Length; i += 4) { bytes[i] = 70; bytes[i + 1] = 150; bytes[i + 2] = 220; bytes[i + 3] = 255; }
        var source = BitmapSource.Create(100, 80, 192, 192, PixelFormats.Bgra32, null, bytes, 400); source.Freeze();
        var document = new ImageDocument(source);
        Check(document.Image.PixelWidth == 100 && document.Image.DpiX == 96, "Normalize print DPI without losing image pixels");
        Check(!document.CanUndo && !document.CanRedo && !document.IsDirty, "New document starts with clean undo state");
        Check(ReferenceEquals(document.Render(), document.Image), "Unedited export reuses image pixels without allocating a full-size render target");
        var box = new Annotation { Tool = DrawTool.Redact, Points = [new(10, 10), new(60, 50)] };
        document.Add(box);
        Check(document.IsDirty && document.CanUndo && document.Annotations.Count == 1, "Annotation creates one undo transaction");
        var rendered = document.Render();
        Check(Pixel(rendered, 25, 25) == (0, 0, 0, 255), "Solid redaction exports truly opaque black pixels");
        Check(Pixel(rendered, 80, 60) == (70, 150, 220, 255), "Rendering preserves pixels outside annotations");
        var renderedPath = Path.Combine(directory, "redaction.png"); ImageFiles.Save(rendered, renderedPath);
        Check(Pixel(ImageFiles.Load(renderedPath), 25, 25) == (0, 0, 0, 255), "PNG roundtrip keeps redaction opaque");
        document.Undo(); Check(document.Annotations.Count == 0 && document.CanRedo && !document.IsDirty, "Undo returns to original revision");
        document.Redo(); Check(document.Annotations.Count == 1, "Redo restores annotation");
        document.Crop(new Rect(5, 5, 70, 60));
        Check(document.Image.PixelWidth == 70 && document.Image.PixelHeight == 60 && document.Annotations.Count == 0, "Crop flattens edits into exact pixel bounds");
        Check(Pixel(document.Image, 20, 20) == (0, 0, 0, 255), "Cropped export retains redaction");
        document.Undo(); Check(document.Image.PixelWidth == 100 && document.Annotations.Count == 1, "Undo crop restores original image and editable annotations");
        document.Undo(); document.Add(new Annotation { Tool = DrawTool.Ellipse, Points = [new(10, 10), new(50, 50)], Width = 4, Color = Colors.Red });
        Check(!document.CanRedo, "New edit after undo discards stale redo branch");
        var ring = document.Annotations[0];
        Check(Pixel(document.Render(), 30, 30) == (70, 150, 220, 255), "Ring leaves its center transparent");
        var before = document.Snapshot(); ring.Translate(new Vector(20, 10)); document.CommitFrom(before); document.Undo();
        Check(document.Annotations[0].Points[0] == new Point(10, 10), "Moving annotations undoes without aliasing mutable points");
        Check(Math.Abs(Annotation.DistanceToSegment(new(5, 3), new(0, 0), new(10, 0)) - 3) < 0.001, "Selection hit test uses segment distance");
        Check(ImageDocument.PixelBounds(new Rect(-5.5, -3.1, 30, 20), 100, 80) == new Int32Rect(0, 0, 25, 17), "Crop pixel rounding clamps and includes touched pixels");
        var pixelated = document.Pixelate(new Rect(0, 0, 50, 40));
        Check(pixelated.PixelWidth == 50 && pixelated.PixelHeight == 40, "Pixelation preserves selected rectangle dimensions");
        Check(Pixel(pixelated, 1, 1) == Pixel(pixelated, 10, 10), "Pixelation averages whole pixel blocks");
        foreach (var tool in Enum.GetValues<DrawTool>().Where(t => t is not (DrawTool.Select or DrawTool.Crop)))
        {
            var test = new ImageDocument(source); test.Add(new Annotation { Tool = tool, Points = [new(15, 15), new(70, 60)], Text = "Test", PixelatedImage = pixelated });
            Check(test.Render().PixelWidth == 100, "Render tool: " + tool);
        }
        var desktopFrame = new DesktopFrame(document.Image, new(-100, -80, 100, 80), [new(new(-100, -80, 100, 80), true, "Virtual")], []);
        var selection = NativeDesktop.Crop(desktopFrame, new(-90, -70, 20, 30));
        Check(selection.PixelWidth == 20 && selection.PixelHeight == 30, "Capture converts negative desktop coordinates into bitmap offsets");
        var history = new HistoryStore();
        for (int i = 0; i < 12; i++) history.Add(source, 10);
        Check(history.Entries.Count == 10, "History prunes oldest captures to retention limit");
        var unchanged = history.Entries[1]; var collection = history.Entries;
        var entry = history.Entries[0]; history.Update(entry.Path, document.Render());
        Check(history.Entries.Count == 10 && history.Entries[0].Path == entry.Path, "Editing history updates capture in place");
        Check(ReferenceEquals(history.Entries[1], unchanged) && ReferenceEquals(history.Entries, collection), "Autosave reuses unchanged previews and the bound history collection");
        Check(!ReferenceEquals(history.Entries[0].Thumbnail, entry.Thumbnail), "Autosave refreshes the edited preview even when file dimensions do not change");
        history.Suspend();
        Check(history.Entries.Count == 0 && history.IsSuspended && File.Exists(entry.Path), "Tray idle releases thumbnail references without deleting captures");
        history.Update(entry.Path, source);
        Check(history.Entries.Count == 0 && history.IsSuspended, "Saving while suspended does not reload the hidden sidebar");
        history.Resume();
        Check(history.Entries.Count == 10 && !history.IsSuspended && history.Entries[0].Path == entry.Path, "Opening the editor restores recent captures after idle cleanup");
        var backgroundHistory = new HistoryStore(load: false);
        Check(backgroundHistory.Entries.Count == 0 && backgroundHistory.IsSuspended, "Background startup skips decoding capture history");
        string narrowPath = Path.Combine(directory, "narrow.png");
        var narrow = BitmapSource.Create(20, 3600, 96, 96, PixelFormats.Bgra32, null, new byte[20 * 3600 * 4], 80);
        ImageFiles.Save(narrow, narrowPath);
        var narrowThumb = ImageFiles.LoadThumbnail(narrowPath, 200, 140, out int narrowWidth, out int narrowHeight);
        Check(narrowWidth == 20 && narrowHeight == 3600 && narrowThumb.PixelWidth <= 200 && narrowThumb.PixelHeight <= 140, "Tall narrow captures produce bounded thumbnails and preserve original dimensions");
        var smallThumb = ImageFiles.LoadThumbnail(entry.Path, 200, 140, out _, out _);
        Check(smallThumb.PixelWidth == source.PixelWidth && smallThumb.PixelHeight == source.PixelHeight, "Small captures are never enlarged for history thumbnails");
        bool rejected = false; try { history.Update(Path.Combine(directory, "outside.png"), source); } catch (ArgumentException) { rejected = true; }
        Check(rejected, "History cannot overwrite paths outside its owned directory");
        foreach (string extension in new[] { "png", "jpg", "bmp" })
        {
            string path = Path.Combine(directory, "export." + extension); ImageFiles.Save(document.Render(), path);
            var loaded = ImageFiles.Load(path); Check(loaded.PixelWidth == 100 && loaded.PixelHeight == 80, extension.ToUpperInvariant() + " export roundtrip keeps full resolution");
        }
        var settings = new AppSettings { DelaySeconds = 5, HotkeyModifiers = 7, HotkeyKey = 0x53 }; settings.Save();
        Check(AppSettings.Load().DelaySeconds == 5, "Settings persist capture countdown");
        new AppSettings { AnnotationColor = "#F16B55", StrokeWidth = 4, PaletteVersion = 0, DelaySeconds = 10, HotkeyKey = 0x4B }.Save();
        var upgraded = AppSettings.Load();
        Check(upgraded.AnnotationColor == AnnotationColors.Default && upgraded.StrokeWidth == 5 && upgraded.DelaySeconds == 10 && upgraded.HotkeyKey == 0x4B, "Palette upgrade strengthens the old default without resetting capture preferences");
        new AppSettings { AnnotationColor = "#81234A", StrokeWidth = 9, PaletteVersion = 0 }.Save();
        var custom = AppSettings.Load();
        Check(custom.AnnotationColor == "#81234A" && custom.StrokeWidth == 9, "Palette upgrade preserves custom annotation colors and widths");
        new AppSettings { AnnotationColor = "#F16B55", PaletteVersion = 1 }.Save();
        Check(AppSettings.Load().AnnotationColor == "#F16B55", "Palette migration runs only once and allows later custom choices");
        File.WriteAllText(Path.Combine(directory, "settings.json"), "{broken");
        Check(AppSettings.Load().HotkeyKey == 0x53, "Corrupt settings recover with safe defaults");
        new AppSettings().Save();
        MainWindow.CheckIdleCleanup(Check);
        var skipped = new List<string>();
        IReadOnlyList<DisplayInfo> realDisplays = Array.Empty<DisplayInfo>();
        if (includeDesktop)
        {
        using var host1 = new HwndSource(new HwndSourceParameters("SnipStudioHotkeyTest1") { Width = 1, Height = 1, WindowStyle = 0 });
        using var host2 = new HwndSource(new HwndSourceParameters("SnipStudioHotkeyTest2") { Width = 1, Height = 1, WindowStyle = 0 });
        using var first = new HotkeyService(host1.Handle); using var second = new HotkeyService(host2.Handle);
        uint key1 = 0, key2 = 0;
        for (uint key = 0x70; key <= 0x7A; key++) if (first.TrySet(7, key, out _)) { key1 = key; break; }
        for (uint key = key1 + 1; key <= 0x7A; key++) if (second.TrySet(7, key, out _)) { key2 = key; break; }
        Check(key1 != 0 && key2 != 0, "Global hotkeys register with the actual Windows API");
        Check(!first.TrySet(7, key2, out _) && first.Registered, "Conflicting hotkey is rejected while previous registration stays active");
        Check(first.TrySet(7, key1, out _), "Existing hotkey survives failed replacement");
        realDisplays = NativeDesktop.Displays();
        Check(realDisplays.Count >= 1, "Native display enumeration finds connected monitors");
        var liveFrame = NativeDesktop.Capture(false);
        Check(liveFrame.Image.PixelWidth == NativeDesktop.Union(realDisplays).Width && liveFrame.Image.PixelHeight == NativeDesktop.Union(realDisplays).Height, "Live desktop capture reads every connected display at physical resolution");
        }
        else skipped.AddRange(["Native hotkey registration", "Native hotkey conflict", "Native hotkey preservation", "Native display enumeration", "Live desktop capture"]);
        var demo = DemoFactory.Create(); ImageFiles.Save(demo, Path.Combine(directory, "sample.png"));
        File.WriteAllText(Path.Combine(directory, "self-test-results.json"), JsonSerializer.Serialize(new { Passed = passed.Count, Tests = passed, Skipped = skipped, Displays = realDisplays }, new JsonSerializerOptions { WriteIndented = true }));
    }
    private static (byte B, byte G, byte R, byte A) Pixel(BitmapSource image, int x, int y)
    {
        var formatted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0); var pixel = new byte[4]; formatted.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0); return (pixel[0], pixel[1], pixel[2], pixel[3]);
    }
}
