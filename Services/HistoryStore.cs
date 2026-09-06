using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    private sealed record CachedEntry(DateTime Modified, long Length, CaptureEntry Entry);
    private Dictionary<string, CachedEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    public bool IsSuspended { get; private set; }
    public string DirectoryPath { get; } = Path.Combine(App.DataDirectory, "Captures");
    public ObservableCollection<CaptureEntry> Entries { get; } = [];
    public HistoryStore(bool load = true) { Directory.CreateDirectory(DirectoryPath); IsSuspended = !load; Reload(); }
    public void Suspend() { IsSuspended = true; Entries.Clear(); _cache.Clear(); }
    public void Resume() { if (!IsSuspended) return; IsSuspended = false; Reload(); }
    public void Reload()
    {
        if (IsSuspended) return;
        var entries = new List<CaptureEntry>();
        var cache = new Dictionary<string, CachedEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in new DirectoryInfo(DirectoryPath).EnumerateFiles("*.png").OrderByDescending(f => f.Name).Take(100))
        {
            try
            {
                if (!_cache.TryGetValue(file.FullName, out var cached) || cached.Modified != file.LastWriteTimeUtc || cached.Length != file.Length)
                {
                    var thumb = ImageFiles.LoadThumbnail(file.FullName, 200, 140, out int width, out int height);
                    cached = new(file.LastWriteTimeUtc, file.Length, new(file.FullName, file.CreationTime, thumb, width, height));
                }
                entries.Add(cached.Entry); cache.Add(file.FullName, cached);
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or System.IO.FileFormatException or ArgumentException) { }
        }
        _cache = cache;
        // Preserve unchanged thumbnails and their WPF item containers during autosave.
        for (int i = 0; i < entries.Count; i++)
        {
            if (i < Entries.Count && Entries[i].Path == entries[i].Path)
            {
                if (!ReferenceEquals(Entries[i], entries[i])) Entries[i] = entries[i];
                continue;
            }
            int existing = -1;
            for (int j = i + 1; j < Entries.Count; j++) if (Entries[j].Path == entries[i].Path) { existing = j; break; }
            if (existing >= 0) { Entries.Move(existing, i); if (!ReferenceEquals(Entries[i], entries[i])) Entries[i] = entries[i]; }
            else Entries.Insert(i, entries[i]);
        }
        while (Entries.Count > entries.Count) Entries.RemoveAt(Entries.Count - 1);
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
        ImageFiles.Save(image, path); File.SetCreationTime(path, created); _cache.Remove(path); Reload();
    }
    private bool Owns(string path) => string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), Path.GetFullPath(DirectoryPath), StringComparison.OrdinalIgnoreCase) && string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase);
    public void Trim(int limit)
    {
        foreach (var file in new DirectoryInfo(DirectoryPath).EnumerateFiles("*.png").OrderByDescending(f => f.Name).Skip(Math.Clamp(limit, 5, 100))) file.Delete();
    }
    public void Clear() { foreach (var file in Directory.EnumerateFiles(DirectoryPath, "*.png")) { if (Owns(file)) File.Delete(file); } Reload(); }
}
