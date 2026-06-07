#!/usr/bin/env python3
"""Generate the Tribulation app icon — an opaque 1024x1024 PNG for the App Store
(no alpha, no pre-rounded corners; iOS applies the rounded mask itself).

Motif: a glowing jade Qi aura behind an upright jian (sword) over a dark teal-ink
field — the cultivator's dao, bold enough to read at home-screen size.

Run:  python3 tools/gen_icon.py
"""
import os, math
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(HERE, "assets", "icon.png")

S = 1024
SS = 4               # supersample for clean edges
N = S * SS

INK_TOP = (15, 38, 52)      # deep teal (matches boot splash 0.09,0.18,0.28)
INK_BOT = (5, 9, 14)        # near-black
JADE = (90, 220, 200)       # Qi jade-cyan
JADE_HI = (190, 255, 245)
GOLD = (235, 200, 110)
GOLD_DK = (150, 120, 50)
BLADE = (225, 240, 245)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def main():
    img = Image.new("RGB", (N, N), INK_BOT)
    px = img.load()
    # vertical ink gradient
    for y in range(N):
        c = lerp(INK_TOP, INK_BOT, (y / N) ** 0.9)
        for x in range(N):
            px[x, y] = c

    # radial jade Qi glow (drawn on an L mask, tinted, screen-blended)
    glow = Image.new("L", (N, N), 0)
    gd = ImageDraw.Draw(glow)
    cx, cy = N // 2, int(N * 0.46)
    R = int(N * 0.40)
    for i in range(R, 0, -1):
        a = int(170 * (1 - i / R) ** 2.2)
        gd.ellipse([cx - i, cy - i, cx + i, cy + i], fill=a)
    glow = glow.filter(ImageFilter.GaussianBlur(N // 90))
    jade_layer = Image.new("RGB", (N, N), JADE)
    img = Image.composite(jade_layer, img, glow.point(lambda v: int(v * 0.85)))

    d = ImageDraw.Draw(img)

    # thin halo ring (Qi formation)
    rr = int(N * 0.34)
    d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], outline=JADE_HI, width=max(2, N // 320))
    rr2 = int(N * 0.30)
    d.ellipse([cx - rr2, cy - rr2, cx + rr2, cy + rr2], outline=lerp(JADE, INK_TOP, 0.4), width=max(1, N // 600))

    # --- upright jian (sword) ---
    midx = cx
    blade_top = int(N * 0.16)
    guard_y = int(N * 0.64)
    blade_w = int(N * 0.038)
    # blade (tapered): bright jade-white with a luminous core
    d.polygon([
        (midx - blade_w, blade_top + blade_w * 2),
        (midx, blade_top),                      # point
        (midx + blade_w, blade_top + blade_w * 2),
        (midx + int(blade_w * 0.8), guard_y),
        (midx - int(blade_w * 0.8), guard_y),
    ], fill=BLADE)
    # luminous core line
    d.line([(midx, blade_top + blade_w * 2), (midx, guard_y)],
           fill=JADE_HI, width=max(2, N // 350))
    # crossguard (gold)
    gw = int(N * 0.12)
    gh = int(N * 0.022)
    d.rounded_rectangle([midx - gw, guard_y, midx + gw, guard_y + gh],
                        radius=gh // 2, fill=GOLD)
    # handle
    hw = int(N * 0.018)
    handle_bot = int(N * 0.78)
    d.rounded_rectangle([midx - hw, guard_y + gh, midx + hw, handle_bot],
                        radius=hw, fill=lerp(GOLD_DK, INK_BOT, 0.3))
    # pommel
    pr = int(N * 0.028)
    d.ellipse([midx - pr, handle_bot - pr, midx + pr, handle_bot + pr], fill=GOLD)

    # bottom mountain silhouette (the long road / summit)
    mh = int(N * 0.86)
    d.polygon([(0, N), (0, mh), (int(N * 0.30), int(N * 0.74)),
               (int(N * 0.5), int(N * 0.82)), (int(N * 0.72), int(N * 0.72)),
               (N, mh), (N, N)], fill=lerp(INK_BOT, INK_TOP, 0.25))

    # subtle vignette
    vig = Image.new("L", (N, N), 0)
    vd = ImageDraw.Draw(vig)
    vd.ellipse([-N // 5, -N // 5, N + N // 5, N + N // 5], fill=255)
    vig = vig.filter(ImageFilter.GaussianBlur(N // 12))
    dark = Image.new("RGB", (N, N), (0, 0, 0))
    img = Image.composite(img, dark, vig)

    img = img.resize((S, S), Image.LANCZOS)
    img.save(OUT)
    print("wrote", os.path.relpath(OUT, HERE), img.size, img.mode)


if __name__ == "__main__":
    main()
