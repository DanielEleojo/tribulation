#!/usr/bin/env python3
# Seamless dark-stone flagstone texture (albedo + normal) for the runner road.
# Toroidal Voronoi: cells = stones, near-equal nearest-two distances = dark mortar grooves.
import numpy as np
from PIL import Image
import os

N = 512
rng = np.random.default_rng(7)
PTS = 36
pts = rng.random((PTS, 2))                      # seed points in [0,1)
cellval = rng.uniform(0.7, 1.15, PTS)           # per-stone brightness variation

ys, xs = np.mgrid[0:N, 0:N].astype(np.float32) / N
d1 = np.full((N, N), 9.0, np.float32)           # nearest
d2 = np.full((N, N), 9.0, np.float32)           # second nearest
owner = np.zeros((N, N), np.int32)
for i, (px, py) in enumerate(pts):
    dx = np.abs(xs - px); dx = np.minimum(dx, 1 - dx)   # toroidal wrap
    dy = np.abs(ys - py); dy = np.minimum(dy, 1 - dy)
    d = np.sqrt(dx * dx + dy * dy)
    closer = d < d1
    d2 = np.where(closer, d1, np.minimum(d2, d))
    owner = np.where(closer, i, owner)
    d1 = np.where(closer, d, d1)

mortar = np.clip((d2 - d1) / 0.045, 0, 1)        # 0 in grooves, 1 inside stone
mortar = mortar ** 0.6

# fine grain: irrational-frequency sines so it doesn't read as a regular plaid
grain = np.zeros((N, N), np.float32)
for f, a in [(11, 0.5), (23, 0.3), (41, 0.2)]:
    ph = rng.random(2) * 2 * np.pi
    grain += a * np.sin(2 * np.pi * f * xs + ph[0] + 1.7 * ys) \
                * np.sin(2 * np.pi * f * ys + ph[1] + 1.3 * xs)
grain = (grain - grain.min()) / (grain.max() - grain.min())

stone_b = cellval[owner]                          # per-cell brightness
height = mortar * (0.6 + 0.4 * grain)             # high on stone, 0 in grooves

# Albedo: dark charcoal stone, near-black grooves. Low values, modest contrast.
base = np.array([0.30, 0.29, 0.34])               # cool stone hue
val = (0.10 + 0.22 * height) * (0.8 + 0.4 * (stone_b - 0.9))  # ~0.07..0.34
alb = np.clip(val[..., None] * base, 0, 1) ** (1 / 2.2)        # to sRGB-ish
alb = (alb * 255).astype(np.uint8)

# Normal map from height gradient (seamless via np.roll)
gx = (np.roll(height, -1, 1) - np.roll(height, 1, 1))
gy = (np.roll(height, -1, 0) - np.roll(height, 1, 0))
strength = 2.5
nx = -gx * strength; ny = -gy * strength; nz = np.ones_like(height)
ln = np.sqrt(nx * nx + ny * ny + nz * nz)
nrm = np.stack([nx / ln, ny / ln, nz / ln], -1)
nrm = ((nrm * 0.5 + 0.5) * 255).astype(np.uint8)

out = "tribulation-unity/Assets/Resources/road"
os.makedirs(out, exist_ok=True)
Image.fromarray(alb).save(f"{out}/road_albedo.png")
Image.fromarray(nrm).save(f"{out}/road_normal.png")
print("wrote", out, "road_albedo.png + road_normal.png")
