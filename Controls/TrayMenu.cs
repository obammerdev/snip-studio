using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SnipStudio.Controls;

public sealed class TrayMenu : ContextMenuStrip
{
    internal sealed class CommandItem(string text, string glyph, Action command) : ToolStripMenuItem(text)
    {
        internal string Glyph { get; } = glyph;
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        internal string Hint { get; set; } = "";
        protected override void OnClick(EventArgs e) { base.OnClick(e); command(); }
    }
    private readonly Func<string> _shortcut;
    private readonly Func<bool> _canAct;
    private readonly CommandItem _capture, _settings, _quit;
    private Font? _menuFont, _hintFont;
    internal Font HintFont => _hintFont ?? Font;
    internal int Scale(int value) => (int)Math.Round(value * DeviceDpi / 96d);
    protected override Padding DefaultPadding => new(Scale(6));

    public TrayMenu(Action capture, Action reveal, Action settings, Action quit, Func<string> shortcut, Func<bool> canAct)
    {
        _shortcut = shortcut; _canAct = canAct;
        Renderer = new TrayRenderer(); ShowImageMargin = false; ShowCheckMargin = false; AutoSize = false;
        BackColor = Color.FromArgb(25, 31, 41); ForeColor = Color.FromArgb(232, 237, 245);
        _capture = new("New snip", "capture", capture);
        _settings = new("Settings", "settings", settings);
        _quit = new("Quit Snip Studio", "quit", quit);
        Items.AddRange([_capture, new CommandItem("Open editor", "editor", reveal), _settings, new ToolStripSeparator(), _quit]);
        PrepareLayout();
    }
    internal void PrepareLayout()
    {
        var previousFont = _menuFont; var previousHint = _hintFont;
        _menuFont = new Font("Segoe UI", Scale(13), FontStyle.Regular, GraphicsUnit.Pixel);
        _hintFont = new Font("Segoe UI", Scale(11), FontStyle.Regular, GraphicsUnit.Pixel);
        Font = _menuFont; previousFont?.Dispose(); previousHint?.Dispose();
        Padding = new Padding(Scale(6));
        _capture.Hint = _shortcut();
        _capture.AccessibleDescription = "Instant region capture. " + _capture.Hint;
        int width = Math.Max(Scale(300), TextRenderer.MeasureText(_capture.Text, Font).Width + TextRenderer.MeasureText(_capture.Hint, HintFont).Width + Scale(76));
        int totalWidth = width + Padding.Horizontal;
        foreach (ToolStripItem item in Items)
        {
            item.AutoSize = false; item.Margin = Padding.Empty;
            item.Size = new Size(totalWidth, Scale(item is ToolStripSeparator ? 13 : 38));
        }
        Size = new Size(totalWidth, Items.Cast<ToolStripItem>().Sum(item => item.Height) + Padding.Vertical);
        PerformLayout();
    }
    protected override void OnOpening(CancelEventArgs e)
    {
        PrepareLayout(); _capture.Enabled = _settings.Enabled = _quit.Enabled = _canAct();
        base.OnOpening(e);
    }
    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (!SystemInformation.HighContrast)
        {
            // Windows 11 rounds the native popup; earlier Windows ignores this attribute.
            int round = 2; DwmSetWindowAttribute(Handle, 33, ref round, sizeof(int));
        }
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) { _menuFont?.Dispose(); _hintFont?.Dispose(); }
    }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    private sealed class TrayRenderer : ToolStripRenderer
    {
        private static Color Surface => SystemInformation.HighContrast ? SystemColors.Menu : Color.FromArgb(25, 31, 41);
        private static Color Hover => SystemInformation.HighContrast ? SystemColors.Highlight : Color.FromArgb(38, 46, 58);
        private static Color Line => SystemInformation.HighContrast ? SystemColors.WindowFrame : Color.FromArgb(46, 55, 69);
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using var brush = new SolidBrush(Surface); e.Graphics.FillRectangle(brush, e.AffectedBounds);
        }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using var pen = new Pen(Line); e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
        }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected || !e.Item.Enabled) return;
            if (e.ToolStrip is not TrayMenu menu) return;
            using var shape = Rounded(new RectangleF(menu.Scale(6), menu.Scale(1), e.Item.Width - menu.Scale(12), e.Item.Height - menu.Scale(2)), menu.Scale(5));
            using var brush = new SolidBrush(Hover);
            var previous = e.Graphics.SmoothingMode; e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            e.Graphics.FillPath(brush, shape); e.Graphics.SmoothingMode = previous;
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item is not CommandItem item) { base.OnRenderItemText(e); return; }
            if (e.ToolStrip is not TrayMenu menu) return;
            Color text = !item.Enabled ? SystemColors.GrayText : SystemInformation.HighContrast ? (item.Selected ? SystemColors.HighlightText : SystemColors.MenuText) : Color.FromArgb(232, 237, 245);
            Color muted = SystemInformation.HighContrast ? text : item.Enabled ? Color.FromArgb(154, 167, 187) : SystemColors.GrayText;
            DrawGlyph(e.Graphics, item.Glyph, new Rectangle(menu.Scale(16), (item.Height - menu.Scale(17)) / 2, menu.Scale(17), menu.Scale(17)), muted);
            const TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;
            int hintWidth = string.IsNullOrEmpty(item.Hint) ? 0 : TextRenderer.MeasureText(item.Hint, menu.HintFont).Width + menu.Scale(14);
            TextRenderer.DrawText(e.Graphics, item.Text, menu.Font, new Rectangle(menu.Scale(45), 0, item.Width - menu.Scale(63) - hintWidth, item.Height), text, flags);
            if (hintWidth > 0) TextRenderer.DrawText(e.Graphics, item.Hint, menu.HintFont, new Rectangle(item.Width - hintWidth - menu.Scale(18), 0, hintWidth, item.Height), muted, flags | TextFormatFlags.Right);
        }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            if (e.ToolStrip is not TrayMenu menu) return;
            using var pen = new Pen(Line);
            e.Graphics.DrawLine(pen, menu.Scale(12), e.Item.Height / 2, e.Item.Width - menu.Scale(12), e.Item.Height / 2);
        }
        private static GraphicsPath Rounded(RectangleF bounds, float radius)
        {
            var path = new GraphicsPath(); float d = radius * 2;
            path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90); path.CloseFigure(); return path;
        }
        private static void DrawGlyph(Graphics graphics, string glyph, Rectangle rect, Color color)
        {
            var state = graphics.Save();
            graphics.TranslateTransform(rect.X, rect.Y); graphics.ScaleTransform(rect.Width / 20f, rect.Height / 20f);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(color, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            switch (glyph)
            {
                case "capture":
                    graphics.DrawLines(pen, [new(7, 3), new(3, 3), new(3, 7)]);
                    graphics.DrawLines(pen, [new(13, 3), new(17, 3), new(17, 7)]);
                    graphics.DrawLines(pen, [new(17, 13), new(17, 17), new(13, 17)]);
                    graphics.DrawLines(pen, [new(7, 17), new(3, 17), new(3, 13)]); break;
                case "editor":
                    graphics.DrawRectangle(pen, 2, 3, 16, 14); graphics.DrawLine(pen, 2, 7, 18, 7); break;
                case "settings":
                    graphics.DrawLine(pen, 3, 5, 17, 5); graphics.DrawLine(pen, 3, 10, 17, 10); graphics.DrawLine(pen, 3, 15, 17, 15);
                    using (var brush = new SolidBrush(color)) { graphics.FillEllipse(brush, 5, 3, 4, 4); graphics.FillEllipse(brush, 11, 8, 4, 4); graphics.FillEllipse(brush, 6, 13, 4, 4); } break;
                case "quit":
                    graphics.DrawArc(pen, 3, 3, 14, 14, -45, 270); graphics.DrawLine(pen, 10, 2, 10, 10); break;
            }
            graphics.Restore(state);
        }
    }
}
