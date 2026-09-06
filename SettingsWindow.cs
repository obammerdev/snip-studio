using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SnipStudio.Services;
using SnipStudio.Controls;

namespace SnipStudio;

public sealed class SettingsWindow : Window
{
    private readonly AppSettings _original;
    private readonly HotkeyService _hotkey;
    private uint _modifiers, _key;
    private readonly TextBox _recorder = new() { IsReadOnly = true, FontWeight = FontWeights.SemiBold, FontSize = 15, Background = WindowTheme.Brush("Control"), Height = 45 };
    private readonly TextBlock _error = new() { Foreground = WindowTheme.Brush("Warning"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0), FontSize = 12 };
    private readonly CheckBox _startup = new(), _tray = new(), _copy = new(), _history = new(), _cursor = new(), _hardware = new();
    private readonly ComboBox _limit = new() { Width = 140, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 5, 0, 0) };
    public AppSettings? Result { get; private set; }

    public SettingsWindow(AppSettings settings, HotkeyService hotkey)
    {
        WindowTheme.Initialize(this);
        _original = settings; _hotkey = hotkey; _modifiers = settings.HotkeyModifiers; _key = settings.HotkeyKey;
        Title = "Settings · Snip Studio"; Width = 600; Height = 775; MinHeight = 550; ResizeMode = ResizeMode.CanResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        MaxHeight = SystemParameters.WorkArea.Height - 35;
        var root = new Grid(); root.RowDefinitions.Add(new() { Height = GridLength.Auto }); root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) }); root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var heading = new StackPanel { Margin = new Thickness(28, 25, 28, 18) };
        heading.Children.Add(new TextBlock { Text = "Preferences", FontSize = 25, FontWeight = FontWeights.SemiBold });
        heading.Children.Add(new TextBlock { Text = "Shortcuts, capture behavior, and your workspace.", Margin = new Thickness(0, 7, 0, 0), Foreground = (Brush)Application.Current.FindResource("Muted") }); root.Children.Add(heading);
        var body = new StackPanel { Margin = new Thickness(28, 0, 28, 24) };
        var scroll = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetRow(scroll, 1); root.Children.Add(scroll);
        Section(body, "CAPTURE SHORTCUT");
        body.Children.Add(Note("Click the field, then press your preferred combination."));
        _recorder.Text = HotkeyService.Display(_modifiers, _key); _recorder.Margin = new Thickness(0, 9, 0, 0); _recorder.ToolTip = "Press Ctrl with Alt or Shift, plus a letter, number, or F1–F11.";
        System.Windows.Automation.AutomationProperties.SetName(_recorder, "Capture shortcut recorder");
        _recorder.PreviewKeyDown += RecordShortcut; body.Children.Add(_recorder);
        var reset = new Button { Content = "Reset to Ctrl + Alt + Shift + S", Style = (Style)Application.Current.FindResource("QuietButton"), HorizontalAlignment = HorizontalAlignment.Left, FontSize = 11, Padding = new Thickness(0, 9, 0, 5) };
        reset.Click += (_, _) => { _modifiers = 7; _key = 0x53; _recorder.Text = HotkeyService.Display(_modifiers, _key); _error.Text = ""; }; body.Children.Add(reset);
        body.Children.Add(Note("Always starts an instant region snip, even when a countdown is selected. Conflicting shortcuts are rejected without replacing your current one."));
        body.Children.Add(_error); body.Children.Add(new Separator());
        Section(body, "STARTUP & BACKGROUND");
        Configure(_startup, "Launch when I sign in to Windows", StartupService.IsEnabled()); body.Children.Add(_startup);
        body.Children.Add(Note("Starts quietly in the system tray, with your shortcut ready."));
        Configure(_tray, "Keep running in the tray when I close the window", settings.CloseToTray); body.Children.Add(_tray);
        body.Children.Add(Note("Right-click the tray icon or press Ctrl + Q to quit completely."));
        body.Children.Add(new Separator()); Section(body, "AFTER A CAPTURE");
        Configure(_copy, "Copy the image to the clipboard automatically", settings.CopyAfterCapture); body.Children.Add(_copy);
        Configure(_cursor, "Include the mouse pointer in captures", settings.IncludeCursor); body.Children.Add(_cursor);
        Configure(_history, "Keep recent captures on this device", settings.KeepHistory); body.Children.Add(_history);
        foreach (int number in new[] { 10, 30, 100 }) _limit.Items.Add(new ComboBoxItem { Content = $"Last {number} captures", Tag = number });
        _limit.SelectedIndex = settings.HistoryLimit <= 10 ? 0 : settings.HistoryLimit <= 30 ? 1 : 2; _limit.IsEnabled = settings.KeepHistory;
        _history.Checked += (_, _) => _limit.IsEnabled = true; _history.Unchecked += (_, _) => _limit.IsEnabled = false;
        body.Children.Add(_limit);
        body.Children.Add(Note("Recent captures include your latest edits. Reopening a saved capture starts a fresh editing session. Turning this off pauses saving; use Clear in the sidebar to remove existing history.", 12));
        body.Children.Add(new Separator());
        Section(body, "PERFORMANCE");
        Configure(_hardware, "Use hardware acceleration", settings.HardwareAcceleration); body.Children.Add(_hardware);
        body.Children.Add(Note("Off uses less memory. Turn on if drawing or zooming very large images feels slow. Quit and reopen the app after changing this setting."));
        body.Children.Add(new Separator());
        var version = typeof(App).Assembly.GetName().Version?.ToString(3);
        body.Children.Add(new TextBlock { Text = $"Snip Studio {version} · Dark", FontWeight = FontWeights.SemiBold, FontSize = 12 });
        body.Children.Add(Note("Native Windows app. No accounts, uploads, or tracking.\nSettings and history: %LOCALAPPDATA%\\SnipStudio", 7));
        var footer = new Border { Background = WindowTheme.Brush("Panel"), BorderBrush = (Brush)Application.Current.FindResource("Line"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(28, 16, 28, 16) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(new Button { Content = "Cancel", IsCancel = true, MinWidth = 90, Margin = new Thickness(0, 0, 9, 0) });
        var save = new Button { Content = "Save preferences", Style = (Style)Application.Current.FindResource("PrimaryButton"), MinWidth = 150 }; save.Click += Save;
        buttons.Children.Add(save); footer.Child = buttons; Grid.SetRow(footer, 2); root.Children.Add(footer); Content = root;
    }
    private static void Configure(CheckBox box, string text, bool value) { box.Content = text; box.IsChecked = value; }
    private static void Section(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, FontSize = 10, Foreground = WindowTheme.Brush("Accent"), FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 5, 0, 7) });
    private static TextBlock Note(string text, double top = 2) => new() { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = 12, LineHeight = 19, Foreground = WindowTheme.Brush("Muted"), Margin = new Thickness(0, top, 0, 0) };
    private void RecordShortcut(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Tab) return;
        e.Handled = true;
        Key key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        if (key == Key.Escape) { _recorder.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); return; }
        var modifiers = Keyboard.Modifiers;
        uint native = (modifiers.HasFlag(ModifierKeys.Control) ? 2u : 0) | (modifiers.HasFlag(ModifierKeys.Alt) ? 1u : 0) | (modifiers.HasFlag(ModifierKeys.Shift) ? 4u : 0) | (modifiers.HasFlag(ModifierKeys.Windows) ? 8u : 0);
        uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        if (!HotkeyService.IsValid(native, virtualKey)) { _error.Text = "Use Ctrl with Alt or Shift, and a letter, number, or F1–F11. Windows-key combinations are reserved."; return; }
        _modifiers = native; _key = virtualKey; _recorder.Text = HotkeyService.Display(native, virtualKey); _error.Text = "";
    }
    private void Save(object sender, RoutedEventArgs e)
    {
        if (!_hotkey.TrySet(_modifiers, _key, out string error)) { _error.Text = error; return; }
        bool wasStartup = StartupService.IsEnabled();
        try
        {
            var result = _original.Clone(); result.HotkeyModifiers = _modifiers; result.HotkeyKey = _key;
            result.CloseToTray = _tray.IsChecked == true; result.CopyAfterCapture = _copy.IsChecked == true; result.KeepHistory = _history.IsChecked == true; result.IncludeCursor = _cursor.IsChecked == true;
            result.HistoryLimit = (int)((ComboBoxItem)_limit.SelectedItem).Tag;
            result.HardwareAcceleration = _hardware.IsChecked == true;
            if (wasStartup != (_startup.IsChecked == true)) StartupService.SetEnabled(_startup.IsChecked == true);
            result.Save(); Result = result; DialogResult = true;
        }
        catch (Exception ex)
        {
            _hotkey.TrySet(_original.HotkeyModifiers, _original.HotkeyKey, out _);
            try { if (StartupService.IsEnabled() != wasStartup) StartupService.SetEnabled(wasStartup); } catch { }
            _error.Text = "Couldn't save preferences: " + ex.Message;
        }
    }
}

public sealed class TextDialog : Window
{
    private readonly TextBox _text;
    public string Value => _text.Text.Trim();
    public TextDialog(string initial)
    {
        WindowTheme.Initialize(this);
        Title = "Add a label · Snip Studio"; Width = 460; Height = 305; ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new StackPanel { Margin = new Thickness(25) };
        root.Children.Add(new TextBlock { Text = "Add a label", FontSize = 21, FontWeight = FontWeights.SemiBold });
        _text = new TextBox { Text = initial, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 120, Margin = new Thickness(0, 16, 0, 12), VerticalContentAlignment = VerticalAlignment.Top, MaxLength = 3000, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        System.Windows.Automation.AutomationProperties.SetName(_text, "Annotation text"); root.Children.Add(_text);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) });
        var apply = new Button { Content = "Apply   Ctrl + Enter", Style = (Style)Application.Current.FindResource("PrimaryButton") }; apply.Click += (_, _) => Apply(); actions.Children.Add(apply); root.Children.Add(actions); Content = root;
        Loaded += (_, _) => { _text.Focus(); _text.SelectAll(); };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { Apply(); e.Handled = true; } };
    }
    private void Apply() { if (!string.IsNullOrWhiteSpace(_text.Text)) DialogResult = true; }
}

public sealed class ShortcutsWindow : Window
{
    public ShortcutsWindow(string globalShortcut)
    {
        WindowTheme.Initialize(this);
        Title = "Keyboard shortcuts · Snip Studio"; Width = 650; Height = 675; MaxHeight = SystemParameters.WorkArea.Height - 35; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var panel = new StackPanel { Margin = new Thickness(28) };
        panel.Children.Add(new TextBlock { Text = "Keyboard shortcuts", FontSize = 25, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 9) });
        panel.Children.Add(new TextBlock { Text = "Your shortcut reference", Foreground = (Brush)Application.Current.FindResource("Muted"), Margin = new Thickness(0, 0, 0, 22) });
        var rows = new (string Action, string Keys)[]
        {
            ("Instant region capture (works anywhere)", globalShortcut), ("New capture with selected mode and delay", "Ctrl + N"),
            ("Open / paste image", "Ctrl + O / Ctrl + V"), ("Copy edited image / save", "Ctrl + C / Ctrl + S"),
            ("Undo / redo", "Ctrl + Z / Ctrl + Shift + Z"), ("Redo (alternative)", "Ctrl + Y"),
            ("Select & move / delete selected annotation", "V / Delete"), ("Pen / highlighter", "B / H"),
            ("Arrow / line / rectangle / ring", "A / L / R / E"), ("Text / numbered steps", "T / N"),
            ("Pixelate / solid redaction / crop", "P / D / C"), ("Square, circle, or angle snapping", "Hold Shift while drawing"),
            ("Fit image / actual pixels", "Ctrl + 0 / Ctrl + 1"), ("Zoom", "Ctrl + mouse wheel, +, or −"),
            ("Pin above other windows", "Ctrl + P"), ("Settings / quit completely", "Ctrl + , / Ctrl + Q"),
            ("Cancel capture, countdown, or current stroke", "Esc"), ("Edit an existing text label", "V, then double-click label")
        };
        foreach (var row in rows)
        {
            var grid = new Grid { Margin = new Thickness(0, 6, 0, 6) }; grid.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) }); grid.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            grid.Children.Add(new TextBlock { Text = row.Action, FontSize = 12 });
            var keys = new TextBlock { Text = row.Keys, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = (Brush)Application.Current.FindResource("Accent") }; Grid.SetColumn(keys, 1); grid.Children.Add(keys); panel.Children.Add(grid);
        }
        var close = new Button { Content = "Got it", IsCancel = true, Margin = new Thickness(0, 20, 0, 0), HorizontalAlignment = HorizontalAlignment.Right, Style = (Style)Application.Current.FindResource("PrimaryButton"), MinWidth = 100 }; panel.Children.Add(close);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
