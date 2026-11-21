from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import numpy as np
root = Path(__file__).resolve().parents[2]
out = root / "Assets/FidgetToys/Materials"
out.mkdir(parents=True, exist_ok=True)
rng = np.random.default_rng(41)
w, h = 1536, 576
noise = rng.normal(0, 1.65, (h,w,1))
im = Image.fromarray(np.uint8(np.clip(np.array([255,212,39]) + noise,0,255)))
d = ImageDraw.Draw(im)
ink = (24,65,52)
def line(x,y,text,size):
    f = ImageFont.truetype("C:/Windows/Fonts/arialnb.ttf",size)
    d.text((x,y),text,font=f,fill=ink,anchor="mm")
d.rounded_rectangle((100,88,1436,488),radius=38,outline=(213,166,30),width=3)
line(433,252,"14 oz",114)
line(433,350,"/ NET WT. (400G)",58)
d.line((728,160,728,410),fill=(205,161,39),width=3)
line(1055,211,"SALTED",51)
line(1055,318,"BUTTER",155)
im.save(out / "ButterPrint.png")

