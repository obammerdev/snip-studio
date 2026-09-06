using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;

namespace SnipStudio;

public partial class App : Application
{
    private Mutex? _mutex;
    private EventWaitHandle? _activation;
    private RegisteredWaitHandle? _wait;
    public static string DataDirectory { get; private set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnipStudio");
    public static bool IsTestMode { get; private set; }
    public static string? StartupNotice { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var index = Array.IndexOf(e.Args, "--data-dir");
        if (index >= 0 && index + 1 < e.Args.Length) DataDirectory = Path.GetFullPath(e.Args[index + 1]);
        IsTestMode = e.Args.Contains("--self-test") || e.Args.Contains("--demo");
        Directory.CreateDirectory(DataDirectory);
        if (e.Args.Contains("--memory-probe"))
        {
            if (index < 0) { Shutdown(2); return; }
            try { Diagnostics.MemoryProbe.Run(DataDirectory); Shutdown(0); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(DataDirectory, "memory-probe-failed.txt"), ex.ToString()); Shutdown(1); }
            return;
        }
        if (e.Args.Contains("--self-test"))
        {
            try { SelfTests.Run(DataDirectory, !e.Args.Contains("--headless")); Shutdown(0); }
            catch (Exception ex) { File.WriteAllText(Path.Combine(DataDirectory, "self-test-failed.txt"), ex.ToString()); Shutdown(1); }
            return;
        }
        var previewIndex = Array.IndexOf(e.Args, "--render-preview");
        var trayPreviewIndex = Array.IndexOf(e.Args, "--render-tray-preview");
        if (trayPreviewIndex >= 0 && trayPreviewIndex + 1 < e.Args.Length)
        {
            Diagnostics.TrayMenuPreview.Render(Path.GetFullPath(e.Args[trayPreviewIndex + 1]));
            Shutdown(0); return;
        }
        if (previewIndex >= 0 && previewIndex + 1 < e.Args.Length)
        {
            // Documentation previews use generated content and isolated storage only.
            DataDirectory = Path.Combine(Path.GetTempPath(), "SnipStudioPreview", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(DataDirectory);
            var preview = new MainWindow(false, integrateWithDesktop: false);
            preview.ExportPreview(Path.GetFullPath(e.Args[previewIndex + 1]));
            Shutdown(0);
            return;
        }
        var instanceName = IsTestMode ? "SnipStudio.Demo" : "SnipStudio.Desktop.v1";
        _mutex = new Mutex(true, @"Local\" + instanceName, out bool first);
        _activation = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\" + instanceName + ".Activate");
        if (!first) { _activation.Set(); Shutdown(); return; }
        DispatcherUnhandledException += (_, args) =>
        {
            try { File.AppendAllText(Path.Combine(DataDirectory, "errors.log"), $"{DateTimeOffset.Now:u}\n{args.Exception}\n"); } catch { }
            MessageBox.Show("Snip Studio couldn't finish that action. Your open image is still available.\n\n" + args.Exception.Message, "Snip Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
            args.Handled = true;
        };
        var window = new MainWindow(e.Args.Contains("--background"));
        MainWindow = window;
        _wait = ThreadPool.RegisterWaitForSingleObject(_activation, (_, _) => Dispatcher.BeginInvoke(window.Reveal), null, Timeout.Infinite, false);
        if (!e.Args.Contains("--background")) window.Show();
        if (e.Args.Contains("--demo"))
        {
            window.LoadDemo();
            if (e.Args.Contains("--show-tray-menu")) Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, window.ShowTrayMenuPreview);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _wait?.Unregister(null);
        _activation?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
