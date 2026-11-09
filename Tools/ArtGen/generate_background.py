"""I Blame You! 시차(패럴랙스) 배경 생성기.

Docs/ArtStyle.md 의 배경 규칙대로 3겹(먼 하늘 / 중간 산·숲 / 가까운 풀숲)과 떠다니는 반짝이 프레임을
Assets/Resources/Stage/Bg* 아래에 PNG로 저장한다. 런타임에 ParallaxBackground가 읽는다 (100px = 1유닛).
중간·가까운 층은 가로로 이어 붙일 수 있게(좌우 끝이 맞게) 그린다.

    python3 Tools/ArtGen/generate_background.py [미리보기.png]
"""
import math
import os
import random
import sys

from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(__file__))
from palette import COLORS, hexrgba  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
BASE = os.path.join(ROOT, "Assets", "Resources", "Stage")


def mix(a, b, t, alpha=None):
    out = tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))
    return out + ((a[3] if alpha is None else alpha),)


def periodic(x, width, waves):
    """가로로 이어지는 높이 함수: 정수 주기의 사인 합이라 x=0과 x=width가 같다."""
    return sum(amp * math.sin(2 * math.pi * (k * x / width) + phase) for k, amp, phase in waves)


def star(draw, x, y, r, color):
    """4갈래 별 (가운데 밝고 끝으로 갈수록 가늘게)."""
    draw.polygon([(x, y - r), (x + r * 0.25, y - r * 0.25), (x + r, y), (x + r * 0.25, y + r * 0.25),
                  (x, y + r), (x - r * 0.25, y + r * 0.25), (x - r, y), (x - r * 0.25, y - r * 0.25)], fill=color)


def sky():
    """먼 하늘: 카메라에 붙는 한 장 (16:9). 위 night → 아래 lilac 빛, 별과 초승달."""
    w, h = 640, 360
    image = Image.new("RGBA", (w, h))
    draw = ImageDraw.Draw(image)
    night, dusk, lilac = COLORS["night"], COLORS["dusk"], COLORS["lilac"]
    for y in range(h):
        t = y / (h - 1)
        color = mix(night, dusk, t / 0.65) if t < 0.65 else mix(dusk, lilac, (t - 0.65) / 0.35 * 0.8)
        draw.line([(0, y), (w, y)], fill=color)

    rnd = random.Random(7)
    glow_layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    glow_draw = ImageDraw.Draw(glow_layer)
    for _ in range(90):
        x, y = rnd.uniform(0, w), rnd.uniform(0, h * 0.7) ** 1.0
        color = COLORS["cream"] if rnd.random() < 0.7 else COLORS["aqua"]
        a = int(255 * (1 - y / (h * 0.8)))
        if rnd.random() < 0.12:
            star(glow_draw, x, y, rnd.choice((3, 5)), color[:3] + (a,))
        else:
            r = rnd.choice((0.8, 1.2, 1.6))
            glow_draw.ellipse([x - r, y - r, x + r, y + r], fill=color[:3] + (a,))

    # 초승달 (오른쪽 위): 크림색 원에서 하늘색 원을 빼서
    mx, my, mr = w * 0.8, h * 0.2, 26
    moon = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    moon_draw = ImageDraw.Draw(moon)
    moon_draw.ellipse([mx - mr, my - mr, mx + mr, my + mr], fill=COLORS["cream"])
    moon_draw.ellipse([mx - mr + 14, my - mr - 6, mx + mr + 14, my + mr - 6], fill=(0, 0, 0, 0))
    halo = moon.filter(ImageFilter.GaussianBlur(10))
    glow_layer = Image.alpha_composite(glow_layer, halo)
    glow_layer = Image.alpha_composite(glow_layer, moon)

    # 몽글몽글 구름 띠 (아래쪽, 옅게)
    clouds = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    cloud_draw = ImageDraw.Draw(clouds)
    for i in range(14):
        cx = i * w / 13 + rnd.uniform(-20, 20)
        cy = h * 0.78 + rnd.uniform(-12, 12)
        r = rnd.uniform(26, 48)
        cloud_draw.ellipse([cx - r * 1.6, cy - r, cx + r * 1.6, cy + r], fill=lilac[:3] + (70,))
    clouds = clouds.filter(ImageFilter.GaussianBlur(6))
    image = Image.alpha_composite(image, clouds)
    return Image.alpha_composite(image, glow_layer)


def mid():
    """중간 층: 둥근 먼 산 + 막대사탕 나무 실루엣. 1280x480, 가로로 이어짐."""
    w, h = 1280, 480
    image = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    dusk, lilac, night = COLORS["dusk"], COLORS["lilac"], COLORS["night"]

    # 먼 산 (lilac 쪽, 둥근 봉우리)
    far = mix(dusk, lilac, 0.55)
    waves = [(2, 40, 0.3), (3, 28, 1.7), (5, 14, 0.9), (9, 6, 2.2)]
    for x in range(w):
        top = h * 0.38 + periodic(x, w, waves)
        draw.line([(x, top), (x, h)], fill=far)
    # 봉우리 눈 모자 대신 밝은 테두리
    for x in range(w):
        top = h * 0.38 + periodic(x, w, waves)
        draw.line([(x, top), (x, top + 3)], fill=mix(far, COLORS["cream"], 0.35))

    # 가까운 언덕 (dusk, 조금 진하게)
    near = mix(dusk, night, 0.25)
    waves2 = [(3, 30, 2.0), (4, 18, 0.4), (7, 9, 1.1)]
    for x in range(w):
        top = h * 0.62 + periodic(x, w, waves2)
        draw.line([(x, top), (x, h)], fill=near)

    # 막대사탕 나무 (둥근 잎 + 가는 줄기), 언덕 위에. 좌우 끝을 넘는 나무는 반대쪽에도 그려 이어지게
    rnd = random.Random(11)
    tree = mix(dusk, night, 0.45)
    tree_light = mix(tree, lilac, 0.35)
    for i in range(16):
        x = (i + rnd.uniform(-0.3, 0.3)) * w / 16
        base = h * 0.62 + periodic(x, w, waves2) + 6
        height = rnd.uniform(60, 110)
        r = rnd.uniform(26, 40)
        for dx in (-w, 0, w):
            tx = x + dx
            if tx + r < 0 or tx - r > w:
                continue
            draw.rectangle([tx - 3, base - height, tx + 3, base], fill=tree)
            draw.ellipse([tx - r, base - height - r, tx + r, base - height + r], fill=tree)
            draw.ellipse([tx - r * 0.7, base - height - r * 0.85, tx - r * 0.05, base - height - r * 0.3], fill=tree_light)
    return image


def near():
    """가까운 층: 동글동글 풀숲 + 작은 꽃. 1280x220, 가로로 이어짐, 살짝 반투명."""
    w, h = 1280, 220
    image = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    bush = mix(COLORS["leaf_dark"], COLORS["night"], 0.35)
    bush_light = mix(COLORS["leaf"], COLORS["night"], 0.3)
    rnd = random.Random(23)
    blobs = []
    for i in range(30):
        x = (i + rnd.uniform(-0.4, 0.4)) * w / 30
        r = rnd.uniform(34, 62)
        blobs.append((x, h - r * 0.55, r))
    for x, y, r in blobs:
        for dx in (-w, 0, w):
            cx = x + dx
            if cx + r < 0 or cx - r > w:
                continue
            draw.ellipse([cx - r, y - r * 0.8, cx + r, y + r], fill=bush)
    for x, y, r in blobs:
        for dx in (-w, 0, w):
            cx = x + dx
            if cx + r < 0 or cx - r > w:
                continue
            draw.arc([cx - r * 0.8, y - r * 0.7, cx + r * 0.6, y + r * 0.6], 200, 290, fill=bush_light, width=5)
    draw.rectangle([0, h - 30, w, h], fill=bush)
    # 작은 꽃 (berry/gold, 어둡게)
    for i in range(22):
        x = rnd.uniform(0, w)
        y = h - rnd.uniform(28, 70)
        color = mix(COLORS["berry"] if i % 3 else COLORS["gold"], COLORS["night"], 0.3)
        for k in range(5):
            a = k * 2 * math.pi / 5
            draw.ellipse([x + math.cos(a) * 4 - 3, y + math.sin(a) * 4 - 3, x + math.cos(a) * 4 + 3, y + math.sin(a) * 4 + 3], fill=color)
        draw.ellipse([x - 2, y - 2, x + 2, y + 2], fill=mix(COLORS["cream"], COLORS["night"], 0.2))
    alpha = image.getchannel("A").point(lambda a: int(a * 0.92))
    image.putalpha(alpha)
    return image


def sparkle_frames():
    """떠다니는 반짝이: 4프레임 깜빡임 (작은 점 → 4갈래 별 → 점). 32x32, 중앙 피벗."""
    frames = []
    for i, (r, core) in enumerate([(4, 2), (8, 2.5), (12, 3), (7, 2)]):
        s = 4
        image = Image.new("RGBA", (32 * s, 32 * s), (0, 0, 0, 0))
        draw = ImageDraw.Draw(image)
        c = 16 * s
        star(draw, c, c, r * s, (255, 255, 255, 230))
        draw.ellipse([c - core * s, c - core * s, c + core * s, c + core * s], fill=(255, 255, 255, 255))
        image = image.resize((32, 32), Image.LANCZOS)
        image = Image.alpha_composite(image.filter(ImageFilter.GaussianBlur(2)), image)
        frames.append(image)
    return frames


def generate():
    sets = {"BgSky": [sky()], "BgMid": [mid()], "BgNear": [near()], "Sparkle": sparkle_frames()}
    for name, frames in sets.items():
        folder = os.path.join(BASE, name)
        os.makedirs(folder, exist_ok=True)
        for i, img in enumerate(frames):
            img.save(os.path.join(folder, f"{i:02d}.png"))
    return sets


def preview(sets, path):
    """카메라 한 화면 모양으로 겹쳐 본 미리보기 (확인용, 저장소에 넣지 않는다)."""
    w, h = 1280, 720
    image = sets["BgSky"][0].resize((w, h))
    m = sets["BgMid"][0]
    image.alpha_composite(m, (0, h - m.height - 60))
    n = sets["BgNear"][0]
    image.alpha_composite(n, (0, h - n.height))
    image.save(path)


if __name__ == "__main__":
    result = generate()
    if len(sys.argv) > 1:
        preview(result, sys.argv[1])
    print("done")
