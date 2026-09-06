from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parent.parent
(root / "Assets").mkdir(exist_ok=True)
image = Image.new("RGBA", (256, 256))
d = ImageDraw.Draw(image)
d.rounded_rectangle((4, 4, 252, 252), radius=58, fill="#147D70")
for points in [[(93, 59), (59, 59), (59, 93)], [(163, 59), (197, 59), (197, 93)], [(197, 163), (197, 197), (163, 197)], [(93, 197), (59, 197), (59, 163)]]:
    d.line(points, fill="white", width=12, joint="curve")
d.line([(91, 128), (117, 153), (166, 103)], fill="white", width=14, joint="curve")
image.save(root / "Assets" / "SnipStudio.ico", sizes=[(16,16), (24,24), (32,32), (48,48), (64,64), (128,128), (256,256)])
