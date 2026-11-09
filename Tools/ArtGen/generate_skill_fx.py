"""I Blame You! 스킬 이펙트 생성기: 순간이동 · 방어막 · 베기 (귀엽지만 화려하게).

generate_magic_fx.py 와 같은 방식(색 층 + 흐린 빛 층)으로 그린다. 크기·프레임 수·중심 피벗은
예전 LevelArtConfig 프레임(Assets/Art/Sprites/Effects/{Teleport,Shield,Slash})과 같아서 코드의 재생 속도·크기를 그대로 쓴다.

  Assets/Resources/Effects/Teleport/00~07   순간이동 (128x128): 보라 고리가 빨려 들었다가 별이 터진다
  Assets/Resources/Effects/Shield/00~05     방어막 반복 (160x160): 흰색 거품. 코드가 속성색으로 물들이므로 흰·회색만 쓴다
  Assets/Resources/Effects/Slash/00~04      베기 (120x120, +x 방향): 흰 초승달 + 분홍 빛 + 끝의 별
  Assets/Resources/Effects/StunStars/00~05  적 경직 어질어질 (64x32, 반복): 머리 위 타원을 도는 별 3개 (앞쪽은 크고 밝게)

    python3 Tools/ArtGen/generate_skill_fx.py [미리보기.png]
"""
import math
import os
import random
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(__file__))
from palette import COLORS, ELEMENTS, hexrgba  # noqa: E402
from generate_magic_fx import Fx, alpha, save, SS, WHITE  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BASE = os.path.join(ROOT, "Assets", "Resources", "Effects")

ARCANE = [hexrgba(v) for v in ELEMENTS["arcane"]]  # 어두운 보라, 연보라, 심지
BERRY = COLORS["berry"]
GOLD = COLORS["gold"]
AQUA = COLORS["aqua"]

TELEPORT_FRAMES = 8
SHIELD_FRAMES = 6
SLASH_FRAMES = 5
STUN_FRAMES = 6


def heart(fx, x, y, r, col, light=False):
    """작은 하트 (귀여운 포인트)."""
    fx.circle(x - r * 0.5, y - r * 0.25, r * 0.55, col, light)
    fx.circle(x + r * 0.5, y - r * 0.25, r * 0.55, col, light)
    fx.poly([(x - r * 1.02, y - r * 0.05), (x + r * 1.02, y - r * 0.05), (x, y + r * 0.95)], col, light)


# --------------------------------------------------------------------------------------
# 순간이동 (128x128, 8프레임 한 번): 0~3 빨려 들기, 4~7 별 터짐
# --------------------------------------------------------------------------------------
def teleport(i):
    dark, base, core = ARCANE
    w = h = 128
    cx, cy = w / 2, h / 2
    fx = Fx(w, h)
    rnd = random.Random(7)
    if i < 4:
        p = i / 3
        # 세로로 긴 고리가 안으로 줄어들며 돈다
        rx, ry = 36 * (1 - p) + 12, 40 * (1 - p) + 14
        fx.l.ellipse([(cx - rx - 4) * SS, (cy - ry - 4) * SS, (cx + rx + 4) * SS, (cy + ry + 4) * SS],
                     outline=alpha(base, 200), width=int(8 * SS))
        fx.ellipse(cx, cy, rx, ry, alpha(dark, 60 + 60 * p))
        fx.c.ellipse([(cx - rx) * SS, (cy - ry) * SS, (cx + rx) * SS, (cy + ry) * SS],
                     outline=alpha(dark, 230), width=int(5 * SS))
        fx.c.ellipse([(cx - rx) * SS, (cy - ry) * SS, (cx + rx) * SS, (cy + ry) * SS],
                     outline=alpha(base, 255), width=int(3 * SS))
        # 가운데 빛기둥
        fx.ellipse(cx, cy, 6 + 6 * p, ry * 0.9, alpha(base, 200), light=True)
        fx.ellipse(cx, cy, 3 + 4 * p, ry * 0.75, alpha(core, 230))
        # 고리를 따라 도는 반짝이 (분홍·하늘색 번갈아)
        for k in range(6):
            a = 2 * math.pi * (k / 6 + p * 0.35)
            x, y = cx + math.cos(a) * rx, cy + math.sin(a) * ry
            col = BERRY if k % 2 else AQUA
            fx.circle(x, y, 4, alpha(col, 200), light=True)
            fx.star(x, y, 5 + 2 * p, WHITE)
    else:
        p = (i - 4) / 3
        # 가운데 번쩍임
        flash = 1 - p
        fx.circle(cx, cy, 26 * flash + 6, alpha(base, 220 * flash), light=True)
        fx.circle(cx, cy, 14 * flash + 2, alpha(core, 255 * flash))
        fx.star(cx, cy, 30 * flash + 8, alpha(WHITE, 255 * (1 - p * 0.7)))
        # 퍼지는 고리
        fx.ring(cx, cy, 18 + 34 * p, 4 * (1 - p) + 1, alpha(base, 255 * (1 - p)))
        # 사방으로 튀는 별·하트
        for k in range(8):
            a = 2 * math.pi * k / 8 + rnd.uniform(-0.15, 0.15)
            d = 16 + 40 * p
            x, y = cx + math.cos(a) * d, cy + math.sin(a) * d
            size = 7 * (1 - p * 0.6)
            fade = 255 * (1 - p * 0.75)
            if k % 4 == 1:
                heart(fx, x, y, size * 0.9, alpha(BERRY, fade))
            else:
                col = GOLD if k % 2 else AQUA
                fx.circle(x, y, size + 2, alpha(col, fade * 0.6), light=True)
                fx.star(x, y, size + 2, alpha(col, fade))
                fx.circle(x, y, size * 0.3, alpha(WHITE, fade))
    return fx.finish(4)


# --------------------------------------------------------------------------------------
# 방어막 (160x160, 6프레임 반복): 흰 거품. SpriteRenderer.color(속성색)로 곱해 물든다
# --------------------------------------------------------------------------------------
def shield(i):
    w = h = 160
    cx, cy = w / 2, h / 2
    fx = Fx(w, h)
    t = i / SHIELD_FRAMES
    r = 62 + 2.5 * math.sin(2 * math.pi * t)  # 숨 쉬듯 살짝 커졌다 작아짐
    light_grey = (225, 225, 235, 255)
    # 속 채움 (옅게) + 바깥 빛
    fx.ring(cx, cy, r + 2, 8, alpha(WHITE, 170), light=True)
    fx.circle(cx, cy, r, alpha(light_grey, 34))
    # 두 겹 테두리
    fx.ring(cx, cy, r, 5, alpha(light_grey, 235))
    fx.ring(cx, cy, r - 6, 2, alpha(WHITE, 160))
    # 왼쪽 위 반들반들한 반사광 + 작은 점 하나
    fx.arc(cx, cy, r - 12, 200, 250, 6, alpha(WHITE, 230))
    fx.circle(cx - r * 0.42, cy - r * 0.62, 4, WHITE)
    # 테두리를 따라 도는 반짝이 3개 (반복이 자연스럽게 1/3 바퀴 = 3개 대칭)
    for k in range(3):
        a = 2 * math.pi * (k / 3 + t / 3) - math.pi / 2
        x, y = cx + math.cos(a) * r, cy + math.sin(a) * r
        twinkle = 0.7 + 0.3 * math.sin(2 * math.pi * (t * 2 + k / 3))
        fx.circle(x, y, 7 * twinkle, alpha(WHITE, 200), light=True)
        fx.star(x, y, 10 * twinkle, WHITE)
    # 아래쪽 작은 하트 무늬 (옅게)
    heart(fx, cx + r * 0.38, cy + r * 0.45, 6, alpha(WHITE, 150))
    return fx.finish(5)


# --------------------------------------------------------------------------------------
# 베기 (120x120, 5프레임 한 번, +x 방향): 초승달이 위에서 아래로 휙
# --------------------------------------------------------------------------------------
def crescent(fx, cx, cy, r, thick, start, end, col, light=False, steps=24):
    """start→end(도, y 아래 기준) 사이의 초승달. 가운데가 가장 두껍다."""
    outer, inner = [], []
    for s in range(steps + 1):
        u = s / steps
        a = math.radians(start + (end - start) * u)
        th = thick * math.sin(math.pi * u)
        outer.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
        inner.append((cx + math.cos(a) * (r - th), cy + math.sin(a) * (r - th)))
    fx.poly(outer + inner[::-1], col, light)


def slash(i):
    w = h = 120
    cx, cy = 44, 60
    fx = Fx(w, h)
    sweep = [(-80, -30), (-80, 30), (-75, 75), (-20, 85), (35, 90)][i]  # 그려지는 호 범위
    fade = [1.0, 1.0, 0.95, 0.6, 0.3][i]
    r = 46
    start, end = sweep
    crescent(fx, cx, cy, r + 4, 18, start, end, alpha(BERRY, 200 * fade), light=True)
    crescent(fx, cx, cy, r, 14, start, end, alpha(COLORS["cream"], 255 * fade))
    crescent(fx, cx, cy, r - 2, 7, start, end, alpha(WHITE, 255 * fade))
    # 호의 끝에 반짝이는 별
    a = math.radians(end)
    tx, ty = cx + math.cos(a) * (r - 4), cy + math.sin(a) * (r - 4)
    if i < 4:
        fx.circle(tx, ty, 8, alpha(GOLD, 200 * fade), light=True)
        fx.star(tx, ty, 10, alpha(WHITE, 255 * fade))
    # 지나간 자리에 흩어지는 반짝이
    if i >= 2:
        rnd = random.Random(i)
        for k in range(4):
            a = math.radians(rnd.uniform(start, end))
            d = r - 6 + (i - 1) * 4
            x, y = cx + math.cos(a) * d, cy + math.sin(a) * d
            fx.star(x, y, 4 * fade + 1, alpha(GOLD if k % 2 else WHITE, 255 * fade))
    return fx.finish(3)


# --------------------------------------------------------------------------------------
# 경직 어질어질 (64x32, 6프레임 반복): 별 3개가 타원을 돈다. 1/3 바퀴에 반복되므로 이어 붙여도 끊기지 않는다
# --------------------------------------------------------------------------------------
def stun_stars(i):
    w, h = 64, 32
    cx, cy, rx, ry = w / 2, h / 2, 22, 7
    fx = Fx(w, h)
    t = i / STUN_FRAMES
    cols = [GOLD, AQUA, BERRY]
    stars = []
    for k in range(3):
        a = 2 * math.pi * (k / 3 + t / 3)
        x, y = cx + math.cos(a) * rx, cy + math.sin(a) * ry
        depth = (math.sin(a) + 1) / 2  # 0 = 뒤(위), 1 = 앞(아래)
        stars.append((depth, x, y, cols[k]))
    for depth, x, y, col in sorted(stars):  # 뒤에 있는 별부터
        r = 4.5 + 3.5 * depth
        fx.circle(x, y, r * 0.8, alpha(col, int(120 + 100 * depth)), light=True)
        fx.star(x, y, r + 1.2, alpha((24, 20, 26, 255), int(150 + 105 * depth)))
        fx.star(x, y, r, alpha(col, int(170 + 85 * depth)))
        fx.circle(x, y, r * 0.22, alpha(WHITE, int(160 + 95 * depth)))
    return fx.finish(2)


def generate():
    result = {
        "Teleport": [teleport(i) for i in range(TELEPORT_FRAMES)],
        "Shield": [shield(i) for i in range(SHIELD_FRAMES)],
        "Slash": [slash(i) for i in range(SLASH_FRAMES)],
        "StunStars": [stun_stars(i) for i in range(STUN_FRAMES)],
    }
    for name, frames in result.items():
        save(os.path.join(BASE, name), frames)
    return result


def preview(result, path):
    rows = list(result.items())
    cell = 170
    image = Image.new("RGBA", (cell * 8 + 90, cell * (len(rows) + 1)), COLORS["night"])
    draw = ImageDraw.Draw(image)
    for r, (name, frames) in enumerate(rows):
        draw.text((4, r * cell + 4), name, fill=(255, 255, 255, 255))
        for c, frame in enumerate(frames):
            image.alpha_composite(frame, (90 + c * cell + (cell - frame.width) // 2, r * cell + (cell - frame.height) // 2))
    # 방어막을 속성색으로 물들인 모습 (게임에서 SpriteRenderer.color 곱하기)
    r = len(rows)
    draw.text((4, r * cell + 4), "Shield tint", fill=(255, 255, 255, 255))
    for c, key in enumerate(ELEMENTS):
        tint = hexrgba(ELEMENTS[key][1])
        frame = result["Shield"][0]
        tinted = frame.copy()
        px = [(int(a * tint[0] / 255), int(b * tint[1] / 255), int(d * tint[2] / 255), int(e * 0.6))
              for a, b, d, e in tinted.getdata()]
        tinted.putdata(px)
        image.alpha_composite(tinted, (90 + c * cell + (cell - frame.width) // 2, r * cell + (cell - frame.height) // 2))
    image.save(path)


if __name__ == "__main__":
    out = generate()
    if len(sys.argv) > 1:
        preview(out, sys.argv[1])
    print("done")
