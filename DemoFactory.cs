using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SnipStudio;

public static class DemoFactory
{
    public static BitmapSource Create()
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(B("#FBF9F3"), null, new Rect(0, 0, 1080, 660));
            Text(dc, "elsewhere", 48, 34, 27, "#29453A", true); Text(dc, "Places to go     Our journal     About us", 650, 44, 14, "#6A7B6F");
            dc.DrawLine(new Pen(B("#E4E7DB"), 1), new Point(48, 93), new Point(1032, 93));
            Text(dc, "LESS SCROLLING. MORE WANDERING.", 48, 132, 12, "#6E8C72", true);
            Text(dc, "A little change of scenery.", 48, 161, 47, "#28483A", true);
            Text(dc, "Slow mornings, open trails, and somewhere new to call your favorite.", 50, 229, 17, "#798878");
            string[] names = ["The quiet coast", "Into the green", "A cabin, somewhere"];
            string[] captions = ["Salt air & slower days", "Find your own path", "Room to do nothing"];
            string[] skies = ["#D4E5DE", "#DDE7CA", "#E7D9C2"];
            string[] hills = ["#7BAFA8", "#88A175", "#A0A481"];
            string[] fronts = ["#457D78", "#3A654D", "#5C735B"];
            for (int i = 0; i < 3; i++)
            {
                double x = 48 + i * 336;
                dc.DrawRoundedRectangle(Brushes.White, new Pen(B("#E3E7DA"), 1), new Rect(x, 294, 312, 295), 14, 14);
                dc.PushClip(new RectangleGeometry(new Rect(x + 9, 303, 294, 197), 9, 9));
                dc.DrawRectangle(B(skies[i]), null, new Rect(x, 300, 312, 200));
                dc.DrawEllipse(B("#F9F0C6"), null, new Point(x + 235, 349), 23, 23);
                var mountain = Geometry.Parse($"M{x - 20},500 L{x + 65},352 {x + 135},432 {x + 204},374 {x + 332},500 Z");
                dc.DrawGeometry(B(hills[i]), null, mountain);
                dc.DrawGeometry(B(fronts[i]), null, Geometry.Parse($"M{x - 10},500 L{x + 60},438 {x + 122},465 {x + 232},405 {x + 340},500 Z"));
                if (i == 0) { dc.DrawRectangle(B("#91C2BA"), null, new Rect(x, 463, 320, 50)); dc.DrawLine(new Pen(B("#D4E6D6"), 2), new Point(x + 30, 481), new Point(x + 212, 481)); }
                if (i == 2) { dc.DrawRectangle(B("#ECD8B0"), null, new Rect(x + 134, 430, 69, 62)); dc.DrawGeometry(B("#344F40"), null, Geometry.Parse($"M{x + 121},433 L{x + 168},392 {x + 217},433 Z")); dc.DrawRectangle(B("#A88558"), null, new Rect(x + 160, 457, 20, 35)); }
                dc.Pop(); Text(dc, names[i], x + 19, 516, 21, "#2C4C3C", true); Text(dc, captions[i], x + 19, 553, 13, "#8C9A88");
            }
            Text(dc, "A thoughtfully made sample image for trying your annotation tools.", 48, 621, 12, "#A0AB9A");
        }
        var image = new RenderTargetBitmap(1080, 660, 96, 96, PixelFormats.Pbgra32); image.Render(visual); image.Freeze(); return image;
    }
    private static Brush B(string color) => new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    private static void Text(DrawingContext dc, string text, double x, double y, double size, string color, bool bold = false) => dc.DrawText(new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal), size, B(color), 1), new Point(x, y));
}
