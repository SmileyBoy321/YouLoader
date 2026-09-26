"""Draws static/assets/og.png, the 1200x630 preview shown when the site is shared on social media or in chat apps.

    python website/make_og.py
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ASSETS = Path(__file__).resolve().parent / "static" / "assets"
PAPER, INK, MUTED, RED = (243, 239, 230), (22, 20, 15), (111, 105, 93), (232, 55, 44)

W, H = 1200, 630
img = Image.new("RGB", (W, H), PAPER)
draw = ImageDraw.Draw(img)

serif = ImageFont.truetype(str(ASSETS / "fonts" / "instrument-serif.woff2"), 124)
serif_italic = ImageFont.truetype(str(ASSETS / "fonts" / "instrument-serif-italic.woff2"), 124)
brand = ImageFont.truetype(str(ASSETS / "fonts" / "instrument-serif.woff2"), 46)
mono = ImageFont.truetype(str(ASSETS / "fonts" / "geist-mono.woff2"), 21)

logo = Image.open(ASSETS / "logo-512.png").convert("RGBA").resize((52, 52), Image.LANCZOS)
img.paste(logo, (64, 58), logo)
draw.text((128, 58), "YouLoader", font=brand, fill=INK)

draw.text((62, 190), "Downloads.", font=serif, fill=INK)
draw.text((62, 310), "Nothing else.", font=serif_italic, fill=RED)
draw.text((66, 480), "FREE YOUTUBE & SOUNDCLOUD DOWNLOADER", font=mono, fill=MUTED)
draw.text((66, 514), "MP3 · MP4  —  no ads, no tracking", font=mono, fill=MUTED)

# The app screenshot, tilted, with a hard offset shadow like on the site
shot = Image.open(ASSETS / "screenshot.png").convert("RGBA")
shot = shot.resize((int(shot.width * 0.62), int(shot.height * 0.62)), Image.LANCZOS)
framed = Image.new("RGBA", (shot.width + 4, shot.height + 4), INK + (255,))
framed.paste(shot, (2, 2))
shadow = Image.new("RGBA", framed.size, INK + (255,))
canvas = Image.new("RGBA", (framed.width + 14, framed.height + 14), (0, 0, 0, 0))
canvas.paste(shadow, (14, 14))
canvas.paste(framed, (0, 0))
canvas = canvas.rotate(2.2, resample=Image.BICUBIC, expand=True)
img.paste(canvas, (W - canvas.width - 40, 70), canvas)

img.save(ASSETS / "og.png", optimize=True)
print("wrote", ASSETS / "og.png")
