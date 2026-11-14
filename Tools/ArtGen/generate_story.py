"""시작 전 웹툰식 스토리 소개(Core/StoryIntro.cs) 컷 그림.

Docs/Story.md 프롤로그를 7컷으로 그린다. 글(말풍선)은 그림에 넣지 않고 게임에서 IMGUI로 얹는다.
2배 크기로 그린 뒤 줄여 테두리를 부드럽게 한다. 색은 palette.py만 쓴다.

    python3 Tools/ArtGen/generate_story.py

출력: Assets/Resources/Story/Panel01.png ~ Panel07.png (640×360), Bubble.png (말풍선 9칸 바탕)
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

from palette import COLORS, OUTLINE, element, hexrgba

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Story")
WIZARD = os.path.join(ROOT, "Assets", "Art", "Sprites", "Characters", "Wizard", "Idle", "wizard_idle_00.png")
W, H = 640, 360
S = 2  # 그릴 때 배율

A_COLOR = hexrgba("73d9ff")  # 플레이어 A 하늘색 (FeatureTestTools 알약과 같음)
B_COLOR = hexrgba("ffa659")  # 플레이어 B 주황
TRICKS = element("arcane", 1)


def canvas():
    return Image.new("RGBA", (W * S, H * S), COLORS["night"])


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(4))


def gradient(img, top, bottom):
    d = ImageDraw.Draw(img)
    for y in range(img.height):
        d.line([(0, y), (img.width, y)], fill=lerp(top, bottom, y / img.height))


def glow(img, cx, cy, radius, color, strength=180):
    layer = Image.new("RGBA", img.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for i in range(12, 0, -1):
        r = radius * i / 12
        a = int(strength * (1 - i / 12) ** 1.5)
        d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=color[:3] + (a,))
    img.alpha_composite(layer)


def stars(img, count, seed):
    rng = random.Random(seed)
    d = ImageDraw.Draw(img)
    for _ in range(count):
        x, y = rng.randrange(img.width), rng.randrange(int(img.height * 0.6))
        r = rng.choice([2, 2, 3, 4])
        d.ellipse([x - r, y - r, x + r, y + r], fill=COLORS["cream"][:3] + (rng.randrange(120, 255),))


def person(d, cx, base, color, facing, angry=True):
    """둥글고 통통한 사람 (전생의 두 라이벌). base = 발끝 y."""
    o = OUTLINE
    body = [cx - 34, base - 110, cx + 34, base]
    d.rounded_rectangle(body, 30, fill=color, outline=o, width=6)
    hr = 40
    hy = base - 140
    d.ellipse([cx - hr, hy - hr, cx + hr, hy + hr], fill=COLORS["sand"], outline=o, width=6)
    d.pieslice([cx - hr, hy - hr, cx + hr, hy + hr], 180, 360, fill=COLORS["soil_dark"], outline=o, width=6)  # 머리카락
    ex = cx + facing * 14
    d.line([(ex - 10, hy - 2), (ex + 10, hy + 6 * facing * 0 + 4)], fill=o, width=6)  # 찡그린 눈
    d.ellipse([ex - 6, hy + 4, ex + 6, hy + 16], fill=o)
    d.arc([ex - 12, hy + 18, ex + 12, hy + 34], 200, 340, fill=o, width=5)  # 입 (으르렁)
    if angry:  # 빨간 화남 표시
        ax, ay = cx - facing * 30, hy - 40
        for dx, dy in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
            d.arc([ax + dx * 8 - 10, ay + dy * 8 - 10, ax + dx * 8 + 10, ay + dy * 8 + 10], 0, 360, fill=element("fire", 0), width=5)


def soul(img, cx, cy, color, size=1.0):
    glow(img, cx, cy, 90 * size, color, 200)
    d = ImageDraw.Draw(img)
    r = 30 * size
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=color, outline=COLORS["cream"], width=5)
    d.polygon([(cx - r * 0.7, cy + r * 0.5), (cx, cy + r * 2.4), (cx + r * 0.7, cy + r * 0.5)], fill=color)
    d.ellipse([cx - 12, cy - 6, cx - 2, cy + 8], fill=OUTLINE)
    d.ellipse([cx + 2, cy - 6, cx + 12, cy + 8], fill=OUTLINE)


def tricks_eyes(img, cx, cy, scale=1.0, grin=True):
    """악신 트릭스: 어둠 속 보라 눈 둘과 초승달 웃음."""
    glow(img, cx, cy, 380 * scale, TRICKS, 110)
    d = ImageDraw.Draw(img)
    for side in (-1, 1):
        ex = cx + side * 130 * scale
        w, h = 90 * scale, 46 * scale
        d.polygon([(ex - w, cy - side * 10 * scale), (ex + w, cy + side * 10 * scale - 20 * scale), (ex, cy + h)], fill=element("arcane", 2), outline=TRICKS)
        d.ellipse([ex - 14 * scale, cy - 2 * scale, ex + 14 * scale, cy + 26 * scale], fill=OUTLINE)
    if grin:
        d.arc([cx - 220 * scale, cy - 40 * scale, cx + 220 * scale, cy + 260 * scale], 20, 160, fill=element("arcane", 2), width=int(14 * scale))
        for i in range(-4, 5):  # 이빨
            tx = cx + i * 40 * scale
            ty = cy + 110 * scale + abs(i) * abs(i) * -4 * scale + 100 * scale
            d.polygon([(tx - 12 * scale, ty - 14 * scale), (tx + 12 * scale, ty - 14 * scale), (tx, ty + 8 * scale)], fill=COLORS["cream"])


def wizard(img, cx, base, height):
    if not os.path.exists(WIZARD):
        return
    w = Image.open(WIZARD).convert("RGBA")
    w = w.crop(w.getbbox())
    scale = height / w.height
    w = w.resize((int(w.width * scale), int(w.height * scale)), Image.LANCZOS)
    img.alpha_composite(w, (int(cx - w.width / 2), int(base - w.height)))


def burst(img, cx, cy, colors, rays=24):
    d = ImageDraw.Draw(img)
    R = max(img.width, img.height)
    for i in range(rays):
        a0 = 2 * math.pi * i / rays
        a1 = a0 + math.pi / rays
        d.polygon([(cx, cy), (cx + R * math.cos(a0), cy + R * math.sin(a0)), (cx + R * math.cos(a1), cy + R * math.sin(a1))], fill=colors[i % len(colors)])


def city(d, img, horizon, tint=1.0):
    """도시 클라리온: 지붕 실루엣 + 가운데 금이 간 수정 탑."""
    rng = random.Random(7)
    x = 0
    while x < img.width:
        w = rng.randrange(120, 220)
        h = rng.randrange(120, 260)
        d.rectangle([x, horizon - h, x + w, horizon], fill=COLORS["dusk"], outline=OUTLINE, width=5)
        d.polygon([(x - 10, horizon - h), (x + w / 2, horizon - h - 70), (x + w + 10, horizon - h)], fill=COLORS["night"], outline=OUTLINE)
        for wy in range(horizon - h + 30, horizon - 20, 50):
            for wx in range(x + 24, x + w - 24, 46):
                if rng.random() < 0.6:
                    d.rectangle([wx, wy, wx + 18, wy + 24], fill=COLORS["gold"])
        x += w + rng.randrange(0, 30)
    tx = img.width / 2
    top = min(420, horizon - 280)  # 수정 꼭대기가 컷 위로 잘리지 않게
    d.rectangle([tx - 70, horizon - top, tx + 70, horizon], fill=COLORS["soil_dark"], outline=OUTLINE, width=6)
    c = horizon - top - 16  # 수정 가운데 (아래 끝이 탑 꼭대기에 닿게)
    cr = [(tx, c - 120), (tx + 90, c), (tx, c + 20), (tx - 90, c)]
    glow(img, tx, c, 260, element("arcane", 1), 150)
    d = ImageDraw.Draw(img)
    d.polygon(cr, fill=element("arcane", 1), outline=OUTLINE)
    d.line([(tx - 10, c - 90), (tx + 20, c - 40), (tx - 15, c - 10), (tx + 5, c + 12)], fill=OUTLINE, width=6)  # 금
    return d


def panel1():  # 횡단보도의 말싸움
    img = canvas()
    gradient(img, COLORS["night"], COLORS["dusk"])
    stars(img, 40, 1)
    d = ImageDraw.Draw(img)
    horizon = 470
    rng = random.Random(3)
    x = 0
    while x < img.width:
        w, h = rng.randrange(100, 200), rng.randrange(160, 380)
        d.rectangle([x, horizon - h, x + w, horizon], fill=COLORS["night"])
        for wy in range(horizon - h + 20, horizon - 10, 40):
            for wx in range(x + 16, x + w - 16, 36):
                if rng.random() < 0.4:
                    d.rectangle([wx, wy, wx + 14, wy + 18], fill=COLORS["gold"][:3] + (200,))
        x += w + 10
    d.rectangle([0, horizon, img.width, img.height], fill=hexrgba("3a3448"))
    for i in range(9):  # 횡단보도
        sx = 40 + i * 140
        d.polygon([(sx, img.height), (sx + 80, img.height), (sx + 110, horizon + 20), (sx + 50, horizon + 20)], fill=COLORS["cream"][:3] + (230,))
    layer = Image.new("RGBA", (img.width // 2, img.height // 2), (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    person(ld, 250, 340, A_COLOR, 1)
    person(ld, 390, 340, B_COLOR, -1)
    img.alpha_composite(layer.resize(img.size, Image.LANCZOS), (0, 0))
    d = ImageDraw.Draw(img)
    d.line([(560, 380), (720, 380)], fill=COLORS["gold"], width=10)  # 불꽃 튀는 시선
    for k in range(5):
        px = 575 + k * 30
        d.line([(px, 355), (px + 16, 405)], fill=COLORS["gold"], width=6)
    return img


def panel2():  # 트럭
    img = canvas()
    img.paste(COLORS["outline"], (0, 0, img.width, img.height))
    burst(img, 640, 360, [COLORS["cream"][:3] + (255,), COLORS["gold"][:3] + (255,)], 30)
    glow(img, 470, 380, 260, COLORS["cream"], 255)
    glow(img, 810, 380, 260, COLORS["cream"], 255)
    d = ImageDraw.Draw(img)
    d.ellipse([400, 310, 540, 450], fill=COLORS["cream"], outline=OUTLINE, width=8)
    d.ellipse([740, 310, 880, 450], fill=COLORS["cream"], outline=OUTLINE, width=8)
    d.rounded_rectangle([330, 470, 950, 560], 20, fill=hexrgba("6b6f7a"), outline=OUTLINE, width=8)  # 범퍼
    for gx in range(560, 730, 40):
        d.rectangle([gx, 330, gx + 20, 440], fill=hexrgba("6b6f7a"), outline=OUTLINE, width=4)
    for cx, col in ((250, A_COLOR), (1030, B_COLOR)):  # 놀라 굳은 둘 (실루엣)
        d.rounded_rectangle([cx - 40, 520, cx + 40, 700], 30, fill=OUTLINE)
        d.ellipse([cx - 45, 420, cx + 45, 510], fill=OUTLINE, outline=col, width=6)
    return img


def panel3():  # 어둠 속 악신
    img = canvas()
    img.paste(COLORS["outline"], (0, 0, img.width, img.height))
    stars(img, 25, 9)
    tricks_eyes(img, 640, 250, 1.0)
    soul(img, 330, 560, A_COLOR, 0.8)
    soul(img, 950, 560, B_COLOR, 0.8)
    return img


def panel4():  # 두 영혼을 꾹
    img = canvas()
    gradient(img, COLORS["outline"], COLORS["night"])
    d = ImageDraw.Draw(img)
    for i in range(160):  # 소용돌이
        t = i / 160
        a = t * math.pi * 6
        r = 420 * (1 - t)
        for col, off in ((A_COLOR, 0), (B_COLOR, math.pi)):
            x = 640 + r * math.cos(a + off)
            y = 360 + r * 0.6 * math.sin(a + off)
            s = 4 + 14 * t
            d.ellipse([x - s, y - s, x + s, y + s], fill=col[:3] + (int(120 + 120 * t),))
    soul(img, 600, 340, A_COLOR, 1.1)
    soul(img, 680, 340, B_COLOR, 1.1)
    glow(img, 640, 360, 200, COLORS["cream"], 200)
    d = ImageDraw.Draw(img)
    for side in (-1, 1):  # 커다란 보라 손이 양쪽에서 꾹
        hx = 640 + side * 330
        d.rounded_rectangle([hx - 120, 260, hx + 120, 460], 70, fill=TRICKS, outline=OUTLINE, width=8)
        for k in range(4):
            fy = 270 + k * 48
            d.rounded_rectangle([hx - side * 120 - 50, fy, hx - side * 120 + 50, fy + 40], 20, fill=TRICKS, outline=OUTLINE, width=6)
    return img


def panel5():  # 도시 광장에 떨어진 한 몸
    img = canvas()
    gradient(img, COLORS["lilac"], hexrgba("ffd9a8"))
    d = ImageDraw.Draw(img)
    horizon = 520
    d = city(d, img, horizon)
    d.rectangle([0, horizon, img.width, img.height], fill=COLORS["sand"])
    for y in range(horizon + 20, img.height, 40):  # 돌바닥
        for x in range((y // 40 % 2) * 40, img.width, 80):
            d.rounded_rectangle([x, y, x + 74, y + 34], 8, outline=COLORS["soil"], width=3)
    for k in range(10):  # 쿵 먼지
        a = math.pi + k * math.pi / 9
        x, y = 640 + 230 * math.cos(a), 690 + 60 * math.sin(a)
        d.ellipse([x - 40, y - 30, x + 40, y + 30], fill=COLORS["cream"][:3] + (220,), outline=OUTLINE, width=4)
    wizard(img, 640, 700, 330)
    return img


def panel6():  # 하늘의 악신이 내기를 건다
    img = canvas()
    gradient(img, COLORS["night"], COLORS["dusk"])
    d = ImageDraw.Draw(img)
    horizon = 640
    city(d, img, horizon)
    tricks_eyes(img, 640, 130, 0.7)  # 도시 위로 드리운 악신 (탑 앞에 겹쳐 그린다)
    d = ImageDraw.Draw(img)
    d.rectangle([0, horizon, img.width, img.height], fill=COLORS["soil_dark"])
    wizard(img, 640, 700, 130)
    return img


def panel7():  # 제목 컷: 이게 다 너 때문이야!
    img = canvas()
    burst(img, 640, 400, [COLORS["berry"][:3] + (255,), COLORS["gold"][:3] + (255,)], 28)
    glow(img, 640, 420, 360, COLORS["cream"], 220)
    d = ImageDraw.Draw(img)
    for cx, col in ((300, A_COLOR), (980, B_COLOR)):  # 양쪽에서 째려보는 두 영혼 그림자
        soul(img, cx, 520, col, 1.0)
    wizard(img, 640, 720, 360)
    return img


def bubble():
    """말풍선 바탕 (흰 둥근 네모 + 진한 외곽선). 게임에서 9칸(테두리 24)으로 늘린다."""
    size = 96
    img = Image.new("RGBA", (size * S, size * S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([4, 4, size * S - 5, size * S - 5], 40, fill=COLORS["cream"], outline=OUTLINE, width=8)
    return img.resize((size, size), Image.LANCZOS)


def main():
    os.makedirs(OUT, exist_ok=True)
    for i, make in enumerate((panel1, panel2, panel3, panel4, panel5, panel6, panel7), 1):
        img = make().resize((W, H), Image.LANCZOS)
        d = ImageDraw.Draw(img)
        d.rectangle([0, 0, W - 1, H - 1], outline=OUTLINE, width=4)  # 컷 테두리
        img.save(os.path.join(OUT, f"Panel{i:02d}.png"))
    bubble().save(os.path.join(OUT, "Bubble.png"))
    print("saved", OUT)


if __name__ == "__main__":
    main()
