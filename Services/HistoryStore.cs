using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;

namespace SnipStudio.Services;

public sealed record CaptureEntry(string Path, DateTime Created, BitmapSource Thumbnail, int Width, int Height)
{
    public string Label => Created.Date == DateTime.Today ? Created.ToString("'Today ·' h:mm tt") : Created.ToString("MMM d · h:mm tt");
    public string Dimensions => $"{Width:N0} × {Height:N0}";
}
public sealed class HistoryStore
{
    public string DirectoryPath { get; } = Path.Combine(App.DataDirectory, "Captures");
    public List<CaptureEntry> Entries { get; private set; } = [];
    public HistoryStore() { Directory.CreateDirectory(DirectoryPath); Reload(); }
    public void Reload()
    {
        var entries = new List<CaptureEntry>();
        foreach (var file in new DirectoryInfo(DirectoryPath).EnumerateFiles("*.png").OrderByDescending(f => f.Name).Take(100))
        {
            try
            {
                var thumb = ImageFiles.Load(file.FullName, 200);
                using var stream = file.OpenRead();
                var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
                entries.Add(new(file.FullName, file.CreationTime, thumb, frame.PixelWidth, frame.PixelHeight));
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or System.IO.FileFormatException or ArgumentException) { }
        }
        Entries = entries;
    }
    public string Add(BitmapSource image, int limit)
    {
        string path = Path.Combine(DirectoryPath, $"{DateTime.Now:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.png");
        ImageFiles.Save(image, path); Trim(limit); Reload(); return path;
    }
    public void Update(string path, BitmapSource image)
    {
        if (!Owns(path)) throw new ArgumentException("This capture isn't in the local history.");
        var created = File.Exists(path) ? File.GetCreationTime(path) : DateTime.Now;
        ImageFiles.Save(image, path); File.SetCreationTime(path, created); Reload();
    }
    private bool Owns(string path) => string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFullPath(DirectoryPath), StringComparison.OrdinalIgnoreCase) && string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase);
    public void Trim(int limit)
    {
        foreach (var file in new DirectoryInfo(DirectoryPath).EnumerateFiles("*.png").OrderByDescending(f => f.Name).Skip(Math.Clamp(limit, 5, 100))) file.Delete();
    }
    public void Clear() { foreach (var file in Directory.EnumerateFiles(DirectoryPath, "*.png")) { if (Owns(file)) File.Delete(file); } Reload(); }
}
