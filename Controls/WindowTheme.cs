using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace SnipStudio.Controls;

public static class WindowTheme
{
    public static readonly DependencyProperty IsDarkProperty = DependencyProperty.RegisterAttached("IsDark", typeof(bool), typeof(WindowTheme), new PropertyMetadata(false, OnChanged));
    public static void SetIsDark(DependencyObject target, bool value) => target.SetValue(IsDarkProperty, value);
    public static bool GetIsDark(DependencyObject target) => (bool)target.GetValue(IsDarkProperty);
    public static Brush Brush(string key) => (Brush)Application.Current.FindResource(key);
    public static void Initialize(Window window) => window.Style = (Style)Application.Current.FindResource(typeof(Window));
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
    private static void OnChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not Window window || e.NewValue is not true) return;
        window.SourceInitialized += (_, _) => Apply(window);
        if (new WindowInteropHelper(window).Handle != IntPtr.Zero) Apply(window);
    }
    private static void Apply(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        int enabled = 1;
        // Older Windows safely ignores unsupported frame attributes.
        DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
        SetColor(handle, 35, "#11151C"); SetColor(handle, 36, "#E8EDF5"); SetColor(handle, 34, "#2B3442");
    }
    private static void SetColor(IntPtr handle, int attribute, string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        int colorRef = color.R | (color.G << 8) | (color.B << 16);
        DwmSetWindowAttribute(handle, attribute, ref colorRef, sizeof(int));
    }
}
