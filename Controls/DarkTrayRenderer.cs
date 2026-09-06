using System.Drawing;
using System.Windows.Forms;

namespace SnipStudio.Controls;

public sealed class DarkTrayRenderer : ToolStripProfessionalRenderer
{
    public DarkTrayRenderer() : base(new TrayColors()) { RoundedEdges = false; }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = Color.FromArgb(232, 237, 245); base.OnRenderItemText(e); }
    private sealed class TrayColors : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => Color.FromArgb(25, 31, 41);
        public override Color MenuBorder => Color.FromArgb(43, 52, 66);
        public override Color MenuItemSelected => Color.FromArgb(43, 54, 70);
        public override Color MenuItemBorder => Color.FromArgb(43, 54, 70);
        public override Color SeparatorDark => Color.FromArgb(43, 52, 66);
        public override Color SeparatorLight => Color.FromArgb(43, 52, 66);
    }
}
