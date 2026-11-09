"""I Blame You! 손맛 이펙트 생성기 (Docs/ArtStyle.md 빛·반짝임 규칙).

  Assets/Resources/Effects/Fx/Dust/00~05      착지 먼지 (128x48, 아래 가운데가 발)
  Assets/Resources/Effects/Fx/LevelUp/00~09   레벨업 빛기둥 (120x320, 아래 가운데가 발)
  Assets/Resources/Effects/Fx/DeathPop/00~07  적이 쓰러질 때 별·하트 파편 (160x160 중앙)

    python3 Tools/ArtGen/generate_juice_fx.py [미리보기.png]
"""
import math
import os
import random
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(__file__))
from palette import COLORS  # noqa: E402
from generate_magic_fx import Fx, alpha, ease_out  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BASE = os.path.join(ROOT, "Assets", "Resources", "Effects", "Fx")
WHITE = (255, 255, 255, 255)


def heart(fx, x, y, r, col):
    fx.circle(x - r * 0.5, y - r * 0.2, r * 0.55, col)
    fx.circle(x + r * 0.5, y - r * 0.2, r * 0.55, col)
    fx.poly([(x - r * 1.02, y - r * 0.05), (x + r * 1.02, y - r * 0.05), (x, y + r * 0.95)], col)


def dust(i, n=6):
    fx = Fx(128, 48)
    p = i / (n - 1)
    puff = alpha(COLORS["cream"], 220 * (1 - p))
    shade = alpha(COLORS["sand"], 200 * (1 - p))
    for side in (-1, 1):
        for k in range(3):
            d = 10 + ease_out(p) * (30 + k * 12)
            x = 64 + side * d
            y = 40 - k * 4 - p * 8
            r = (7 - k * 1.5) * (1 - p * 0.5) + 2
            fx.circle(x, y + 1.5, r, shade)
            fx.circle(x, y, r * 0.85, puff)
    if i <= 2:
        fx.star(64 + (i - 1) * 6, 38, 6 - i * 1.5, alpha(WHITE, 220))
    return fx.finish(1)


def level_up(i, n=10):
    w, h = 120, 320
    fx = Fx(w, h)
    p = i / (n - 1)
    gold, aqua, cream = COLORS["gold"], COLORS["aqua"], COLORS["cream"]
    grow = ease_out(min(1, p * 2.5))
    fade = 1 if p < 0.6 else 1 - (p - 0.6) / 0.4
    top = h - 20 - grow * 280
    half = 22 * (1 - p * 0.6)
    fx.poly([(60 - half, h - 14), (60 + half, h - 14), (60 + half * 0.4, top), (60 - half * 0.4, top)], alpha(gold, 120 * fade), light=True)
    fx.poly([(60 - half * 0.45, h - 14), (60 + half * 0.45, h - 14), (60 + half * 0.15, top + 10), (60 - half * 0.15, top + 10)], alpha(cream, 230 * fade))
    # 바닥 고리
    fx.ellipse(60, h - 14, 34 + p * 16, 8 + p * 3, alpha(gold, 200 * fade), light=True)
    fx.ellipse(60, h - 14, 24 + p * 14, 5 + p * 2, alpha(cream, 220 * fade))
    # 올라가는 별
    rnd = random.Random(3)
    for k in range(9):
        phase = (p * 1.6 + k / 9) % 1
        x = 60 + rnd.uniform(-34, 34)
        y = h - 20 - phase * 270
        r = rnd.choice((3, 5, 7)) * (1 - phase * 0.4)
        col = gold if k % 3 else aqua
        fx.star(x, y, r, alpha(col, 255 * fade * (1 - phase * 0.5)))
    if i <= 1:
        fx.circle(60, h - 20, 26 - i * 8, alpha(WHITE, 220))
    return fx.finish(3)


def death_pop(i, n=8):
    fx = Fx(160, 160)
    p = i / (n - 1)
    rnd = random.Random(8)
    if i <= 1:
        fx.circle(80, 80, 22 - i * 8, alpha(WHITE, 230))
        fx.star(80, 80, 34 - i * 10, alpha(COLORS["cream"], 240))
    fx.ring(80, 80, 12 + ease_out(p) * 46, 4 * (1 - p) + 1, alpha(COLORS["cream"], 220 * (1 - p)))
    for k in range(8):
        a = 2 * math.pi * k / 8 + rnd.uniform(-0.25, 0.25)
        d = 8 + ease_out(p) * rnd.uniform(40, 56)
        x, y = 80 + math.cos(a) * d, 80 + math.sin(a) * d + p * p * 12
        fade = 1 - p * 0.9
        if k % 2:
            heart(fx, x, y, 7 * fade + 2, alpha(COLORS["berry"], 255 * fade))
        else:
            fx.star(x, y, 8 * fade + 2, alpha(COLORS["gold"], 255 * fade))
    return fx.finish(2)


def generate():
    sets = {"Dust": [dust(i) for i in range(6)], "LevelUp": [level_up(i) for i in range(10)], "DeathPop": [death_pop(i) for i in range(8)]}
    for name, frames in sets.items():
        folder = os.path.join(BASE, name)
        os.makedirs(folder, exist_ok=True)
        for i, img in enumerate(frames):
            img.save(os.path.join(folder, f"{i:02d}.png"))
    return sets


def preview(sets, path):
    cell_w, cell_h = 170, 330
    image = Image.new("RGBA", (cell_w * 10, cell_h * 3), COLORS["night"])
    for r, frames in enumerate(sets.values()):
        for c, f in enumerate(frames):
            image.alpha_composite(f, (c * cell_w + (cell_w - f.width) // 2, r * cell_h + cell_h - f.height))
    image.save(path)


if __name__ == "__main__":
    out = generate()
    if len(sys.argv) > 1:
        preview(out, sys.argv[1])
    print("done")
