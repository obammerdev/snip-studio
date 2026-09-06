using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnipStudio.Services;
using SnipStudio.Controls;

namespace SnipStudio;

public sealed class PinWindow : Window
{
    public PinWindow(BitmapSource bitmap)
    {
        WindowTheme.Initialize(this);
        Title = "Snip Studio · Pinned image"; Topmost = true; Width = Math.Min(700, bitmap.PixelWidth + 16); Height = Math.Min(600, bitmap.PixelHeight + 16);
        MinWidth = 100; MinHeight = 80; WindowStyle = WindowStyle.ToolWindow; ResizeMode = ResizeMode.CanResizeWithGrip; Background = WindowTheme.Brush("Canvas");
        Content = new Image { Source = bitmap, Stretch = Stretch.Uniform, Margin = new Thickness(4), ToolTip = "Drag to move · Double-click to close · Right-click for options" };
        var menu = new ContextMenu();
        var copy = new MenuItem { Header = "Copy image" }; copy.Click += async (_, _) => { try { await ImageFiles.CopyAsync(bitmap); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Couldn't copy"); } };
        var close = new MenuItem { Header = "Close pinned image" }; close.Click += (_, _) => Close();
        var top = new MenuItem { Header = "Always on top", IsCheckable = true, IsChecked = true }; top.Click += (_, _) => Topmost = top.IsChecked;
        menu.Items.Add(copy); menu.Items.Add(top); menu.Items.Add(new Separator()); menu.Items.Add(close); ContextMenu = menu;
        MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) Close(); else DragMove(); };
        PreviewKeyDown += async (_, e) => { if (e.Key == Key.Escape) Close(); if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { try { await ImageFiles.CopyAsync(bitmap); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Couldn't copy"); } } };
    }
}
