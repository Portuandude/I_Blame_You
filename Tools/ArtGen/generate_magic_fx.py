"""I Blame You! 마법 이펙트 생성기: 발사 → 비행 → 적중 3단계 (Docs/ArtStyle.md "이펙트 요약").

pixelfx(patch-gg/pixelfx)의 방식대로 색(color) 층과 빛(light) 층을 따로 그린 뒤, 빛 층을 흐리게 해 아래에 깔아 합친다.
속성색은 palette.ELEMENTS 의 (어두운 쪽, 기본, 심지) 3단을 안에서 밖으로 쓴다.

  Assets/Resources/Effects/Cast/<속성>/00~05    손 앞에 빛이 모였다 터짐 (96x96)
  Assets/Resources/Effects/Flight/<속성>/00~07  비행 반복 (+x 방향, 기존 크기 유지. 땅은 비행이 없다)
  Assets/Resources/Effects/Hit/<속성>/00~07     적중 터짐 (140x140)

    python3 Tools/ArtGen/generate_magic_fx.py [미리보기.png]
"""
import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(__file__))
from palette import COLORS, OUTLINE, hexrgba, ELEMENTS  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BASE = os.path.join(ROOT, "Assets", "Resources", "Effects")
SS = 4
WHITE = (255, 255, 255, 255)

# 폴더 이름(코드의 Element enum 이름) → 팔레트 이름
FOLDERS = {"Fire": "fire", "Water": "water", "Wind": "wind", "Earth": "earth", "Neutral": "arcane"}


def ramp(folder):
    return [hexrgba(v) for v in ELEMENTS[FOLDERS[folder]]]


def alpha(color, a):
    return color[:3] + (max(0, min(255, int(a))),)


class Fx:
    """색 층 + 빛 층. 좌표는 최종 픽셀 단위 (y 아래로)."""

    def __init__(self, w, h):
        self.w, self.h = w, h
        self.color = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
        self.light = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
        self.c = ImageDraw.Draw(self.color)
        self.l = ImageDraw.Draw(self.light)

    def circle(self, x, y, r, col, light=False):
        d = self.l if light else self.c
        d.ellipse([(x - r) * SS, (y - r) * SS, (x + r) * SS, (y + r) * SS], fill=col)

    def ellipse(self, x, y, rx, ry, col, light=False):
        d = self.l if light else self.c
        d.ellipse([(x - rx) * SS, (y - ry) * SS, (x + rx) * SS, (y + ry) * SS], fill=col)

    def poly(self, pts, col, light=False):
        d = self.l if light else self.c
        d.polygon([(x * SS, y * SS) for x, y in pts], fill=col)

    def ring(self, x, y, r, width, col, light=False):
        d = self.l if light else self.c
        d.ellipse([(x - r) * SS, (y - r) * SS, (x + r) * SS, (y + r) * SS], outline=col, width=max(1, int(width * SS)))

    def arc(self, x, y, r, start, end, width, col, light=False):
        d = self.l if light else self.c
        d.arc([(x - r) * SS, (y - r) * SS, (x + r) * SS, (y + r) * SS], start, end, fill=col, width=max(1, int(width * SS)))

    def star(self, x, y, r, col, light=False):
        k = 0.22
        self.poly([(x, y - r), (x + r * k, y - r * k), (x + r, y), (x + r * k, y + r * k),
                   (x, y + r), (x - r * k, y + r * k), (x - r, y), (x - r * k, y - r * k)], col, light)

    def finish(self, glow=4):
        color = self.color.resize((self.w, self.h), Image.LANCZOS)
        light = self.light.resize((self.w, self.h), Image.LANCZOS)
        halo = light.filter(ImageFilter.GaussianBlur(glow))
        out = Image.alpha_composite(halo, light)
        return Image.alpha_composite(out, color)


# --------------------------------------------------------------------------------------
# 비행 (반복 8프레임, +x 방향)
# --------------------------------------------------------------------------------------
FRAMES = 8


def fire_flight(i):
    dark, base, core = ramp("Fire")
    w, h = 120, 64
    fx = Fx(w, h)
    t = i / FRAMES
    hx, hy = 86, 32
    # 꼬리 잔상 (불씨 방울 3개, 뒤로 갈수록 옅게)
    for k, (dx, r, a) in enumerate(((-26, 11, 150), (-44, 8, 100), (-60, 5, 60))):
        wob = math.sin(2 * math.pi * (t + k * 0.23)) * 4
        fx.circle(hx + dx, hy + wob, r + 3, alpha(dark, a), light=True)
        fx.circle(hx + dx, hy + wob, r, alpha(base, a))
    # 불꽃 몸통: 뒤로 늘어진 물방울 모양, 혀가 출렁인다
    pts = []
    for s in range(0, 33):
        a = math.pi * 2 * s / 32
        r = 17
        x = hx + math.cos(a) * r
        y = hy + math.sin(a) * r * 0.9
        if math.cos(a) < 0:  # 뒤쪽은 길게 늘이고 출렁이게
            stretch = -math.cos(a)
            x -= stretch * (22 + 6 * math.sin(2 * math.pi * (t * 2 + s / 8)))
            y += math.sin(a) * stretch * 3
        pts.append((x, y))
    fx.poly([(x + 2, y) for x, y in pts], alpha(dark, 255))
    inner = [(hx + (x - hx) * 0.72 + 3, hy + (y - hy) * 0.72) for x, y in pts]
    fx.poly(inner, base)
    fx.circle(hx + 4, hy, 8, core)
    fx.circle(hx + 6, hy - 2, 3.5, WHITE)
    fx.circle(hx, hy, 24, alpha(base, 170), light=True)
    # 반짝이 불씨
    rnd = random.Random(1)
    for k in range(5):
        phase = (t + k / 5) % 1
        x = hx - 10 - phase * 60
        y = hy + rnd.uniform(-14, 14) + math.sin(phase * 7 + k) * 3
        fx.circle(x, y, 2.5 * (1 - phase) + 0.8, alpha(core, 255 * (1 - phase)))
    return fx.finish(4)


def water_flight(i):
    dark, base, core = ramp("Water")
    w, h = 112, 48
    fx = Fx(w, h)
    t = i / FRAMES
    hx, hy = 84, 24
    # 창 몸통: 앞이 뾰족한 긴 물방울
    body = [(hx + 18, hy), (hx + 4, hy - 10), (hx - 30, hy - 7), (hx - 56, hy), (hx - 30, hy + 7), (hx + 4, hy + 10)]
    fx.poly([(x + 1, y) for x, y in body], dark)
    fx.poly([(hx + 14, hy), (hx + 2, hy - 7), (hx - 30, hy - 4.5), (hx - 50, hy), (hx - 30, hy + 4.5), (hx + 2, hy + 7)], base)
    fx.poly([(hx + 9, hy), (hx, hy - 3.5), (hx - 24, hy - 1.5), (hx - 24, hy + 1.5), (hx, hy + 3.5)], core)
    # 두 줄 물결 리본이 몸을 감고 돈다
    for sign in (1, -1):
        pts = []
        for s in range(30):
            x = hx + 8 - s * 2.2
            y = hy + sign * math.sin(2 * math.pi * (t + s / 15)) * 9
            pts.append((x, y))
        for (x0, y0), (x1, y1) in zip(pts, pts[1:]):
            fx.c.line([(x0 * SS, y0 * SS), (x1 * SS, y1 * SS)], fill=alpha(core, 220), width=2 * SS)
    # 거품 (뒤로 흘러감)
    for k in range(4):
        phase = (t + k / 4) % 1
        x = hx - 30 - phase * 40
        y = hy + (k % 2 * 2 - 1) * (6 + phase * 6)
        r = 3.5 * (1 - phase * 0.5)
        fx.ring(x, y, r, 1.2, alpha(core, 255 * (1 - phase)))
    fx.ellipse(hx - 10, hy, 34, 14, alpha(base, 140), light=True)
    return fx.finish(3)


def wind_flight(i):
    dark, base, core = ramp("Wind")
    w, h = 104, 72
    fx = Fx(w, h)
    t = i / FRAMES
    hx, hy = 70, 36
    # 초승달 칼날 두 개가 돈다
    for k in range(2):
        a0 = (t * 360 + k * 180) % 360
        fx.arc(hx, hy, 22, a0, a0 + 130, 7, alpha(dark, 230))
        fx.arc(hx, hy, 22, a0 + 10, a0 + 120, 4, base)
        fx.arc(hx, hy, 22, a0 + 30, a0 + 90, 1.8, core)
    fx.circle(hx, hy, 7, alpha(core, 230))
    fx.circle(hx, hy, 3, WHITE)
    # 뒤로 흐르는 바람 줄
    for k in range(3):
        phase = (t + k / 3) % 1
        y = hy - 14 + k * 14
        x0 = hx - 20 - phase * 30
        fx.c.line([(x0 * SS, y * SS), ((x0 - 26) * SS, (y + 2) * SS)], fill=alpha(base, 230 * (1 - phase)), width=3 * SS)
    fx.circle(hx, hy, 28, alpha(base, 120), light=True)
    return fx.finish(3)


def neutral_flight(i):
    dark, base, core = ramp("Neutral")
    w, h = 72, 72
    fx = Fx(w, h)
    t = i / FRAMES
    cx, cy = 40, 36
    pulse = 1 + 0.08 * math.sin(2 * math.pi * t)
    fx.circle(cx, cy, 15 * pulse, dark)
    fx.circle(cx, cy, 11 * pulse, base)
    fx.circle(cx + 1, cy - 1, 6, core)
    fx.circle(cx + 2, cy - 3, 2.5, WHITE)
    # 궤도를 도는 별 3개 (gold / aqua)
    for k in range(3):
        a = 2 * math.pi * (t + k / 3)
        x, y = cx + math.cos(a) * 22, cy + math.sin(a) * 9
        col = COLORS["gold"] if k == 0 else COLORS["aqua"]
        fx.star(x, y, 5 if k == 0 else 4, col)
    # 꼬리 잔상
    for k, (dx, r, a) in enumerate(((-14, 10, 110), (-24, 7, 70))):
        fx.circle(cx + dx, cy, r, alpha(base, a), light=True)
    fx.circle(cx, cy, 22, alpha(base, 160), light=True)
    return fx.finish(4)


# --------------------------------------------------------------------------------------
# 적중 (한 번, 8프레임, 140x140 중앙)
# --------------------------------------------------------------------------------------

def ease_out(p):
    return 1 - (1 - p) ** 2


def hit(folder, i, seed):
    dark, base, core = ramp(folder)
    w = h = 140
    fx = Fx(w, h)
    p = i / (FRAMES - 1)
    cx = cy = 70
    rnd = random.Random(seed)

    # 처음 2프레임: 하얀 섬광
    if i <= 2:
        r = [26, 20, 12][i]
        fx.circle(cx, cy, r + 8, alpha(base, 220), light=True)
        fx.circle(cx, cy, r, core)
        fx.circle(cx, cy, r * 0.6, WHITE)
        fx.star(cx, cy, r * 1.9, alpha(WHITE, 240))

    # 퍼지는 고리
    ring_r = 14 + ease_out(p) * 50
    fx.ring(cx, cy, ring_r, 6 * (1 - p) + 1, alpha(base, 255 * (1 - p)))
    fx.ring(cx, cy, ring_r, 3 * (1 - p) + 1, alpha(base, 200 * (1 - p)), light=True)

    # 속성별 파편
    count = 10
    for k in range(count):
        a = 2 * math.pi * (k + rnd.uniform(-0.3, 0.3)) / count
        speed = rnd.uniform(0.75, 1.1)
        d = 10 + ease_out(p) * 48 * speed
        x, y = cx + math.cos(a) * d, cy + math.sin(a) * d
        fade = 1 - p
        if folder == "Fire":
            y -= p * 12  # 불씨는 위로 떠오름
            r = 5 * fade + 1
            fx.circle(x, y, r + 1.5, alpha(dark, 255 * fade))
            fx.circle(x, y, r, alpha(core, 255 * fade))
        elif folder == "Water":
            y += p * p * 20  # 물방울은 떨어짐
            r = 4.5 * fade + 1
            tail = (x - math.cos(a) * r * 2.2, y - math.sin(a) * r * 2.2)
            fx.poly([tail, (x + math.cos(a + 1.6) * r, y + math.sin(a + 1.6) * r),
                     (x + math.cos(a) * r, y + math.sin(a) * r), (x + math.cos(a - 1.6) * r, y + math.sin(a - 1.6) * r)],
                    alpha(base, 255 * fade))
            fx.circle(x, y, r * 0.5, alpha(core, 255 * fade))
        elif folder == "Wind":
            a0 = math.degrees(a) + p * 200
            fx.arc(x, y, 8, a0, a0 + 120, 3 * fade + 1, alpha(base, 255 * fade))
            fx.arc(x, y, 8, a0 + 20, a0 + 90, 1.5, alpha(core, 255 * fade))
        elif folder == "Earth":
            y += p * p * 26  # 돌 조각은 떨어짐
            s = 6 * (1 - p * 0.5)
            rot = a + p * 4
            pts = [(x + math.cos(rot + j * 2.1 + rnd.uniform(-0.3, 0.3)) * s, y + math.sin(rot + j * 2.1) * s) for j in range(3)]
            fx.poly([(px + (px - x) * 0.35, py + (py - y) * 0.35) for px, py in pts], alpha(OUTLINE, 255 * fade))
            fx.poly(pts, alpha(base, 255 * fade))
            fx.circle(x - s * 0.2, y - s * 0.3, s * 0.3, alpha(core, 255 * fade))
        else:
            col = COLORS["gold"] if k % 2 else core
            fx.star(x, y, 7 * fade + 2, alpha(col, 255 * fade))

    # 땅: 흙먼지 구름
    if folder == "Earth":
        for k in range(5):
            x = cx - 40 + k * 20
            r = 8 + ease_out(p) * 10
            fx.circle(x, cy + 24 - p * 6, r, alpha(hexrgba("d8c0a0"), 170 * (1 - p)))

    # 반짝임 별 (화려하게): 중간 프레임에 2~3개
    if 2 <= i <= 6:
        for k in range(3):
            a = rnd.uniform(0, 2 * math.pi)
            d = rnd.uniform(20, 46)
            r = [3, 6, 8, 6, 3][i - 2] * rnd.uniform(0.7, 1.1)
            fx.star(cx + math.cos(a) * d, cy + math.sin(a) * d, r, COLORS["cream"])
            fx.star(cx + math.cos(a) * d, cy + math.sin(a) * d, r + 2, alpha(COLORS["aqua"], 200), light=True)
    return fx.finish(4)


# --------------------------------------------------------------------------------------
# 발사 (손 앞에 빛이 모였다 터짐, 6프레임, 96x96)
# --------------------------------------------------------------------------------------
CAST_FRAMES = 6


def cast(folder, i):
    dark, base, core = ramp(folder)
    fx = Fx(96, 96)
    cx = cy = 48
    rnd = random.Random(len(folder))
    if i < 4:
        p = i / 3  # 모이기
        for k in range(8):
            a = 2 * math.pi * k / 8 + rnd.uniform(-0.2, 0.2)
            d = 38 * (1 - p) + 6
            x, y = cx + math.cos(a) * d, cy + math.sin(a) * d
            fx.circle(x, y, 2.5 + p * 1.5, core)
            tx, ty = cx + math.cos(a) * (d + 10), cy + math.sin(a) * (d + 10)
            fx.c.line([(x * SS, y * SS), (tx * SS, ty * SS)], fill=alpha(base, 200), width=2 * SS)
        fx.ring(cx, cy, 30 * (1 - p) + 8, 2.5, alpha(base, 160 + 90 * p))
        fx.circle(cx, cy, 5 + p * 8, base)
        fx.circle(cx, cy, 3 + p * 5, core)
        fx.circle(cx, cy, 12 + p * 10, alpha(base, 150), light=True)
    else:
        p = (i - 4) / 1  # 터짐
        r = 14 + p * 14
        fx.circle(cx, cy, r + 4 - p * 10, alpha(base, 200 * (1 - p * 0.85)), light=True)
        fx.circle(cx, cy, r * (1 - p * 0.5), alpha(core, 255 * (1 - p * 0.5)))
        fx.circle(cx, cy, r * 0.5 * (1 - p * 0.6), WHITE)
        fx.star(cx, cy, 30 - p * 8, alpha(WHITE, 255 * (1 - p * 0.4)))
        fx.ring(cx, cy, r + 10, 3 * (1 - p) + 1, alpha(base, 255 * (1 - p)))
    return fx.finish(3)


FLIGHTS = {"Fire": fire_flight, "Water": water_flight, "Wind": wind_flight, "Neutral": neutral_flight}


def save(folder, frames):
    os.makedirs(folder, exist_ok=True)
    for i, img in enumerate(frames):
        img.save(os.path.join(folder, f"{i:02d}.png"))


def generate():
    result = {}
    for name in FOLDERS:
        casts = [cast(name, i) for i in range(CAST_FRAMES)]
        hits = [hit(name, i, seed=len(name) * 31) for i in range(FRAMES)]
        save(os.path.join(BASE, "Cast", name), casts)
        save(os.path.join(BASE, "Hit", name), hits)
        result[f"Cast/{name}"] = casts
        result[f"Hit/{name}"] = hits
        if name in FLIGHTS:
            flights = [FLIGHTS[name](i) for i in range(FRAMES)]
            save(os.path.join(BASE, "Flight", name), flights)
            result[f"Flight/{name}"] = flights
    return result


def preview(result, path):
    rows = list(result.items())
    cell = 144
    image = Image.new("RGBA", (cell * 8 + 110, cell * len(rows)), COLORS["night"])
    draw = ImageDraw.Draw(image)
    for r, (name, frames) in enumerate(rows):
        draw.text((4, r * cell + 4), name, fill=(255, 255, 255, 255))
        for c, frame in enumerate(frames):
            image.alpha_composite(frame, (110 + c * cell + (cell - frame.width) // 2, r * cell + (cell - frame.height) // 2))
    image.save(path)


if __name__ == "__main__":
    out = generate()
    if len(sys.argv) > 1:
        preview(out, sys.argv[1])
    print("done")
