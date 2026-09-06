using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnipStudio.Services;

namespace SnipStudio.Diagnostics;

internal static class MemoryProbe
{
    internal static void Run(string directory)
    {
        string captures = Path.Combine(directory, "Captures");
        Directory.CreateDirectory(captures);
        if (Directory.EnumerateFiles(captures).Any()) throw new InvalidOperationException("Use an empty data directory for the memory probe.");
        var sample = DemoFactory.Create();
        for (int i = 0; i < 29; i++) ImageFiles.Save(sample, Path.Combine(captures, $"sample-{i:00}.png"));
        var narrow = BitmapSource.Create(20, 3600, 96, 96, PixelFormats.Bgra32, null, new byte[20 * 3600 * 4], 80);
        narrow.Freeze(); ImageFiles.Save(narrow, Path.Combine(captures, "narrow.png"));
        Collect();
        var history = new HistoryStore();
        long thumbnailPixels = history.Entries.Sum(e => (long)e.Thumbnail.PixelWidth * e.Thumbnail.PixelHeight);
        Collect();
        var loaded = Snapshot();
        long before = GC.GetTotalAllocatedBytes(true);
        var watch = Stopwatch.StartNew();
        string active = history.Entries[0].Path;
        for (int i = 0; i < 8; i++) history.Update(active, sample);
        watch.Stop();
        long allocated = GC.GetTotalAllocatedBytes(true) - before;
        var afterEdits = Snapshot();
        Collect();
        var settled = Snapshot();
        GC.KeepAlive(history);
        File.WriteAllText(Path.Combine(directory, "memory-probe.json"), JsonSerializer.Serialize(new
        {
            ThumbnailPixels = thumbnailPixels, AllocatedBytesForEightSaves = allocated,
            EightSavesMilliseconds = watch.ElapsedMilliseconds, Loaded = loaded, AfterEdits = afterEdits, Settled = settled
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static object Snapshot()
    {
        using var process = Process.GetCurrentProcess();
        return new { WorkingSetBytes = process.WorkingSet64, PrivateBytes = process.PrivateMemorySize64, ManagedBytes = GC.GetTotalMemory(false) };
    }
    private static void Collect() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); }
}
