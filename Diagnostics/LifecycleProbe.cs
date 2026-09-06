using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnipStudio.Editor;
using SnipStudio.Services;

namespace SnipStudio;

public partial class MainWindow
{
    // Generated pixels, real editor/overlay windows, and the production idle timer.
    // No clipboard writes, global shortcut registration, or personal capture reads.
    internal static async Task RunLifecycleProbeAsync(string directory)
    {
        if (Directory.Exists(Path.Combine(directory, "Captures"))) throw new InvalidOperationException("Use a fresh data directory.");
        var samples = new List<object>();
        var window = new MainWindow(true, integrateWithDesktop: false);
        using var menu = new Controls.TrayMenu(() => { }, () => { }, () => { }, () => { }, () => "Ctrl + Alt + Shift + S", () => true);
        menu.CreateControl();
        void Measure(string stage)
        {
            using var process = Process.GetCurrentProcess();
            samples.Add(new { Stage = stage, WorkingSetBytes = process.WorkingSet64, PrivateBytes = process.PrivateMemorySize64,
                ManagedBytes = GC.GetTotalMemory(false), CpuMilliseconds = process.TotalProcessorTime.TotalMilliseconds });
            File.WriteAllText(Path.Combine(directory, "lifecycle-probe.json"), JsonSerializer.Serialize(samples, new JsonSerializerOptions { WriteIndented = true }));
        }
        try
        {
            await Task.Delay(1000); Measure("background-startup");
            window.Show(); await Task.Delay(1000); Measure("empty-editor");
            for (int cycle = 1; cycle <= 3; cycle++)
            {
                window.Hide(); await Task.Delay(200);
                await CaptureSampleAsync(window);
                window.Reveal(); await Task.Delay(1000); Measure($"capture-{cycle}-editor");
                if (cycle == 3)
                {
                    await Task.Delay(3200); Measure("capture-3-editor-idle");
                    if (window._history.IsSuspended || window._history.Entries.Count == 0 || !ReferenceEquals(window.Editor.Document, window._document))
                        throw new InvalidOperationException("Foreground cleanup must preserve the visible editor and history.");
                }
                window.Hide(); await Task.Delay(4200); Measure($"capture-{cycle}-tray");
            }
            window.Reveal();
            var document = window._document!;
            document.Add(new Annotation { Tool = DrawTool.Ellipse, Points = [new(50, 50), new(300, 200)] });
            var revision = document.Revision;
            window.Hide(); await Task.Delay(4200);
            document.Undo(); document.Redo();
            if (document.Revision != revision) throw new InvalidOperationException("Undo/redo failed after idle cleanup.");
            Measure("edited-tray");
        }
        finally
        {
            window._exiting = true; window._historyTimer.Stop(); window._statusTimer.Stop(); window._imageIdleTimer.Stop();
            window._document?.Changed -= window.DocumentChanged;
            window.Editor.Document = null; window.Close();
        }
    }
    private static async Task CaptureSampleAsync(MainWindow window)
    {
        var displays = NativeDesktop.Displays(); var bounds = NativeDesktop.Union(displays);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) dc.DrawImage(DemoFactory.Create(), new Rect(0, 0, bounds.Width, bounds.Height));
        var bitmap = new RenderTargetBitmap(bounds.Width, bounds.Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual); bitmap.Freeze();
        var frame = new DesktopFrame(bitmap, bounds, displays, Array.Empty<DesktopWindow>());
        var overlay = new CaptureOverlay(frame, "Region");
        try
        {
            overlay.Show(); await Task.Delay(250);
            for (int i = 0; i < 20; i++)
            {
                overlay.SelectForProbe(new PixelRect(bounds.X + 250, bounds.Y + 150, 600 + i * 10, 400));
                await Task.Delay(16);
            }
            overlay.SelectForProbe(new PixelRect(bounds.X + 250, bounds.Y + 150, 800, 450), finish: true);
            window.LoadImage(overlay.Result!, "Memory probe");
        }
        finally { if (overlay.IsVisible) overlay.Close(); }
    }
}
