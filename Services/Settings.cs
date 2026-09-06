using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Win32;
using SnipStudio.Editor;

namespace SnipStudio.Services;

public sealed class AppSettings
{
    public uint HotkeyModifiers { get; set; } = 7; // Ctrl + Alt + Shift; Windows key shortcuts are reserved.
    public uint HotkeyKey { get; set; } = 0x53;
    public bool CloseToTray { get; set; } = true;
    public bool CopyAfterCapture { get; set; } = true;
    public bool KeepHistory { get; set; } = true;
    public int HistoryLimit { get; set; } = 30;
    public bool IncludeCursor { get; set; }
    public int DelaySeconds { get; set; }
    public string CaptureMode { get; set; } = "Region";
    public string SaveDirectory { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
    public string AnnotationColor { get; set; } = AnnotationColors.Default;
    public double StrokeWidth { get; set; } = AnnotationColors.DefaultWidth;
    public int PaletteVersion { get; set; }
    public double TextSize { get; set; } = 26;

    public AppSettings Clone() => (AppSettings)MemberwiseClone();
    public static AppSettings Load()
    {
        var path = Path.Combine(App.DataDirectory, "settings.json");
        try
        {
            if (!File.Exists(path)) return new();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new();
            if (settings.PaletteVersion < 1)
            {
                bool oldDefault = string.Equals(settings.AnnotationColor, "#F16B55", StringComparison.OrdinalIgnoreCase);
                settings.AnnotationColor = AnnotationColors.Upgrade(settings.AnnotationColor);
                if (oldDefault && settings.StrokeWidth == 4) settings.StrokeWidth = AnnotationColors.DefaultWidth;
                settings.PaletteVersion = 1;
            }
            settings.HistoryLimit = Math.Clamp(settings.HistoryLimit, 5, 100);
            settings.DelaySeconds = new[] { 0, 3, 5, 10 }.Contains(settings.DelaySeconds) ? settings.DelaySeconds : 0;
            if (!new[] { "Region", "Window", "Screen", "All displays" }.Contains(settings.CaptureMode)) settings.CaptureMode = "Region";
            settings.StrokeWidth = double.IsFinite(settings.StrokeWidth) ? Math.Clamp(settings.StrokeWidth, 1, 24) : AnnotationColors.DefaultWidth;
            settings.TextSize = double.IsFinite(settings.TextSize) ? Math.Clamp(settings.TextSize, 12, 72) : 26;
            if (!HotkeyService.IsValid(settings.HotkeyModifiers, settings.HotkeyKey)) { settings.HotkeyModifiers = 7; settings.HotkeyKey = 0x53; }
            return settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            App.StartupNotice = "Settings couldn't be read. Defaults are active; your captures are still saved.";
            return new();
        }
    }
    public void Save()
    {
        Directory.CreateDirectory(App.DataDirectory);
        string path = Path.Combine(App.DataDirectory, "settings.json");
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
    }
}

public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue("SnipStudio") is string;
    }
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            string exe = Environment.ProcessPath ?? throw new InvalidOperationException("Couldn't locate Snip Studio.");
            if (!exe.EndsWith("SnipStudio.exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Run SnipStudio.exe directly before enabling startup.");
            key.SetValue("SnipStudio", $"\"{exe}\" --background");
        }
        else key.DeleteValue("SnipStudio", false);
    }
}
