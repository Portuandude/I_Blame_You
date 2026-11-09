"""I Blame You! 시각 효과 묶음 생성기 (Docs/ArtStyle.md: 귀엽지만 화려하게).

generate_magic_fx.py 의 Fx(색 층 + 흐린 빛 층)로 그린다. 모두 런타임 로드(EffectLibrary.LoadRuntimeFrames, 중앙 피벗, 100px = 1유닛).

  Assets/Resources/Effects/Dust/00~05      착지 먼지 (96x40, 아래 가운데가 발밑)
  Assets/Resources/Effects/LevelUp/00~09   레벨업 빛기둥 (120x240, 아래쪽 1/4 지점이 발밑)
  Assets/Resources/Effects/DeathPop/00~07  적 사망 별·하트 파편 (128x128)
  Assets/Resources/Effects/Sparkle/00~05   유물 상자 반짝임 반복 (96x96)
  Assets/Resources/Effects/CheckpointPillar/00~09  체크포인트 켤 때 aqua 빛기둥 + 큰 반짝임 3개 (120x240, 아래쪽 1/4 지점이 수정 발밑)
  Assets/Resources/Effects/ClearBurst/00~09        전투 구역 돌파 축하: 별·하트가 크게 터짐 (192x192)

    python3 Tools/ArtGen/generate_vfx.py [미리보기.png]
"""
import math
import os
import random
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from generate_magic_fx import Fx, alpha  # noqa: E402
from palette import COLORS  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BASE = os.path.join(ROOT, "Assets", "Resources", "Effects")

GOLD, AQUA, CREAM, BERRY = COLORS["gold"], COLORS["aqua"], COLORS["cream"], COLORS["berry"]
SAND, LILAC = COLORS["sand"], COLORS["lilac"]


def faded(img, k):
    """색을 어둡게 섞지 않고 투명도만 줄인다 (반투명으로 그리면 줄일 때 검은 바탕이 섞여 탁해진다)."""
    if k >= 1:
        return img
    a = img.getchannel("A").point(lambda v: int(v * max(0.0, k)))
    img.putalpha(a)
    return img


def heart(fx, x, y, r, col, light=False):
    """작은 하트 (원 두 개 + 아래 꼭짓점)."""
    fx.circle(x - r * 0.5, y - r * 0.2, r * 0.55, col, light)
    fx.circle(x + r * 0.5, y - r * 0.2, r * 0.55, col, light)
    fx.poly([(x - r * 1.02, y), (x + r * 1.02, y), (x, y + r)], col, light)


def dust_frames():
    """발 양옆으로 퍼지는 둥근 먼지 구름 (sand → cream), 6프레임."""
    frames = []
    w, h = 96, 56
    for i in range(6):
        t = i / 5
        fx = Fx(w, h)
        a = 255
        for side in (-1, 1):
            for k, (dx, r) in enumerate(((10, 7), (22, 6), (32, 4.5))):
                x = w / 2 + side * (dx + 14 * t * (1 + k * 0.4))
                y = h - 6 - 8 * t * (1 + k * 0.3) - k * 1.5
                rr = r * (0.7 + 0.6 * t)
                fx.circle(x, y, rr, alpha(SAND, a))
                fx.circle(x - side * rr * 0.3, y - rr * 0.35, rr * 0.5, alpha(CREAM, a))
        frames.append(faded(fx.finish(glow=1), 0.9 * (1 - t) ** 1.3))
    return frames


def levelup_frames(color=GOLD, stars=None):
    """금빛 기둥이 솟았다가 가늘어지며 사라지고, 별이 위로 떠오른다. 10프레임."""
    frames = []
    w, h = 120, 240
    foot = h * 0.75
    rng = random.Random(7)
    if stars is None:
        stars = [(rng.uniform(-40, 40), rng.uniform(0, 1), rng.choice((5, 7, 9))) for _ in range(9)]
    for i in range(10):
        t = i / 9
        fx = Fx(w, h)
        grow = min(1.0, t * 3)               # 0.3 동안 솟음
        fade = 1.0 if t < 0.5 else 1 - (t - 0.5) / 0.5
        width = 26 * (1.2 - 0.7 * t)
        top = foot - (h * 0.72) * grow
        fx.poly([(w / 2 - width, foot), (w / 2 + width, foot), (w / 2 + width * 0.5, top), (w / 2 - width * 0.5, top)],
                alpha(color, 140), light=True)
        fx.poly([(w / 2 - width * 0.45, foot), (w / 2 + width * 0.45, foot), (w / 2 + width * 0.2, top), (w / 2 - width * 0.2, top)],
                alpha(CREAM, 230))
        # 발밑 고리
        fx.ellipse(w / 2, foot, 34 + 18 * t, 8 + 3 * t, alpha(color, 220), light=True)
        fx.ellipse(w / 2, foot, 24 + 18 * t, 4 + 2 * t, alpha(CREAM, 220))
        for sx, phase, r in stars:
            p = (t * 1.3 + phase) % 1.0
            y = foot - p * h * 0.7
            a = 255 * math.sin(math.pi * p)
            fx.star(w / 2 + sx, y, r, alpha(color, a), light=True)
            fx.star(w / 2 + sx, y, r * 0.6, alpha(CREAM, a))
        frames.append(faded(fx.finish(glow=4), fade))
    return frames


def deathpop_frames():
    """펑 하고 터지는 고리 + 사방으로 튀는 별·하트, 8프레임."""
    frames = []
    w, h = 128, 128
    cx, cy = w / 2, h / 2
    rng = random.Random(11)
    bits = []
    for k in range(8):
        ang = 2 * math.pi * k / 8 + rng.uniform(-0.25, 0.25)
        bits.append((ang, rng.uniform(34, 52), "heart" if k % 3 == 0 else "star", rng.choice((GOLD, AQUA, BERRY, CREAM))))
    for i in range(8):
        t = i / 7
        fx = Fx(w, h)
        # 터지는 고리 (처음 3프레임)
        if t < 0.45:
            r = 10 + 60 * t
            fx.ring(cx, cy, r, 6 * (1 - t / 0.45) + 1, alpha(CREAM, 230 * (1 - t / 0.45)))
            fx.circle(cx, cy, 14 * (1 - t / 0.45), alpha(CREAM, 200), light=True)
        for ang, dist, kind, col in bits:
            d = dist * (1 - (1 - t) ** 2)
            x = cx + math.cos(ang) * d
            y = cy - math.sin(ang) * d + 12 * t * t  # 살짝 떨어진다
            a = 255
            size = 7 * (1.0 - 0.4 * t)
            if kind == "heart":
                heart(fx, x, y, size + 2, alpha(col, a * 0.5), light=True)
                heart(fx, x, y, size, alpha(col, a))
            else:
                fx.star(x, y, size + 3, alpha(col, a * 0.5), light=True)
                fx.star(x, y, size + 1, alpha(col, a))
        frames.append(faded(fx.finish(glow=3), 1.0 if t < 0.6 else 1 - (t - 0.6) / 0.45))
    return frames


def sparkle_frames():
    """유물 상자 위에서 반짝이는 별 3개 (서로 엇갈려 깜빡임), 반복 6프레임."""
    frames = []
    w, h = 96, 96
    spots = [(30, 34, 0.0, 9), (66, 26, 0.33, 7), (56, 60, 0.66, 6)]
    for i in range(6):
        t = i / 6
        fx = Fx(w, h)
        for x, y, phase, r in spots:
            k = max(0.0, math.sin(2 * math.pi * (t + phase)))
            if k <= 0.05:
                continue
            fx.star(x, y, r * k + 3, alpha(GOLD, 200 * k), light=True)
            fx.star(x, y, r * k, alpha(CREAM, 255 * k))
            fx.circle(x, y, 1.6 * k, alpha(CREAM, 255))
        frames.append(fx.finish(glow=3))
    return frames


def checkpoint_frames():
    """체크포인트를 켤 때: aqua 기둥 + 큰 반짝임 3개가 엇갈려 떠오른다 (레벨업 기둥과 같은 모양, 색만 다름)."""
    return levelup_frames(AQUA, stars=[(-26, 0.0, 11), (24, 0.33, 9), (-4, 0.66, 12)])


def clearburst_frames():
    """전투 구역 돌파: 가운데에서 큰 고리 두 겹 + 별·하트 14개가 크게 퍼져 반짝이다 사라진다. 10프레임."""
    frames = []
    w, h = 192, 192
    cx, cy = w / 2, h / 2
    rng = random.Random(23)
    bits = []
    for k in range(14):
        ang = 2 * math.pi * k / 14 + rng.uniform(-0.2, 0.2)
        bits.append((ang, rng.uniform(56, 84), "heart" if k % 3 == 1 else "star", (GOLD, AQUA, BERRY, CREAM)[k % 4],
                     rng.uniform(0, 1)))
    for i in range(10):
        t = i / 9
        fx = Fx(w, h)
        if t < 0.5:
            q = t / 0.5
            fx.ring(cx, cy, 14 + 80 * q, 8 * (1 - q) + 1, alpha(GOLD, 220 * (1 - q)))
            fx.ring(cx, cy, 8 + 56 * q, 5 * (1 - q) + 1, alpha(AQUA, 230 * (1 - q)))
            fx.circle(cx, cy, 22 * (1 - q), alpha(CREAM, 220), light=True)
        for ang, dist, kind, col, phase in bits:
            d = dist * (1 - (1 - t) ** 2.2)
            x = cx + math.cos(ang) * d
            y = cy - math.sin(ang) * d + 10 * t * t
            twinkle = 0.75 + 0.25 * math.sin(2 * math.pi * (t * 2 + phase))
            size = 9 * (1.0 - 0.35 * t) * twinkle
            if kind == "heart":
                heart(fx, x, y, size + 3, alpha(col, 130), light=True)
                heart(fx, x, y, size + 1, alpha(col, 255))
            else:
                fx.star(x, y, size + 5, alpha(col, 130), light=True)
                fx.star(x, y, size + 2, alpha(col, 255))
                fx.circle(x, y, 1.8, alpha(CREAM, 255))
        frames.append(faded(fx.finish(glow=4), 1.0 if t < 0.55 else 1 - (t - 0.55) / 0.5))
    return frames


SETS = {"Dust": dust_frames, "LevelUp": levelup_frames, "DeathPop": deathpop_frames, "Sparkle": sparkle_frames,
        "CheckpointPillar": checkpoint_frames, "ClearBurst": clearburst_frames}


def generate():
    out = {}
    for name, make in SETS.items():
        folder = os.path.join(BASE, name)
        os.makedirs(folder, exist_ok=True)
        frames = make()
        for i, img in enumerate(frames):
            img.save(os.path.join(folder, f"{i:02d}.png"))
        out[name] = frames
    return out


def preview(sets, path):
    rows = list(sets.values())
    cw = max(im.width for row in rows for im in row)
    ch = [max(im.height for im in row) for row in rows]
    sheet = Image.new("RGBA", (cw * max(len(r) for r in rows), sum(ch)), (40, 34, 70, 255))
    y = 0
    for row, rh in zip(rows, ch):
        for i, im in enumerate(row):
            sheet.alpha_composite(im, (i * cw, y))
        y += rh
    sheet.save(path)


if __name__ == "__main__":
    sets = generate()
    if len(sys.argv) > 1:
        preview(sets, sys.argv[1])
    print("done")
