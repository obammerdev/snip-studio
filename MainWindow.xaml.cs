using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using SnipStudio.Controls;
using SnipStudio.Editor;
using SnipStudio.Services;
using Forms = System.Windows.Forms;

namespace SnipStudio;

public partial class MainWindow : Window
{
    private AppSettings _settings = AppSettings.Load();
    private readonly HistoryStore _history;
    private readonly Dictionary<DrawTool, Button> _toolButtons = [];
    private readonly List<PinWindow> _pins = [];
    private readonly DispatcherTimer _historyTimer = new() { Interval = TimeSpan.FromMilliseconds(700) };
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(7) };
    private readonly DispatcherTimer _imageIdleTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private bool _imageWorkSinceIdle;
    private HotkeyService? _hotkey;
    private Forms.NotifyIcon? _tray;
    private TrayMenu? _trayMenu;
    private ImageDocument? _document;
    private string? _historyPath;
    private Guid _historyRevision;
    private string _title = "Untitled capture";
    private string? _exportPath;
    private bool _ready, _exiting, _captureBusy, _instantRequested, _fit = true, _trayNoticeShown, _syncStyle;
    private CancellationTokenSource? _captureCancel;
    private CaptureOverlay? _overlay;
    private int _dialogDepth;

    public MainWindow(bool background, bool integrateWithDesktop = true)
    {
        _history = new HistoryStore(load: !background);
        InitializeComponent();
        Width = Math.Min(Width, SystemParameters.WorkArea.Width - 40); Height = Math.Min(Height, SystemParameters.WorkArea.Height - 40);
        BuildTools(); BuildPalette();
        ModeBox.SelectedIndex = Math.Max(0, Array.IndexOf(new[] { "Region", "Window", "Screen", "All displays" }, _settings.CaptureMode));
        DelayBox.SelectedIndex = Math.Max(0, Array.IndexOf(new[] { 0, 3, 5, 10 }, _settings.DelaySeconds));
        WidthSlider.Value = _settings.StrokeWidth;
        TextSizeBox.SelectedIndex = Math.Max(0, Array.IndexOf(new[] { 18d, 26d, 36d, 48d, 64d }, _settings.TextSize));
        Editor.StrokeWidth = _settings.StrokeWidth; Editor.TextSize = _settings.TextSize;
        WidthLabel.Text = _settings.StrokeWidth.ToString("0");
        try { SetColor((Color)ColorConverter.ConvertFromString(_settings.AnnotationColor)); } catch { SetColor((Color)ColorConverter.ConvertFromString(AnnotationColors.Default)); }
        Editor.SelectionChanged += () => { UpdateActions(); SyncSelectedStyle(); };
        WidthSlider.PreviewMouseLeftButtonUp += (_, _) => Editor.ApplyStyleToSelection();
        WidthSlider.LostKeyboardFocus += (_, _) => Editor.ApplyStyleToSelection();
        CanvasScroll.ScrollChanged += (_, e) => { if (_fit && (e.ViewportWidthChange != 0 || e.ViewportHeightChange != 0)) FitImage(); };
        Editor.TextRequested += AddText;
        Editor.CropCompleted += () => { SetTool(DrawTool.Select); Dispatcher.BeginInvoke(FitImage); SetStatus("Cropped. Ctrl+Z restores the full image."); };
        Editor.PointerMoved += point => { if (_document != null) CanvasInfo.Text = $"{_document.Image.PixelWidth:N0} × {_document.Image.PixelHeight:N0} px   ·   {(int)point.X}, {(int)point.Y}"; };
        _historyTimer.Tick += (_, _) => { _historyTimer.Stop(); FlushHistory(); };
        _statusTimer.Tick += (_, _) => { _statusTimer.Stop(); SetReadyStatus(); };
        _imageIdleTimer.Tick += (_, _) => ReleaseIdleImageResources();
        IsVisibleChanged += (_, _) => UpdateBackgroundState();
        StateChanged += (_, _) => UpdateBackgroundState();
        Loaded += (_, _) => { FitImage(); if (App.StartupNotice != null) SetStatus(App.StartupNotice, true); };
        _ready = true;
        if (!integrateWithDesktop)
        {
            RefreshHistory(); SetTool(DrawTool.Pen); UpdateShortcutLabel(); SetReadyStatus();
            return;
        }
        var handle = new WindowInteropHelper(this).EnsureHandle();
        _hotkey = new HotkeyService(handle); _hotkey.Pressed += () => { if (_dialogDepth == 0) _ = StartCaptureAsync(true); };
        bool registered = _hotkey.TrySet(_settings.HotkeyModifiers, _settings.HotkeyKey, out string shortcutError);
        CreateTray(); RefreshHistory(); SetTool(DrawTool.Pen); UpdateShortcutLabel();
        if (registered) SetReadyStatus(); else SetStatus(shortcutError + " Open Settings to fix it.", true);
    }

    private void BuildTools()
    {
        var tools = new (DrawTool Tool, string Icon, string Name, string Key)[]
        {
            (DrawTool.Select,"select","Select & move","V"), (DrawTool.Pen,"pen","Pen","B"), (DrawTool.Highlighter,"highlighter","Highlighter","H"),
            (DrawTool.Arrow,"arrow","Arrow","A"), (DrawTool.Line,"line","Line","L"), (DrawTool.Rectangle,"rectangle","Rectangle","R"),
            (DrawTool.Ellipse,"ellipse","Ellipse / ring","E"), (DrawTool.Text,"text","Text","T"), (DrawTool.Step,"step","Numbered step","N"),
            (DrawTool.Pixelate,"pixelate","Pixelate","P"), (DrawTool.Redact,"redact","Solid redaction","D"), (DrawTool.Crop,"crop","Crop","C")
        };
        foreach (var (tool, icon, name, key) in tools)
        {
            if (tool is DrawTool.Pen or DrawTool.Pixelate or DrawTool.Crop) ToolRail.Children.Add(new Border { Height = 1, Background = (Brush)FindResource("Line"), Margin = new Thickness(7, 5, 7, 5) });
            var button = new Button { Content = new Icon { Kind = icon, Width = 21, Height = 21 }, ToolTip = $"{name}  {key}", Height = 36, Padding = new Thickness(0), Margin = new Thickness(0, 2, 0, 2), BorderThickness = new Thickness(0), Background = Brushes.Transparent, Tag = tool, IsEnabled = false };
            AutomationProperties.SetName(button, name);
            button.Click += (_, _) => { SetTool(tool); Editor.Focus(); };
            _toolButtons[tool] = button; ToolRail.Children.Add(button);
        }
    }
    private void BuildPalette()
    {
        foreach (string hex in AnnotationColors.Palette)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var button = new Button { Style = (Style)FindResource("SwatchButton"), Width = 27, Height = 27, Margin = new Thickness(0, 0, 1, 0), Padding = new Thickness(0), Background = new SolidColorBrush(color), BorderBrush = Brushes.Transparent, BorderThickness = new Thickness(1.5), ToolTip = hex, Tag = color };
            AutomationProperties.SetName(button, "Color " + hex);
            button.Click += (_, _) => { SetColor(color); Editor.ApplyStyleToSelection(); Editor.Focus(); };
            Palette.Children.Add(button);
        }
    }
    private void SetColor(Color color)
    {
        Editor.Color = color; _settings.AnnotationColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}"; HexColorBox.Text = _settings.AnnotationColor;
        foreach (Button button in Palette.Children) { bool selected = (Color)button.Tag == color; button.BorderBrush = selected ? (Brush)FindResource("Ink") : Brushes.Transparent; }
    }
    private void SyncSelectedStyle()
    {
        if (_syncStyle || _document == null || Editor.SelectedIndex < 0 || Editor.SelectedIndex >= _document.Annotations.Count) return;
        _syncStyle = true;
        try
        {
            var annotation = _document.Annotations[Editor.SelectedIndex];
            SetColor(annotation.Color); Editor.StrokeWidth = annotation.Width; WidthSlider.Value = annotation.Width; WidthLabel.Text = annotation.Width.ToString("0");
            Editor.TextSize = annotation.FontSize;
            foreach (ComboBoxItem item in TextSizeBox.Items) if (double.Parse(item.Tag.ToString()!) == annotation.FontSize) { TextSizeBox.SelectedItem = item; break; }
        }
        finally { _syncStyle = false; }
    }
    private void SetTool(DrawTool tool)
    {
        Editor.Tool = tool;
        foreach (var pair in _toolButtons)
        {
            pair.Value.Background = pair.Key == tool ? (Brush)FindResource("AccentSurface") : Brushes.Transparent;
            ((Icon)pair.Value.Content).Stroke = (Brush)FindResource(pair.Key == tool ? "Accent" : "Muted");
        }
        (ToolTipTitle.Text, ToolHint.Text) = tool switch
        {
            DrawTool.Select => ("Select & move", "Drag an annotation to move it. Delete removes it. Double-click text to edit."),
            DrawTool.Ellipse => ("Ellipse / ring", "Drag to draw a ring. Hold Shift for a circle. Ctrl+Z to undo."),
            DrawTool.Rectangle => ("Rectangle", "Drag to draw an outline. Hold Shift for a square."),
            DrawTool.Arrow or DrawTool.Line => (tool.ToString(), "Drag from start to finish. Hold Shift to snap to 45° angles."),
            DrawTool.Highlighter => ("Highlighter", "Draw a translucent highlight. Adjust the color and width above."),
            DrawTool.Text => ("Text", "Click to add a label. Use Select and double-click a label to edit."),
            DrawTool.Step => ("Numbered steps", "Click to place a marker. Each new marker gets the next number."),
            DrawTool.Pixelate => ("Pixelate", "Drag over an area to pixelate it. Use solid redaction for sensitive information."),
            DrawTool.Redact => ("Solid redaction", "Drag to cover an area in opaque black. The cover is included when you copy or save."),
            DrawTool.Crop => ("Crop", "Drag the area to keep. Release to crop. Ctrl+Z restores the original."),
            _ => ("Pen", "Draw on the image. Change the color or width above. Ctrl+Z to undo.")
        };
    }

    private void CreateTray()
    {
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/SnipStudio.ico"))!.Stream;
        _trayMenu = new TrayMenu(
            () => Dispatcher.Invoke(() => _ = StartCaptureAsync(true)), () => Dispatcher.Invoke(Reveal),
            () => Dispatcher.Invoke(() => { Reveal(); ShowSettings(); }), () => Dispatcher.Invoke(Quit),
            () => HotkeyService.Display(_settings.HotkeyModifiers, _settings.HotkeyKey), () => !_captureBusy && _dialogDepth == 0);
        _tray = new Forms.NotifyIcon { Icon = new System.Drawing.Icon(stream), Text = "Snip Studio", Visible = true, ContextMenuStrip = _trayMenu };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(Reveal);
    }
    public void Reveal() { _imageIdleTimer.Stop(); _history.Resume(); RefreshHistory(); Show(); if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; Activate(); if (_imageWorkSinceIdle) _imageIdleTimer.Start(); }
    private void UpdateBackgroundState()
    {
        _imageIdleTimer.Stop();
        if (_exiting) return;
        if (IsVisible && WindowState != WindowState.Minimized) { _history.Resume(); RefreshHistory(); if (_imageWorkSinceIdle) _imageIdleTimer.Start(); }
        else _imageIdleTimer.Start();
    }
    private void ScheduleImageCleanup()
    {
        _imageWorkSinceIdle = true; _imageIdleTimer.Stop(); _imageIdleTimer.Start();
    }
    private void ReleaseIdleImageResources()
    {
        _imageIdleTimer.Stop();
        if (_exiting) return;
        if (_captureBusy || _dialogDepth > 0 || Editor.IsInteracting) { _imageIdleTimer.Start(); return; }
        bool released = false;
        if (!IsVisible || WindowState == WindowState.Minimized)
        {
            FlushHistory(); released = _history.Entries.Count > 0;
            HistoryList.ItemsSource = null; _history.Suspend();
        }
        // One collection after image work settles; preserve the document and all undo states.
        // No periodic collections or working-set trimming while the app is idle.
        if (released || _imageWorkSinceIdle) GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: false);
        _imageWorkSinceIdle = false;
    }
    private async Task StartCaptureAsync(bool instant)
    {
        if (_dialogDepth > 0) return;
        if (_captureBusy)
        {
            if (instant && _overlay == null && _captureCancel != null) { _instantRequested = true; _captureCancel.Cancel(); }
            return;
        }
        if (!CanLeaveDocument()) return;
        _captureBusy = true; NewButton.IsEnabled = false; _captureCancel = new CancellationTokenSource();
        var token = _captureCancel.Token;
        bool wasVisible = IsVisible && WindowState != WindowState.Minimized;
        bool captured = false;
        var visiblePins = _pins.Where(p => p.IsVisible).ToArray();
        try
        {
            Hide(); foreach (var pin in visiblePins) pin.Hide();
            int delay = instant ? 0 : _settings.DelaySeconds;
            if (delay > 0) await new CountdownWindow(() => _captureCancel?.Cancel()).CountAsync(delay, token);
            await Task.Delay(180, token); // Let DWM remove our windows before reading pixels.
            token.ThrowIfCancellationRequested();
            _imageWorkSinceIdle = true;
            var frame = NativeDesktop.Capture(_settings.IncludeCursor);
            BitmapSource? result;
            string mode = instant ? "Region" : _settings.CaptureMode;
            if (mode == "All displays") result = frame.Image;
            else
            {
                _overlay = new CaptureOverlay(frame, mode); _overlay.ShowDialog(); result = _overlay.Result; _overlay = null;
            }
            if (result != null)
            {
                LoadImage(result, $"Snip · {DateTime.Now:h:mm tt}"); captured = true; Reveal();
                if (_settings.CopyAfterCapture)
                {
                    try { await ImageFiles.CopyAsync(_document!.Image); SetStatus("Captured and copied. Add your finishing touches, then copy again."); }
                    catch (Exception ex) { SetStatus("Captured. Clipboard was busy: " + ex.Message, true); }
                }
                else SetStatus("Captured. Make it yours.");
            }
            else SetStatus("Capture canceled.");
        }
        catch (OperationCanceledException) { SetStatus("Capture canceled."); }
        catch (Exception ex) { SetStatus("Couldn't capture: " + ex.Message, true); wasVisible = true; }
        finally
        {
            _overlay = null; _captureCancel.Dispose(); _captureCancel = null; _captureBusy = false; NewButton.IsEnabled = true;
            foreach (var pin in visiblePins) if (_pins.Contains(pin)) pin.Show();
            if (!captured && wasVisible) Reveal();
            UpdateBackgroundState();
            if (_instantRequested) { _instantRequested = false; await StartCaptureAsync(true); }
        }
    }
    private void LoadImage(BitmapSource bitmap, string title, string? historyPath = null, string? exportPath = null)
    {
        ScheduleImageCleanup();
        _historyTimer.Stop();
        if (_document != null) _document.Changed -= DocumentChanged;
        _document = new ImageDocument(bitmap); _document.Changed += DocumentChanged;
        _historyPath = historyPath; _historyRevision = historyPath == null ? Guid.Empty : _document.Revision;
        _exportPath = exportPath; _title = title;
        Editor.Document = _document; CanvasScroll.Visibility = Visibility.Visible; EmptyState.Visibility = Visibility.Collapsed;
        _fit = true; UpdateActions(); FlushHistory(); Dispatcher.BeginInvoke(DispatcherPriority.Loaded, FitImage); Editor.Focus();
    }
    public void LoadDemo() => LoadImage(DemoFactory.Create(), "A little weekend inspiration");
    private void DocumentChanged()
    {
        ScheduleImageCleanup();
        UpdateActions(); _historyTimer.Stop(); if (_settings.KeepHistory) _historyTimer.Start();
        if (_fit) Dispatcher.BeginInvoke(FitImage);
    }
    private void UpdateActions()
    {
        if (CopyButton == null) return;
        bool hasImage = _document != null;
        CopyButton.IsEnabled = SaveButton.IsEnabled = PinButton.IsEnabled = StyleControls.IsEnabled = ZoomControls.IsEnabled = hasImage;
        foreach (var button in _toolButtons.Values) button.IsEnabled = hasImage;
        UndoButton.IsEnabled = _document?.CanUndo == true; RedoButton.IsEnabled = _document?.CanRedo == true; DeleteButton.IsEnabled = Editor.SelectedIndex >= 0;
        DocumentTitle.Text = hasImage ? _title + (_document!.IsDirty ? "  •" : "") : "Your workspace";
        Title = hasImage ? $"{_title} — Snip Studio" : "Snip Studio";
        if (hasImage) CanvasInfo.Text = $"{_document!.Image.PixelWidth:N0} × {_document.Image.PixelHeight:N0} px   ·   {_document.Annotations.Count} edits";
    }
    private bool FlushHistory()
    {
        _historyTimer.Stop();
        if (!_settings.KeepHistory || _document == null) return false;
        if (_historyRevision == _document.Revision && _historyPath != null && File.Exists(_historyPath)) return true;
        try
        {
            var image = _document.Render();
            if (_historyPath == null) _historyPath = _history.Add(image, _settings.HistoryLimit);
            else _history.Update(_historyPath, image);
            _historyRevision = _document.Revision; RefreshHistory(); return true;
        }
        catch (Exception ex) { SetStatus("History couldn't be saved. Use Save to keep this image. " + ex.Message, true); return false; }
    }
    private void RefreshHistory()
    {
        HistoryList.ItemsSource = _history.Entries;
        HistoryEmpty.Visibility = _history.Entries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryDescription.Text = _settings.KeepHistory ? $"On this device · last {_settings.HistoryLimit} captures" : "Saving is paused in Settings";
    }
    private bool CanLeaveDocument()
    {
        Editor.CancelInteraction();
        if (_document == null || FlushHistory() || (!_document.IsDirty && _exportPath != null)) return true;
        _dialogDepth++;
        var choice = MessageBox.Show(this, "Save this image before continuing?", "Keep your capture", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        _dialogDepth--;
        return choice == MessageBoxResult.No || (choice == MessageBoxResult.Yes && SaveImage());
    }
    private bool SaveImage()
    {
        if (_document == null) return false;
        Editor.CancelInteraction();
        _dialogDepth++;
        try
        {
            var dialog = new SaveFileDialog { Title = "Save your image", Filter = "PNG image (*.png)|*.png|JPEG image (*.jpg)|*.jpg|Bitmap image (*.bmp)|*.bmp", DefaultExt = ".png", AddExtension = true, FileName = $"Snip {DateTime.Now:yyyy-MM-dd HH-mm-ss}.png", InitialDirectory = Directory.Exists(_settings.SaveDirectory) ? _settings.SaveDirectory : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), OverwritePrompt = true };
            if (dialog.ShowDialog(this) != true) return false;
            ImageFiles.Save(_document.Render(), dialog.FileName);
            _exportPath = dialog.FileName; _document.SavedRevision = _document.Revision;
            _settings.SaveDirectory = Path.GetDirectoryName(dialog.FileName)!; SaveSettingsQuietly(); FlushHistory(); UpdateActions(); SetStatus("Saved " + Path.GetFileName(dialog.FileName)); return true;
        }
        catch (Exception ex) { SetStatus("Couldn't save: " + ex.Message, true); return false; }
        finally { _dialogDepth--; }
    }
    private async Task CopyImageAsync()
    {
        if (_document == null) return;
        Editor.CancelInteraction();
        try { ScheduleImageCleanup(); await ImageFiles.CopyAsync(_document.Render()); FlushHistory(); SetStatus("Image copied. Ready to paste anywhere."); }
        catch (Exception ex) { SetStatus("Couldn't copy: " + ex.Message, true); }
    }
    private void OpenImage()
    {
        if (!CanLeaveDocument()) return;
        _dialogDepth++;
        try
        {
            var dialog = new OpenFileDialog { Title = "Open an image", Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff;*.webp|All files|*.*" };
            if (dialog.ShowDialog(this) == true) { LoadImage(ImageFiles.Load(dialog.FileName), Path.GetFileName(dialog.FileName), exportPath: dialog.FileName); SetStatus("Image opened. Drag, draw, and make it yours."); }
        }
        catch (Exception ex) { SetStatus("Couldn't open this image: " + ex.Message, true); }
        finally { _dialogDepth--; }
    }
    private void PasteImage()
    {
        try
        {
            var bitmap = Clipboard.GetImage();
            if (bitmap == null) { SetStatus("The clipboard doesn't contain an image. Copy an image and try again."); return; }
            if (!CanLeaveDocument()) return;
            LoadImage(bitmap, "Pasted image"); SetStatus("Image pasted. All yours.");
        }
        catch (Exception ex) { SetStatus("Couldn't paste: " + ex.Message, true); }
    }
    private void AddText(Point point, Annotation? existing)
    {
        if (_document == null) return;
        _dialogDepth++;
        try
        {
            var dialog = new TextDialog(existing?.Text ?? "") { Owner = this };
            if (dialog.ShowDialog() != true) return;
            if (existing != null) _document.Commit(() => existing.Text = dialog.Value);
            else _document.Add(new Annotation { Tool = DrawTool.Text, Color = Editor.Color, FontSize = Editor.TextSize, Text = dialog.Value, Points = [point] });
        }
        finally { _dialogDepth--; Editor.Focus(); }
    }
    private void PinImage()
    {
        if (_document == null) return;
        var pin = new PinWindow(_document.Render()); _pins.Add(pin); pin.Closed += (_, _) => _pins.Remove(pin); pin.Show();
        SetStatus("Pinned above your windows. Drag to move; double-click to close.");
    }
    private void ShowSettings()
    {
        if (_captureBusy || _dialogDepth > 0 || _hotkey == null) return;
        _dialogDepth++;
        try
        {
            var dialog = new SettingsWindow(_settings, _hotkey) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                _settings = dialog.Result!; UpdateShortcutLabel(); _history.Trim(_settings.HistoryLimit); _history.Reload(); RefreshHistory();
                if (_settings.KeepHistory) FlushHistory(); else _historyTimer.Stop();
                SetStatus("Settings saved.");
            }
        }
        finally { _dialogDepth--; }
    }
    private void ShowHelp()
    {
        _dialogDepth++;
        try { new ShortcutsWindow(HotkeyService.Display(_settings.HotkeyModifiers, _settings.HotkeyKey)) { Owner = this }.ShowDialog(); }
        finally { _dialogDepth--; }
    }
    private void UpdateShortcutLabel()
    {
        string shortcut = HotkeyService.Display(_settings.HotkeyModifiers, _settings.HotkeyKey);
        ShortcutStatus.Text = (_hotkey?.Registered == true ? "Instant snip   " : "Shortcut unavailable   ") + shortcut;
        EmptyShortcut.Text = shortcut + "  ·  instant capture";
    }
    private void SetReadyStatus()
    {
        int displays; try { displays = NativeDesktop.Displays().Count; } catch { displays = 1; }
        StatusText.Text = _hotkey?.Registered == true ? $"Ready   ·   {displays} display{(displays == 1 ? "" : "s")} connected   ·   Everything stays on this device" : "Global shortcut unavailable. Choose another in Settings.";
        StatusDot.Fill = (Brush)FindResource(_hotkey?.Registered == true ? "Accent" : "Warning");
    }
    private void SetStatus(string text, bool error = false)
    {
        StatusText.Text = text; StatusText.ToolTip = text;
        StatusDot.Fill = (Brush)FindResource(error ? "Warning" : "Accent");
        _statusTimer.Stop(); if (!error) _statusTimer.Start();
    }
    private void SaveSettingsQuietly() { try { _settings.Save(); } catch (Exception ex) { SetStatus("Couldn't save settings: " + ex.Message, true); } }

    private double PixelScale => VisualTreeHelper.GetDpi(this).DpiScaleX;
    private void FitImage()
    {
        if (!_fit || _document == null || CanvasScroll.ViewportWidth <= 0 || CanvasScroll.ViewportHeight <= 0) return;
        double scale = Math.Min((CanvasScroll.ViewportWidth - 60) / _document.Image.PixelWidth, (CanvasScroll.ViewportHeight - 60) / _document.Image.PixelHeight);
        SetZoom(Math.Min(1 / PixelScale, Math.Max(0.02, scale)), true);
    }
    private void SetZoom(double zoom, bool fitting = false)
    {
        _fit = fitting; zoom = Math.Clamp(zoom, 0.02 / PixelScale, 8 / PixelScale);
        CanvasScale.ScaleX = CanvasScale.ScaleY = zoom; Editor.ViewScale = zoom; Editor.InvalidateVisual();
        ZoomLabel.Content = $"{zoom * PixelScale * 100:0}%";
    }
    private void Zoom(double factor) => SetZoom(CanvasScale.ScaleX * factor);
    private void Undo() { Editor.CancelInteraction(); Editor.ClearSelection(); _document?.Undo(); }
    private void Redo() { Editor.CancelInteraction(); Editor.ClearSelection(); _document?.Redo(); }
    private async void New_Click(object sender, RoutedEventArgs e) => await StartCaptureAsync(false);
    private void Open_Click(object sender, RoutedEventArgs e) => OpenImage();
    private void Paste_Click(object sender, RoutedEventArgs e) => PasteImage();
    private async void Copy_Click(object sender, RoutedEventArgs e) => await CopyImageAsync();
    private void Save_Click(object sender, RoutedEventArgs e) => SaveImage();
    private void Pin_Click(object sender, RoutedEventArgs e) => PinImage();
    private void Undo_Click(object sender, RoutedEventArgs e) => Undo();
    private void Redo_Click(object sender, RoutedEventArgs e) => Redo();
    private void Delete_Click(object sender, RoutedEventArgs e) => Editor.DeleteSelection();
    private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings();
    private void Help_Click(object sender, RoutedEventArgs e) => ShowHelp();
    private void ZoomIn_Click(object sender, RoutedEventArgs e) => Zoom(1.25);
    private void ZoomOut_Click(object sender, RoutedEventArgs e) => Zoom(0.8);
    private void ActualSize_Click(object sender, RoutedEventArgs e) => SetZoom(1 / PixelScale);
    private void Fit_Click(object sender, RoutedEventArgs e) { _fit = true; FitImage(); }
    private void Canvas_SizeChanged(object sender, SizeChangedEventArgs e) { if (_ready) FitImage(); }
    private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e) { if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { Zoom(e.Delta > 0 ? 1.15 : 1 / 1.15); e.Handled = true; } }
    private void CaptureOption_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        _settings.CaptureMode = ((ComboBoxItem)ModeBox.SelectedItem).Content.ToString()!;
        _settings.DelaySeconds = int.Parse(((ComboBoxItem)DelayBox.SelectedItem).Tag.ToString()!); SaveSettingsQuietly();
    }
    private void Width_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_ready || _syncStyle) return; Editor.StrokeWidth = WidthSlider.Value; _settings.StrokeWidth = WidthSlider.Value; WidthLabel.Text = WidthSlider.Value.ToString("0");
    }
    private void TextSize_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _syncStyle) return; Editor.TextSize = double.Parse(((ComboBoxItem)TextSizeBox.SelectedItem).Tag.ToString()!); _settings.TextSize = Editor.TextSize; Editor.ApplyStyleToSelection();
    }
    private void HexColor_Changed(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_ready) return;
        try { if (HexColorBox.Text.Trim().Length != 7 || !HexColorBox.Text.StartsWith('#')) throw new FormatException(); SetColor((Color)ColorConverter.ConvertFromString(HexColorBox.Text.Trim())); Editor.ApplyStyleToSelection(); }
        catch { HexColorBox.Text = _settings.AnnotationColor; SetStatus("Use a color like #147D70."); }
    }
    private void History_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not CaptureEntry entry || !CanLeaveDocument()) return;
        try { LoadImage(ImageFiles.Load(entry.Path), entry.Label, entry.Path); SetStatus("Reopened from local history. Previous edits are part of this image."); }
        catch (Exception ex) { SetStatus("Couldn't reopen capture: " + ex.Message, true); }
    }
    private void ClearHistory_Click(object sender, RoutedEventArgs e)
    {
        if (_history.Entries.Count == 0) return;
        _dialogDepth++;
        try
        {
            if (MessageBox.Show(this, "Clear all images from Snip Studio's local history? Exported files are kept, and the current image stays open.", "Clear capture history", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            _historyTimer.Stop(); _history.Clear(); _historyPath = null; _historyRevision = Guid.Empty; RefreshHistory(); SetStatus("History cleared. New edits or captures will be saved while history is enabled.");
        }
        catch (Exception ex) { SetStatus("Couldn't clear history: " + ex.Message, true); }
        finally { _dialogDepth--; }
    }
    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (_dialogDepth > 0 || _captureBusy || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        if (files == null || files.Length == 0 || !CanLeaveDocument()) return;
        try { LoadImage(ImageFiles.Load(files[0]), Path.GetFileName(files[0]), exportPath: files[0]); SetStatus("Image opened."); }
        catch (Exception ex) { SetStatus("Couldn't open that file: " + ex.Message, true); }
    }
    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (_dialogDepth > 0) return;
        if (Keyboard.FocusedElement is TextBox) { if (e.Key == Key.Enter) { Editor.Focus(); e.Handled = true; } return; }
        bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control), shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
        if (ctrl && !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
        {
            e.Handled = true;
            switch (e.Key)
            {
                case Key.N: await StartCaptureAsync(false); break;
                case Key.O: OpenImage(); break;
                case Key.V: PasteImage(); break;
                case Key.C: await CopyImageAsync(); break;
                case Key.S: SaveImage(); break;
                case Key.P: PinImage(); break;
                case Key.Z: if (shift) Redo(); else Undo(); break;
                case Key.Y: Redo(); break;
                case Key.D0: _fit = true; FitImage(); break;
                case Key.D1: SetZoom(1 / PixelScale); break;
                case Key.OemPlus: case Key.Add: Zoom(1.25); break;
                case Key.OemMinus: case Key.Subtract: Zoom(0.8); break;
                case Key.OemComma: ShowSettings(); break;
                case Key.Q: Quit(); break;
                default: e.Handled = false; break;
            }
            return;
        }
        if (Keyboard.Modifiers is not (ModifierKeys.None or ModifierKeys.Shift)) return;
        if (e.Key == Key.F1) { ShowHelp(); e.Handled = true; return; }
        if (_document == null) return;
        DrawTool? tool = e.Key switch { Key.V => DrawTool.Select, Key.B => DrawTool.Pen, Key.H => DrawTool.Highlighter, Key.A => DrawTool.Arrow, Key.L => DrawTool.Line, Key.R => DrawTool.Rectangle, Key.E => DrawTool.Ellipse, Key.T => DrawTool.Text, Key.N => DrawTool.Step, Key.P => DrawTool.Pixelate, Key.D => DrawTool.Redact, Key.C => DrawTool.Crop, _ => null };
        if (tool != null) { SetTool(tool.Value); Editor.Focus(); e.Handled = true; }
        else if (e.Key is Key.Delete or Key.Back) { Editor.DeleteSelection(); e.Handled = true; }
        else if (e.Key == Key.Escape) { Editor.ClearSelection(); e.Handled = true; }
    }
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_exiting) return;
        if (_settings.CloseToTray)
        {
            e.Cancel = true; FlushHistory(); SaveSettingsQuietly(); Hide();
            if (!_trayNoticeShown) { _tray?.ShowBalloonTip(3500, "Snip Studio is ready", "Your capture shortcut still works. Right-click the tray icon to quit.", Forms.ToolTipIcon.Info); _trayNoticeShown = true; }
        }
        else { e.Cancel = true; Quit(); }
    }
    private void Quit()
    {
        if (_captureBusy || _dialogDepth > 0 || !CanLeaveDocument()) return;
        _exiting = true; _historyTimer.Stop(); _statusTimer.Stop(); _imageIdleTimer.Stop(); SaveSettingsQuietly();
        foreach (var pin in _pins.ToArray()) pin.Close();
        _hotkey?.Dispose(); _tray?.Icon?.Dispose(); _tray?.Dispose(); _trayMenu?.Dispose(); Application.Current.Shutdown();
    }
}
