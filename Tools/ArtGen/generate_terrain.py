"""I Blame You! 지형 타일·장식 생성기 (Docs/ArtStyle.md 팔레트).

땅 블록은 런타임에 흙색 사각형으로 채우고, 그 위에 이 그림들을 겹친다 (ChunkBuilder):
  TerrainGrass/00~03  윗면 풀 띠: 가운데, 왼쪽 끝, 오른쪽 끝, 양쪽 끝 (140x140, 타일 위 40px까지 풀잎)
  TerrainSide/00~01   옆면 외곽선·그림자: 왼쪽, 오른쪽 (100x100)
  TerrainUnder/00     아랫면 외곽선·그림자·뿌리 (100x100)
  TerrainPebble/00~02 흙 속 돌멩이·뿌리 무늬 (100x100)
  TerrainDecor/00~05  윗면 장식: 분홍 꽃, 노란 꽃, 버섯, 풀 다발, 돌, 빛나는 수정 (하단이 땅에 닿게 그림)
  Platform/00, Crumble/00  나무 발판, 무너지는 발판 (generate_sprites.py에서 옮겨 옴)
모든 그림은 Assets/Resources/Stage/<이름>/NN.png (100px = 1유닛, 중앙 피벗으로 읽힌다).

    python3 Tools/ArtGen/generate_terrain.py [미리보기.png]
"""
import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(__file__))
from palette import COLORS, OUTLINE, element  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BASE = os.path.join(ROOT, "Assets", "Resources", "Stage")
SS = 4


def mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3)) + (a[3],)


def canvas(w, h):
    image = Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))
    return image, ImageDraw.Draw(image)


def done(image, w, h):
    return image.resize((w, h), Image.LANCZOS)


def S(*values):
    return [v * SS for v in values]


# ---- 윗면 풀 띠 ----
# 140x140 캔버스: 가로 20~120이 타일, 세로 40이 타일 윗면. 띠는 윗면에서 아래로 34px, 풀잎은 위로 최대 14px.
GRASS_W, GRASS_H, MARGIN, TOP = 140, 140, 20, 40


def grass(left_end, right_end, seed):
    image, d = canvas(GRASS_W, GRASS_H)
    rnd = random.Random(seed)
    leaf, light, dark = COLORS["leaf"], COLORS["leaf_light"], COLORS["leaf_dark"]
    x0 = MARGIN + 4 if left_end else 0
    x1 = GRASS_W - MARGIN - 4 if right_end else GRASS_W
    band = 30

    # 아래 가장자리: 둥글게 출렁이는 물결 (가운데 타일끼리 이어지도록 140 주기... 대신 타일 폭 100 주기)
    def bottom(x):
        return TOP + band - 6 + 5 * math.sin(2 * math.pi * (x - MARGIN) / 50)

    # 외곽선 층 → 몸 → 밝은 윗면 순서로 그린다
    for layer, grow, color in ((0, 3, OUTLINE), (1, 0, dark), (2, -5, leaf)):
        pts = []
        for x in range(int(x0), int(x1) + 1, 2):
            pts.append((x, bottom(x) + grow))
        top_y = TOP - grow
        poly = [(x0, top_y)] + [(x1, top_y)] + list(reversed(pts))
        if left_end or right_end:
            pass
        d.polygon([(px * SS, py * SS) for px, py in poly], fill=color)
        # 끝은 둥근 반원으로
        if left_end:
            r = (bottom(x0) + grow - top_y) / 2
            d.ellipse(S(x0 - r, top_y, x0 + r, top_y + 2 * r), fill=color)
        if right_end:
            r = (bottom(x1) + grow - top_y) / 2
            d.ellipse(S(x1 - r, top_y, x1 + r, top_y + 2 * r), fill=color)
    # 윗면 하이라이트 띠
    hx0 = x0 + (6 if left_end else 0)
    hx1 = x1 - (6 if right_end else 0)
    d.rounded_rectangle(S(hx0, TOP + 1, hx1, TOP + 8), radius=3 * SS, fill=light)

    # 풀잎 (위로 솟은 뾰족한 잎, 외곽선 포함)
    blades = []
    x = x0 + 4
    while x < x1 - 4:
        blades.append((x, rnd.uniform(6, 14), rnd.uniform(-3, 3)))
        x += rnd.uniform(7, 12)
    for bx, height, lean in blades:
        tip = (bx + lean, TOP - height)
        d.polygon([(px * SS, py * SS) for px, py in [(bx - 4.5, TOP + 3), tip, (bx + 4.5, TOP + 3)]], fill=OUTLINE)
    for bx, height, lean in blades:
        tip = (bx + lean, TOP - height + 2.5)
        d.polygon([(px * SS, py * SS) for px, py in [(bx - 2.5, TOP + 4), tip, (bx + 2.5, TOP + 4)]], fill=light)
    d.rounded_rectangle(S(hx0, TOP + 1, hx1, TOP + 8), radius=3 * SS, fill=light)

    # 띠 안의 작은 반짝임 점 (화려하게)
    for _ in range(3):
        px = rnd.uniform(x0 + 8, x1 - 8)
        py = rnd.uniform(TOP + 10, TOP + 20)
        d.ellipse(S(px - 1.5, py - 1.5, px + 1.5, py + 1.5), fill=mix(light, COLORS["cream"], 0.5))
    return done(image, GRASS_W, GRASS_H)


def side(left):
    image, d = canvas(100, 100)
    soil, dark = COLORS["soil"], COLORS["soil_dark"]
    x = 0 if left else 100
    sign = 1 if left else -1
    d.rectangle(S(min(x, x + sign * 14), 0, max(x, x + sign * 14), 100), fill=mix(soil, dark, 0.55))
    d.rectangle(S(min(x, x + sign * 4), 0, max(x, x + sign * 4), 100), fill=OUTLINE)
    # 오른쪽 아래 림 라이트는 오른쪽 면에만 (빛은 왼쪽 위에서)
    if not left:
        d.rectangle(S(100 - 6, 0, 100 - 4, 100), fill=mix(COLORS["sand"], soil, 0.3))
    return done(image, 100, 100)


def under():
    image, d = canvas(100, 100)
    soil, dark = COLORS["soil"], COLORS["soil_dark"]
    d.rectangle(S(0, 84, 100, 100), fill=mix(soil, dark, 0.6))
    d.rectangle(S(0, 96, 100, 100), fill=OUTLINE)
    rnd = random.Random(3)
    for _ in range(3):
        x = rnd.uniform(10, 90)
        pts = [(x, 84)]
        for k in range(3):
            x += rnd.uniform(-4, 4)
            pts.append((x, 86 + k * 4))
        d.line([(px * SS, py * SS) for px, py in pts], fill=mix(COLORS["sand"], dark, 0.4), width=2 * SS)
    return done(image, 100, 100)


def pebble(seed):
    image, d = canvas(100, 100)
    rnd = random.Random(seed)
    soil = COLORS["soil"]
    for _ in range(rnd.randint(2, 3)):
        x, y = rnd.uniform(20, 80), rnd.uniform(20, 80)
        rx, ry = rnd.uniform(5, 9), rnd.uniform(4, 6)
        d.ellipse(S(x - rx - 1.5, y - ry - 1.5, x + rx + 1.5, y + ry + 1.5), fill=mix(soil, COLORS["soil_dark"], 0.7))
        d.ellipse(S(x - rx, y - ry, x + rx, y + ry), fill=mix(COLORS["sand"], soil, 0.45))
        d.ellipse(S(x - rx * 0.6, y - ry * 0.7, x, y - ry * 0.1), fill=mix(COLORS["sand"], COLORS["cream"], 0.3))
    if seed % 2 == 0:
        # 작은 뿌리 무늬
        x = rnd.uniform(15, 85)
        pts = [(x, 10)]
        for k in range(4):
            x += rnd.uniform(-6, 6)
            pts.append((x, 10 + (k + 1) * 10))
        d.line([(px * SS, py * SS) for px, py in pts], fill=mix(COLORS["soil_dark"], soil, 0.3), width=2 * SS)
    return done(image, 100, 100)


# ---- 장식 (하단이 땅) ----
def flower(color, w=40, h=56):
    image, d = canvas(w, h)
    cx = w / 2
    stem_top = 22
    d.line(S(cx, h, cx, stem_top), fill=OUTLINE, width=5 * SS)
    d.line(S(cx, h, cx, stem_top), fill=COLORS["leaf_dark"], width=2 * SS)
    d.ellipse(S(cx, h - 18, cx + 13, h - 10), fill=OUTLINE)
    d.ellipse(S(cx + 1.5, h - 16.5, cx + 11.5, h - 11.5), fill=COLORS["leaf"])
    for k in range(5):
        a = k * 2 * math.pi / 5 - math.pi / 2
        px, py = cx + math.cos(a) * 8, stem_top - 4 + math.sin(a) * 8
        d.ellipse(S(px - 7.5, py - 7.5, px + 7.5, py + 7.5), fill=OUTLINE)
    for k in range(5):
        a = k * 2 * math.pi / 5 - math.pi / 2
        px, py = cx + math.cos(a) * 8, stem_top - 4 + math.sin(a) * 8
        d.ellipse(S(px - 5.5, py - 5.5, px + 5.5, py + 5.5), fill=color)
        d.ellipse(S(px - 3, py - 3.5, px, py - 0.5), fill=mix(color, COLORS["cream"], 0.6))
    d.ellipse(S(cx - 4.5, stem_top - 8.5, cx + 4.5, stem_top + 0.5), fill=OUTLINE)
    d.ellipse(S(cx - 3, stem_top - 7, cx + 3, stem_top - 1), fill=COLORS["gold"] if color != COLORS["gold"] else COLORS["cream"])
    return done(image, w, h)


def mushroom(w=44, h=46):
    image, d = canvas(w, h)
    cx = w / 2
    d.rounded_rectangle(S(cx - 8, 20, cx + 8, h), radius=5 * SS, fill=OUTLINE)
    d.rounded_rectangle(S(cx - 5.5, 22, cx + 5.5, h - 2.5), radius=4 * SS, fill=COLORS["cream"])
    d.pieslice(S(cx - 21, 3, cx + 21, 45), 180, 360, fill=OUTLINE)
    d.pieslice(S(cx - 18, 6, cx + 18, 42), 180, 360, fill=COLORS["berry"])
    for px, py, r in ((cx - 9, 17, 3.5), (cx + 4, 13, 4.5), (cx + 12, 20, 2.5)):
        d.ellipse(S(px - r, py - r, px + r, py + r), fill=COLORS["cream"])
    # 눈 (귀엽게)
    for px in (cx - 3, cx + 3):
        d.ellipse(S(px - 1.3, 30, px + 1.3, 34), fill=OUTLINE)
    return done(image, w, h)


def tuft(w=48, h=30):
    image, d = canvas(w, h)
    rnd = random.Random(9)
    blades = [(w / 2 + rnd.uniform(-16, 16), rnd.uniform(14, 26), rnd.uniform(-6, 6)) for _ in range(7)]
    for bx, height, lean in blades:
        d.polygon([(px * SS, py * SS) for px, py in [(bx - 5, h), (bx + lean, h - height), (bx + 5, h)]], fill=OUTLINE)
    for i, (bx, height, lean) in enumerate(blades):
        color = COLORS["leaf_light"] if i % 2 else COLORS["leaf"]
        d.polygon([(px * SS, py * SS) for px, py in [(bx - 3, h), (bx + lean * 0.9, h - height + 3), (bx + 3, h)]], fill=color)
    return done(image, w, h)


def stone(w=44, h=28):
    image, d = canvas(w, h)
    d.ellipse(S(2, 4, w - 2, h + 12), fill=OUTLINE)
    d.ellipse(S(5, 7, w - 5, h + 9), fill=mix(COLORS["lilac"], COLORS["soil"], 0.45))
    d.ellipse(S(10, 9, w / 2, 16), fill=mix(COLORS["lilac"], COLORS["cream"], 0.4))
    return done(image, w, h)


def crystal(w=40, h=52):
    image, d = canvas(w, h)
    aqua, cream = COLORS["aqua"], COLORS["cream"]
    shards = [(w / 2, 4, 9), (w / 2 - 10, 18, 6), (w / 2 + 10, 22, 6)]
    for cx, top, half in shards:
        pts = [(cx, top), (cx + half, top + 12), (cx + half * 0.8, h), (cx - half * 0.8, h), (cx - half, top + 12)]
        grown = [(px + (2.5 if px > cx else -2.5 if px < cx else 0), py - (2.5 if py == top else 0)) for px, py in pts]
        d.polygon([(px * SS, py * SS) for px, py in grown], fill=OUTLINE)
    for cx, top, half in shards:
        pts = [(cx, top), (cx + half, top + 12), (cx + half * 0.8, h), (cx - half * 0.8, h), (cx - half, top + 12)]
        d.polygon([(px * SS, py * SS) for px, py in pts], fill=element("water", 1))
        d.polygon([(px * SS, py * SS) for px, py in [(cx, top + 2), (cx - half + 2, top + 12), (cx - half * 0.5, h - 4), (cx, h - 6)]],
                  fill=aqua)
        d.line(S(cx - 1, top + 6, cx - 1, top + 14), fill=cream, width=2 * SS)
    image = done(image, w, h)
    halo = image.filter(ImageFilter.GaussianBlur(3))
    return Image.alpha_composite(halo, image)


# ---- 발판 ----
def plank(cracks, w=100, h=40):
    image, d = canvas(w, h)
    wood, light, dark = COLORS["soil"], COLORS["sand"], COLORS["soil_dark"]
    d.rounded_rectangle(S(0, 0, w, h), radius=6 * SS, fill=OUTLINE)
    d.rounded_rectangle(S(3, 3, w - 3, h - 4), radius=4 * SS, fill=mix(wood, light, 0.35))
    d.rounded_rectangle(S(3, 3, w - 3, 11), radius=3 * SS, fill=light)
    d.line(S(3, h / 2 + 3, w - 3, h / 2 + 3), fill=mix(wood, dark, 0.3), width=2 * SS)
    for x in (10, w - 10):
        d.ellipse(S(x - 3, h / 2 - 4, x + 3, h / 2 + 2), fill=OUTLINE)
        d.ellipse(S(x - 1.5, h / 2 - 2.5, x + 0.5, h / 2 - 0.5), fill=COLORS["cream"])
    # 위에 작은 잎 하나 (귀엽게)
    d.ellipse(S(w * 0.62, -2, w * 0.62 + 14, 8), fill=OUTLINE)
    d.ellipse(S(w * 0.62 + 2, 0, w * 0.62 + 12, 6), fill=COLORS["leaf"])
    rnd = random.Random(cracks + 4)
    for _ in range(cracks):
        x = rnd.uniform(18, 82)
        pts = [(x, 4)]
        for k in range(3):
            x += rnd.uniform(-8, 8)
            pts.append((x, 4 + (k + 1) * (h - 10) / 3))
        d.line([(px * SS, py * SS) for px, py in pts], fill=OUTLINE, width=2 * SS)
    return done(image, w, h)


def generate():
    sets = {
        "TerrainGrass": [grass(False, False, 1), grass(True, False, 2), grass(False, True, 3), grass(True, True, 4)],
        "TerrainSide": [side(True), side(False)],
        "TerrainUnder": [under()],
        "TerrainPebble": [pebble(0), pebble(1), pebble(2)],
        "TerrainDecor": [flower(COLORS["berry"]), flower(COLORS["gold"]), mushroom(), tuft(), stone(), crystal()],
        "Platform": [plank(0)],
        "Crumble": [plank(3, h=45)],
    }
    for name, frames in sets.items():
        folder = os.path.join(BASE, name)
        os.makedirs(folder, exist_ok=True)
        for i, img in enumerate(frames):
            img.save(os.path.join(folder, f"{i:02d}.png"))
    return sets


def preview(sets, path):
    """작은 언덕 하나를 런타임과 같은 방식으로 조립해 본다 (확인용)."""
    tiles = ["..........",
             "...####...",
             "..######..",
             "##########",
             "####..####"]
    cell = 100
    rows, cols = len(tiles), len(tiles[0])
    image = Image.new("RGBA", (cols * cell, (rows + 1) * cell), COLORS["dusk"])
    solid = lambda x, y: 0 <= y < rows and (x < 0 or x >= cols or tiles[y][x] == "#")
    for y in range(rows):
        for x in range(cols):
            if tiles[y][x] != "#":
                continue
            px, py = x * cell, (y + 1) * cell
            image.paste(COLORS["soil"], (px, py, px + cell, py + cell))
            if (x * 7 + y * 13) % 5 == 0:
                image.alpha_composite(sets["TerrainPebble"][(x + y) % 3], (px, py))
            if not solid(x - 1, y):
                image.alpha_composite(sets["TerrainSide"][0], (px, py))
            if not solid(x + 1, y):
                image.alpha_composite(sets["TerrainSide"][1], (px, py))
            if y + 1 < rows and not solid(x, y + 1):
                image.alpha_composite(sets["TerrainUnder"][0], (px, py))
    for y in range(rows):
        for x in range(cols):
            if tiles[y][x] != "#" or solid(x, y - 1):
                continue
            left = not solid(x - 1, y) or solid(x - 1, y - 1)
            right = not solid(x + 1, y) or solid(x + 1, y - 1)
            g = sets["TerrainGrass"][(1 if left else 0) + (2 if right else 0)]
            px, py = x * cell - MARGIN, (y + 1) * cell - TOP
            image.alpha_composite(g, (px, py))
            if (x * 5 + y) % 3 == 0:
                deco = sets["TerrainDecor"][(x + y * 3) % 6]
                image.alpha_composite(deco, (x * cell + 50 - deco.width // 2, (y + 1) * cell - deco.height + 6))
    image.alpha_composite(sets["Platform"][0], (0, 2 * cell))
    image.save(path)


if __name__ == "__main__":
    result = generate()
    if len(sys.argv) > 1:
        preview(result, sys.argv[1])
    print("done")
