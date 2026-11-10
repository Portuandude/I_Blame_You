"""I Blame You! 시각 효과 묶음 생성기 (Docs/ArtStyle.md: 귀엽지만 화려하게).

generate_magic_fx.py 의 Fx(색 층 + 흐린 빛 층)로 그린다. 모두 런타임 로드(EffectLibrary.LoadRuntimeFrames, 중앙 피벗, 100px = 1유닛).

  Assets/Resources/Effects/Dust/00~05      착지 먼지 (96x40, 아래 가운데가 발밑)
  Assets/Resources/Effects/LevelUp/00~09   레벨업 빛기둥 (120x240, 아래쪽 1/4 지점이 발밑)
  Assets/Resources/Effects/DeathPop/00~07  적 사망 별·하트 파편 (128x128)
  Assets/Resources/Effects/Sparkle/00~05   유물 상자 반짝임 반복 (96x96)
  (제거됨 2026-11-10, 예전 그림은 Trash/) CheckpointPillar/00~09  체크포인트 켤 때 aqua 빛기둥 + 큰 반짝임 3개 (120x240, 아래쪽 1/4 지점이 수정 발밑)
  Assets/Resources/Effects/ClearBurst/00~09        전투 구역 돌파 축하: 별·하트가 크게 터짐 (192x192)
  Assets/Resources/Effects/ChestOpen/00~11         유물 상자 열기: 뚜껑이 튀어 올라 별로 터지고 gold 빛기둥 + 별 (120x240,
                                                   상자 그림(72x64) 가운데가 캔버스 가운데보다 48px 아래 — VfxPlayer.ChestOpen)
  Assets/Resources/Effects/Shockwave/00~07         무거운 착지 충격파: 납작한 고리 두 겹 + 흙먼지 + 별 (192x64, 아래에서 16px이 바닥)
  Assets/Resources/Effects/HeartOrb/00~07          회복 구슬 반복: 통통한 berry 하트 + aqua 마나 고리 (80x80)
  Assets/Resources/Effects/HeartPop/00~07          회복 구슬 주울 때: 하트·별이 위로 톡톡 튐 (128x128)
  Assets/Resources/Effects/ShieldShatter/00~07     방어막 깨짐: 거품이 유리 조각 10개로 흩어지고 aqua 반짝임 (192x192, 방어막 그림과 같은 중심)
  Assets/Resources/Effects/RelicBanner/00          유물 이름 띠 (360x80, 가운데에 글자가 올라간다)

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


WOOD, WOOD_LIGHT, WOOD_LINE, BAND = (138, 90, 50, 255), (156, 106, 60, 255), (110, 69, 38, 255), (232, 182, 74, 255)
OUTLINE = (24, 20, 26, 255)


def chest_open_frames():
    """유물 상자 열기 (generate_sprites.relic_chest_frames 와 같은 상자): 살짝 웅크렸다가 뚜껑이 빙글 돌며 튀어 올라 별로 터지고,
    열린 상자에서 gold 빛기둥과 별이 솟는다. 마지막엔 상자째 옅어진다 (게임에서는 상자 오브젝트가 이미 사라졌다). 12프레임."""
    frames = []
    w, h = 120, 240
    ox, oy = w / 2 - 36, h / 2 + 48 - 32   # 상자 그림 좌표(72x64) → 캔버스
    pillar = levelup_frames(GOLD)
    rng = random.Random(41)
    bits = [(rng.uniform(-2.6, -0.5), rng.uniform(40, 80), (GOLD, CREAM, BERRY, AQUA)[k % 4], k % 3 == 1) for k in range(10)]
    lid_path = [(8 + 56 * u, 28 - 18 * math.sin(math.pi * u)) for u in [k / 16 for k in range(17)]]

    def P(x, y):
        return ox + x, oy + y

    for i in range(12):
        t = i / 11
        fx = Fx(w, h)
        squash = 0.9 if i == 0 else 1.0
        # 몸통 (발밑 기준 세로 squash)
        top = 60 - (60 - 30) * squash
        fx.poly([P(8, top), P(64, top), P(64, 60), P(8, 60)], OUTLINE)
        fx.poly([P(11, top + 3), P(61, top + 3), P(61, 57), P(11, 57)], WOOD)
        for y in (40, 48):
            fx.poly([P(11, y), P(61, y), P(61, y + 1), P(11, y + 1)], WOOD_LINE)
        for x in (14, 54):
            fx.poly([P(x, top), P(x + 4, top), P(x + 4, 57), P(x, 57)], BAND)
        fx.poly([P(11, 55), P(61, 55), P(61, 58), P(11, 58)], BAND)
        fx.circle(*P(36, 37), 7, OUTLINE)
        fx.circle(*P(36, 37), 5, (242, 193, 78, 255))
        fx.circle(*P(36, 37), 3, CREAM)
        # 열린 입구에서 빛 (뚜껑이 뜬 뒤)
        if i >= 1:
            glow = 1.0 if t < 0.6 else 1 - (t - 0.6) / 0.4
            fx.ellipse(*P(36, top + 1), 26, 6, alpha(CREAM, 255 * glow))
            fx.ellipse(*P(36, top - 2), 32, 12, alpha(GOLD, 230 * glow), light=True)
        # 뚜껑: 0은 닫힘(틈으로 빛), 1~4 빙글 돌며 위로, 5에서 별로 터짐
        if i <= 4:
            dy = [0, -16, -34, -48, -56][i]
            rot = math.radians([0, -14, -40, -75, -115][i])
            cx, cy = 36, 22
            def L(x, y):
                x, y = x - cx, y - cy
                return P(cx + x * math.cos(rot) - y * math.sin(rot), cy + dy + x * math.sin(rot) + y * math.cos(rot))
            fx.poly([L(x, y) for x, y in lid_path], OUTLINE)
            fx.poly([L(11 + (x - 8) * 50 / 56, 28 - (28 - y) * 0.85) for x, y in lid_path], WOOD_LIGHT)
            for x in (14, 54):
                fx.poly([L(x, 15 if x == 14 else 15), L(x + 4, 13), L(x + 4, 28), L(x, 28)], BAND)
            if i == 0:
                fx.poly([P(11, 27), P(61, 27), P(61, 31), P(11, 31)], alpha(CREAM, 240))
        elif i <= 6:
            q = (i - 5) / 1
            sx, sy = P(36 + 10, 22 - 62)
            fx.star(sx, sy, 16 * (1 - q * 0.5), alpha(CREAM, 255 * (1 - q * 0.6)))
            fx.circle(sx, sy, 14 * (1 - q * 0.4), alpha(GOLD, 200 * (1 - q)), light=True)
            fx.ring(sx, sy, 10 + 12 * q, 3 * (1 - q) + 1, alpha(GOLD, 255 * (1 - q)))
        # 열린 입구에서 솟는 별·하트
        if i >= 1:
            for ang, dist, col, is_heart in bits:
                q = min(1.0, (t - 0.08) / 0.7)
                d = dist * (1 - (1 - q) ** 2)
                x, y = P(36 + math.cos(ang) * d * 0.7, 30 + math.sin(ang) * d)
                a = 255 * (1 - max(0.0, q - 0.6) / 0.4)
                size = 6 * (1 - 0.3 * q)
                if is_heart:
                    heart(fx, x, y, size + 1, alpha(col, a))
                else:
                    fx.star(x, y, size + 4, alpha(col, a * 0.5), light=True)
                    fx.star(x, y, size + 1, alpha(col, a))
        img = fx.finish(glow=4)
        # 빛기둥은 상자 입구(캔버스 아래쪽 1/4 지점 근처)에서 솟도록 레벨업 기둥을 뒤에 깐다
        if i >= 1:
            beam = pillar[min(i - 1, len(pillar) - 1)].copy()
            back = Image.new("RGBA", (w, h), (0, 0, 0, 0))
            back.alpha_composite(beam, (0, int(oy + 30 - h * 0.75)) if oy + 30 - h * 0.75 >= 0 else (0, 0))
            img = Image.alpha_composite(back, img)
        frames.append(faded(img, 1.0 if t < 0.7 else 1 - (t - 0.7) / 0.35))
    return frames


def relic_banner_frames():
    """유물 이름 띠: cream 띠 + gold 테두리 + 양끝 berry 꼬리, 가운데 위에 별. 글자는 게임에서 TextMesh로 올린다."""
    w, h = 360, 80
    fx = Fx(w, h)
    cy = 44
    for side in (-1, 1):  # 접힌 꼬리
        x0 = w / 2 + side * 150
        fx.poly([(x0, cy - 16), (x0 + side * 28, cy - 16), (x0 + side * 18, cy), (x0 + side * 28, cy + 16), (x0, cy + 16)],
                alpha(BERRY, 255))
        fx.poly([(x0, cy + 16), (x0 - side * 10, cy + 22), (x0 - side * 10, cy + 12)], (200, 60, 120, 255))
    fx.poly([(w / 2 - 160, cy - 20), (w / 2 + 160, cy - 20), (w / 2 + 160, cy + 20), (w / 2 - 160, cy + 20)], GOLD)
    fx.poly([(w / 2 - 156, cy - 16), (w / 2 + 156, cy - 16), (w / 2 + 156, cy + 16), (w / 2 - 156, cy + 16)], CREAM)
    fx.poly([(w / 2 - 160, cy - 24), (w / 2 + 160, cy - 24), (w / 2 + 160, cy + 24), (w / 2 - 160, cy + 24)],
            alpha(GOLD, 150), light=True)
    fx.star(w / 2, cy - 24, 13, GOLD)
    fx.star(w / 2, cy - 24, 7, CREAM)
    for x in (w / 2 - 120, w / 2 + 120):
        fx.star(x, cy - 22, 6, alpha(CREAM, 255))
    return [fx.finish(glow=3)]


def shockwave_frames():
    """중간보스 착지 충격파: 바닥을 따라 납작한 gold·cream 고리 두 겹이 퍼지고, 양옆으로 흙먼지 덩이와 별이 튄다. 8프레임."""
    frames = []
    w, h = 192, 64
    cx, floor = w / 2, h - 16
    for i in range(8):
        t = i / 7
        fx = Fx(w, h)
        q = 1 - (1 - t) ** 2
        fx.ellipse(cx, floor, 20 + 72 * q, 5 + 8 * q, alpha(GOLD, 200 * (1 - t)), light=True)
        fx.c.ellipse([(cx - 16 - 72 * q) * 4, (floor - 4 - 7 * q) * 4, (cx + 16 + 72 * q) * 4, (floor + 4 + 7 * q) * 4],
                     outline=alpha(CREAM, 255 * (1 - t)), width=max(1, int((6 * (1 - t) + 1) * 4)))
        fx.c.ellipse([(cx - 8 - 44 * q) * 4, (floor - 2 - 4 * q) * 4, (cx + 8 + 44 * q) * 4, (floor + 2 + 4 * q) * 4],
                     outline=alpha(GOLD, 255 * (1 - t)), width=max(1, int((4 * (1 - t) + 1) * 4)))
        for side in (-1, 1):
            for k, (dx, r) in enumerate(((18, 8), (34, 6.5), (50, 5))):
                x = cx + side * (dx + 26 * q * (1 + k * 0.2))
                y = floor - 4 - 18 * math.sin(math.pi * min(1.0, t * 1.2)) * (1 - k * 0.25)
                rr = r * (1 - 0.5 * t)
                fx.circle(x, y, rr + 1.5, alpha(SAND, 230 * (1 - t)))
                fx.circle(x - rr * 0.3, y - rr * 0.3, rr * 0.55, alpha(CREAM, 200 * (1 - t)))
            sx = cx + side * (26 + 52 * q)
            sy = floor - 14 - 22 * math.sin(math.pi * t)
            fx.star(sx, sy, 7 * (1 - 0.4 * t), alpha(GOLD if side < 0 else AQUA, 255 * (1 - t * 0.8)))
        frames.append(fx.finish(glow=3))
    return frames


BERRY_DARK = (200, 60, 120, 255)


def chubby_heart(fx, x, y, r, fill, rim=None, light=False):
    """통통한 하트: 원 두 개를 크게 겹치고 아래 꼭짓점을 둥글게."""
    if rim is not None:
        chubby_heart(fx, x, y, r + 2.2, rim, light=light)
    fx.circle(x - r * 0.48, y - r * 0.18, r * 0.62, fill, light)
    fx.circle(x + r * 0.48, y - r * 0.18, r * 0.62, fill, light)
    fx.poly([(x - r * 1.08, y - r * 0.02), (x + r * 1.08, y - r * 0.02), (x + r * 0.25, y + r * 0.82),
             (x, y + r * 0.95), (x - r * 0.25, y + r * 0.82)], fill, light)


def heart_orb_frames():
    """회복 구슬: 통통한 berry 하트가 두근거리고(크기 ±6%), aqua 마나 고리를 반짝이 3개가 돈다. 8프레임 반복."""
    frames = []
    w = h = 80
    cx, cy = w / 2, h / 2
    for i in range(8):
        t = i / 8
        beat = 1 + 0.06 * math.sin(2 * math.pi * t * 2)  # 한 바퀴에 두 번 두근
        fx = Fx(w, h)
        fx.ring(cx, cy, 24, 8, alpha(BERRY, 70 + 30 * math.sin(2 * math.pi * t)), light=True)
        # 마나 고리 뒤쪽 절반 (하트 뒤)
        fx.arc(cx, cy + 2, 27, 180, 360, 2.5, alpha(AQUA, 200))
        r = 15 * beat
        chubby_heart(fx, cx, cy - 1, r, BERRY, rim=BERRY_DARK)
        fx.circle(cx - r * 0.5, cy - r * 0.45, r * 0.26, alpha(CREAM, 240))
        fx.circle(cx - r * 0.18, cy - r * 0.62, r * 0.1, CREAM)
        fx.ellipse(cx + r * 0.4, cy + r * 0.3, r * 0.22, r * 0.12, alpha(CREAM, 90))  # 볼 같은 반사
        # 마나 고리 앞쪽 절반 (하트 앞)
        fx.arc(cx, cy + 2, 27, 0, 180, 2.5, alpha(AQUA, 230))
        for k in range(3):
            a = 2 * math.pi * (t + k / 3)
            x, y = cx + math.cos(a) * 27, cy + 2 + math.sin(a) * 27
            fx.circle(x, y, 4, alpha(AQUA, 180), light=True)
            fx.star(x, y, 5.5, CREAM)
        frames.append(fx.finish(glow=3))
    return frames


def heart_pop_frames():
    """회복 구슬 주울 때: 가운데 번쩍 + 고리, 하트 4개·aqua 별 4개가 위쪽으로 톡톡 튀었다 사라진다. 8프레임."""
    frames = []
    w = h = 128
    cx, cy = w / 2, h / 2 + 10
    bits = [(-0.5 * math.pi + d, k) for k, d in enumerate((-1.1, -0.6, -0.2, 0.2, 0.6, 1.1, -1.5, 1.5))]
    for i in range(8):
        t = i / 7
        fx = Fx(w, h)
        if t < 0.45:
            q = t / 0.45
            if q < 0.35:
                fx.circle(cx, cy, 16 * (1 - q), alpha(CREAM, 230 * (1 - q / 0.35)))
            fx.ring(cx, cy, 10 + 30 * q, 4 * (1 - q) + 1, alpha(BERRY, 240 * (1 - q)))
        for ang, k in bits:
            dist = (34 + 8 * (k % 3)) * (1 - (1 - t) ** 2)
            x = cx + math.cos(ang) * dist
            y = cy + math.sin(ang) * dist + 14 * t * t
            a = 255 * (1 - max(0.0, t - 0.55) / 0.45)
            if k % 2 == 0:
                chubby_heart(fx, x, y, 6 * (1 - 0.3 * t), alpha(BERRY, a), rim=alpha(BERRY_DARK, a))
            else:
                fx.star(x, y, 9 * (1 - 0.3 * t), alpha(AQUA, a * 0.6), light=True)
                fx.star(x, y, 6 * (1 - 0.3 * t), alpha(CREAM, a))
        frames.append(fx.finish(glow=3))
    return frames


def shield_shatter_frames():
    """방어막 깨짐: 테두리(반지름 62px, generate_skill_fx 방어막과 같음)에서 유리 조각 10개가 돌며 바깥으로 튀고,
    깨진 테두리 고리가 번쩍 퍼지며 aqua 별이 반짝인다. 흰·aqua만 (색을 입히지 않고 그대로 쓴다). 8프레임."""
    frames = []
    w = h = 192
    cx, cy = w / 2, h / 2
    rng = random.Random(77)
    shards = []
    for k in range(10):
        ang = 2 * math.pi * k / 10 + rng.uniform(-0.2, 0.2)
        shards.append((ang, rng.uniform(26, 40), rng.uniform(10, 15), rng.uniform(-2.5, 2.5), rng.uniform(0, math.pi)))
    for i in range(8):
        t = i / 7
        fx = Fx(w, h)
        if t < 0.4:
            q = t / 0.4
            fx.ring(cx, cy, 62 + 14 * q, 6 * (1 - q) + 1, alpha(CREAM, 255 * (1 - q)))
        for ang, speed, size, spin, rot0 in shards:
            d = 60 + speed * (1 - (1 - t) ** 2) * 1.2
            x = cx + math.cos(ang) * d
            y = cy + math.sin(ang) * d + 18 * t * t  # 살짝 떨어진다
            rot = rot0 + spin * t * 2
            sz = size * (1 - 0.35 * t)
            pts = [(x + math.cos(rot + a) * sz * r, y + math.sin(rot + a) * sz * r)
                   for a, r in ((0, 1.0), (2.3, 0.7), (4.0, 0.85))]
            a = 255 * (1 - max(0.0, t - 0.5) / 0.5)
            fx.poly(pts, alpha(AQUA, a * 0.6), light=True)
            fx.poly(pts, alpha(CREAM, a))
            fx.poly([pts[0], pts[1], ((pts[0][0] + pts[2][0]) / 2, (pts[0][1] + pts[2][1]) / 2)], alpha((255, 255, 255, 255), a))
        for k in range(4):
            ang = 2 * math.pi * (k / 4 + 0.125)
            d = 50 + 30 * t
            tw = math.sin(math.pi * min(1.0, t * 1.4))
            fx.star(cx + math.cos(ang) * d, cy + math.sin(ang) * d, 9 * tw + 1, alpha(AQUA, 255 * tw))
        frames.append(fx.finish(glow=3))
    return frames


SETS = {"Dust": dust_frames, "LevelUp": levelup_frames, "DeathPop": deathpop_frames, "Sparkle": sparkle_frames,
        "ClearBurst": clearburst_frames,
        "ChestOpen": chest_open_frames, "RelicBanner": relic_banner_frames, "Shockwave": shockwave_frames,
        "HeartOrb": heart_orb_frames, "HeartPop": heart_pop_frames,
        "ShieldShatter": shield_shatter_frames}


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
