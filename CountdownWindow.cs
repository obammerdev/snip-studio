using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using SnipStudio.Services;
using SnipStudio.Controls;

namespace SnipStudio;

public sealed class CountdownWindow : Window
{
    private readonly TextBlock _number = new() { FontSize = 40, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center };
    public CountdownWindow(Action cancel)
    {
        WindowTheme.Initialize(this);
        Title = "Snip Studio · Countdown"; Width = 300; Height = 158; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        Topmost = true; ShowInTaskbar = App.IsTestMode; AllowsTransparency = true; Background = Brushes.Transparent;
        var stack = new StackPanel { Margin = new Thickness(18) };
        stack.Children.Add(new TextBlock { Text = "CAPTURING IN", Foreground = WindowTheme.Brush("Accent"), FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center });
        stack.Children.Add(_number);
        var button = new Button { Content = "Cancel capture   Esc", Margin = new Thickness(35, 5, 35, 0), Padding = new Thickness(10, 5, 10, 5) };
        button.Click += (_, _) => cancel(); stack.Children.Add(button);
        Content = new Border { CornerRadius = new CornerRadius(16), Background = WindowTheme.Brush("Panel"), BorderBrush = WindowTheme.Brush("Line"), BorderThickness = new Thickness(1), Child = stack };
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { cancel(); e.Handled = true; } };
        Loaded += (_, _) =>
        {
            var bounds = NativeDesktop.CursorDisplay().Bounds;
            var dpi = VisualTreeHelper.GetDpi(this);
            int w = (int)(Width * dpi.DpiScaleX), h = (int)(Height * dpi.DpiScaleY);
            NativeDesktop.SetWindowPos(new WindowInteropHelper(this).Handle, new IntPtr(-1), bounds.X + (bounds.Width - w) / 2, bounds.Y + 40, w, h, 0x0040);
        };
    }
    public async Task CountAsync(int seconds, CancellationToken token)
    {
        _number.Text = seconds.ToString(); Show();
        try { for (int i = seconds; i > 0; i--) { _number.Text = i.ToString(); await Task.Delay(1000, token); } }
        finally { Close(); }
    }
}
