"""Regenerate the installer artwork and repository mark. Requires Pillow."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parent.parent

def mark(draw, x, y, size):
    def p(points):
        return [(x + a * size / 256, y + b * size / 256) for a, b in points]
    draw.rounded_rectangle((x, y, x + size, y + size), radius=size * .23, fill="#147D70")
    for points in [[(93, 59), (59, 59), (59, 93)], [(163, 59), (197, 59), (197, 93)],
                   [(197, 163), (197, 197), (163, 197)], [(93, 197), (59, 197), (59, 163)]]:
        draw.line(p(points), fill="white", width=max(2, round(size * 12 / 256)), joint="curve")
    draw.line(p([(91, 128), (117, 153), (166, 103)]), fill="white", width=max(2, round(size * 14 / 256)), joint="curve")

small = Image.new("RGB", (120, 120), "#191F29")
mark(ImageDraw.Draw(small), 17, 17, 86)
small.save(root / "installer" / "wizard-small.bmp")
large = Image.new("RGB", (328, 628), "#11151C")
d = ImageDraw.Draw(large)
for offset in range(0, 600, 60):
    d.line([(0, offset + 200), (328, offset - 40)], fill="#1B2831", width=1)
mark(d, 84, 212, 160)
large.save(root / "installer" / "wizard-large.bmp")
logo = Image.new("RGBA", (256, 256))
mark(ImageDraw.Draw(logo), 4, 4, 248)
(root / "docs" / "images").mkdir(parents=True, exist_ok=True)
logo.save(root / "docs" / "images" / "logo.png")
