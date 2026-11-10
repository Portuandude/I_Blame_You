"""I Blame You! HUD 체력·마나 바 그림 생성기 (귀엽지만 화려하게).

IMGUI(SkillHUD.Bars.cs)가 9분할(GUIStyle.border)로 늘려 그리는 둥근 캡슐 조각과 아이콘을 만든다.
그림은 화면 픽셀 1:1 크기로 그린다 (9분할 테두리 픽셀 수 = 코드의 RectOffset 값과 맞출 것).

  Assets/Resources/UI/HUD/BarBack.png   36x18  어두운 캡슐 (바 바탕). 9분할 좌우 9픽셀
  Assets/Resources/UI/HUD/BarFill.png   36x18  흰 캡슐 + 윗면 하이라이트. 코드가 체력(분홍)·마나(하늘)로 물들인다. 9분할 좌우 9픽셀
  Assets/Resources/UI/HUD/BarFrame.png  46x28  외곽선 + 크림 테두리 + 바깥 흰 빛. 코드가 바마다 빛 색으로 물들인다. 9분할 좌우 14, 위아래 14
  Assets/Resources/UI/HUD/Heart.png     24x24  체력 하트
  Assets/Resources/UI/HUD/Star.png      24x24  마나 별
  Assets/Resources/UI/HUD/Crown.png     24x24  보스 체력바 왕관
  Assets/Resources/UI/HUD/Flag.png      22x22  스테이지 진행 바 끝 깃발
  Assets/Resources/UI/HUD/RoomTile.png  16x16  미니맵 방 칸 (흰 둥근 사각, 코드가 물들임). 9분할 사방 5픽셀
  Assets/Resources/UI/HUD/MapChest.png  16x16  미니맵 보물 방 상자
  Assets/Resources/UI/HUD/MapHelmet.png 16x16  미니맵 중간보스 방 투구
  Assets/Resources/UI/HUD/MapElite.png  16x16  미니맵 정예 방 보라 별
  Assets/Resources/UI/HUD/MapLock.png   16x16  미니맵 안 연 협동 봉인문 자물쇠 (방 칸 오른쪽 아래에 작게)
  Assets/Resources/UI/HUD/Panel.png     32x32  HUD 패널 (진한 보라 반투명 + 크림 테두리, 둥근 모서리). 9분할 사방 10픽셀
  Assets/Resources/UI/HUD/Button.png         32x32  둥근 버튼 보통 (dusk→lilac 젤리 + 크림 테두리 + 아래 두께). 9분할 사방 10픽셀
  Assets/Resources/UI/HUD/ButtonHover.png    32x32  마우스를 올렸을 때 (조금 밝게 + 빛 테두리)
  Assets/Resources/UI/HUD/ButtonActive.png   32x32  누르고 있을 때 (아래 두께가 눌려 2픽셀 내려감, 조금 어둡게)
  Assets/Resources/UI/HUD/CardFrame.png 32x32  특성·유물 카드 테두리 (흰 둥근 테두리 + 안쪽 옅은 빛, 코드가 소유자 색으로 물들임). 9분할 사방 10픽셀
  Assets/Resources/UI/HUD/Pill.png      24x24  알림 피드 줄 배경 (반투명 night 알약 + 옅은 크림 테두리). 9분할 좌우 11픽셀

    python3 Tools/ArtGen/generate_hud.py [미리보기.png]
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(__file__))
from palette import COLORS, OUTLINE, hexrgba  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "UI", "HUD")

SS = 8  # 크게 그린 뒤 줄여서 둥근 끝을 부드럽게
BAR_W, BAR_H = 36, 18
GLOW = 5  # 테두리 바깥 빛 여백 (BarFrame 크기 = 바 + 2*GLOW). BAR_H·GLOW를 바꾸면 SkillHUD.Bars.cs 상수·9분할 값도 함께
CREAM = COLORS["cream"]
NIGHT = COLORS["night"]
WHITE = (255, 255, 255, 255)


def big(w, h):
    return Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))


def small(img, w, h):
    return img.resize((w, h), Image.LANCZOS)


def capsule(draw, x0, y0, x1, y1, fill=None, outline=None, width=0):
    r = (y1 - y0) / 2
    draw.rounded_rectangle([x0 * SS, y0 * SS, x1 * SS - 1, y1 * SS - 1], radius=r * SS, fill=fill,
                           outline=outline, width=int(width * SS))


def bar_back():
    img = big(BAR_W, BAR_H)
    d = ImageDraw.Draw(img)
    capsule(d, 0, 0, BAR_W, BAR_H, fill=hexrgba("1a1430", 235))
    # 아래쪽 안쪽 그림자 대신 위쪽을 살짝 더 어둡게: 홈이 파인 느낌
    capsule(d, 1.5, 1, BAR_W - 1.5, BAR_H * 0.55, fill=hexrgba("0f0b1e", 200))
    return small(img, BAR_W, BAR_H)


def bar_fill():
    img = big(BAR_W, BAR_H)
    d = ImageDraw.Draw(img)
    # 몸통: 밝은 회색 (물들이면 조금 어두운 색), 아래쪽은 더 어둡게
    capsule(d, 0, 0, BAR_W, BAR_H, fill=(225, 225, 230, 255))
    capsule(d, 0, BAR_H * 0.5, BAR_W, BAR_H, fill=(185, 185, 200, 255))
    capsule(d, 0, 0, BAR_W, BAR_H * 0.8, fill=(225, 225, 230, 255))
    # 윗면 반짝 띠 (흰색: 물들여도 밝게 남는다)
    capsule(d, 3.5, 2.5, BAR_W - 3.5, 7, fill=WHITE)
    return small(img, BAR_W, BAR_H)


def bar_frame():
    w, h = BAR_W + 2 * GLOW, BAR_H + 2 * GLOW
    glow = big(w, h)
    g = ImageDraw.Draw(glow)
    capsule(g, GLOW - 1.5, GLOW - 1.5, w - GLOW + 1.5, h - GLOW + 1.5, outline=(255, 255, 255, 200), width=3)
    glow = small(glow, w, h).filter(ImageFilter.GaussianBlur(2.2))

    rim = big(w, h)
    r = ImageDraw.Draw(rim)
    capsule(r, GLOW - 1, GLOW - 1, w - GLOW + 1, h - GLOW + 1, outline=OUTLINE, width=1.6)
    capsule(r, GLOW - 0.2, GLOW - 0.2, w - GLOW + 0.2, h - GLOW + 0.2, outline=CREAM, width=1.1)
    rim = small(rim, w, h)
    return Image.alpha_composite(glow, rim)


def heart_shape(d, cx, cy, s, col):
    # 두 원 + 아래 뾰족한 삼각형
    r = s * 0.5
    d.ellipse([(cx - s) * SS, (cy - s * 0.75) * SS, (cx - s + 2 * r + s * 0.05) * SS, (cy - s * 0.75 + 2 * r) * SS], fill=col)
    d.ellipse([(cx - s * 0.05) * SS, (cy - s * 0.75) * SS, (cx + s) * SS, (cy - s * 0.75 + 2 * r) * SS], fill=col)
    d.polygon([((cx - s * 0.97) * SS, (cy - s * 0.15) * SS), ((cx + s * 0.97) * SS, (cy - s * 0.15) * SS),
               (cx * SS, (cy + s * 0.95) * SS)], fill=col)


def heart():
    n = 24
    img = big(n, n)
    d = ImageDraw.Draw(img)
    heart_shape(d, 12, 12.5, 10.5, OUTLINE)
    heart_shape(d, 12, 12.3, 8.8, COLORS["berry"])
    heart_shape(d, 12, 13.6, 6.0, hexrgba("ff3f86"))
    d.ellipse([6 * SS, 6 * SS, 10 * SS, 9.5 * SS], fill=CREAM)  # 반짝
    d.ellipse([10.5 * SS, 6.5 * SS, 12 * SS, 8 * SS], fill=CREAM)
    return small(img, n, n)


def star_points(cx, cy, r_out, r_in, n=5):
    pts = []
    for i in range(n * 2):
        a = -math.pi / 2 + i * math.pi / n
        r = r_out if i % 2 == 0 else r_in
        pts.append(((cx + math.cos(a) * r) * SS, (cy + math.sin(a) * r) * SS))
    return pts


def star():
    n = 24
    img = big(n, n)
    d = ImageDraw.Draw(img)
    d.polygon(star_points(12, 12.8, 11.5, 5.6), fill=OUTLINE)
    d.polygon(star_points(12, 12.8, 9.4, 4.4), fill=hexrgba("4099ff"))
    d.polygon(star_points(12, 13.3, 6.2, 3.0), fill=COLORS["aqua"])
    d.ellipse([9 * SS, 7.5 * SS, 12 * SS, 10.5 * SS], fill=CREAM)  # 반짝
    return small(img, n, n)


def crown():
    n = 24
    img = big(n, n)
    d = ImageDraw.Draw(img)

    def body(grow, col):
        pts = [(2.5 - grow, 19 + grow), (2.5 - grow, 7.5 - grow), (7.5, 12.5 - grow), (12, 4 - grow * 1.3),
               (16.5, 12.5 - grow), (21.5 + grow, 7.5 - grow), (21.5 + grow, 19 + grow)]
        d.polygon([(x * SS, y * SS) for x, y in pts], fill=col)

    body(1.6, OUTLINE)
    for x, y in ((2.5, 7), (12, 3.5), (21.5, 7)):  # 뾰족한 끝의 동그란 장식 (외곽선)
        d.ellipse([(x - 2.4) * SS, (y - 2.4) * SS, (x + 2.4) * SS, (y + 2.4) * SS], fill=OUTLINE)
    body(0, COLORS["gold"])
    d.rectangle([2.5 * SS, 15.5 * SS, 21.5 * SS, 19 * SS], fill=hexrgba("e0a92e"))  # 아래 띠 그림자
    for x, y in ((2.5, 7), (12, 3.5), (21.5, 7)):
        d.ellipse([(x - 1.4) * SS, (y - 1.4) * SS, (x + 1.4) * SS, (y + 1.4) * SS], fill=CREAM)
    d.ellipse([9.5 * SS, 13 * SS, 14.5 * SS, 18 * SS], fill=COLORS["berry"])  # 가운데 보석
    d.ellipse([10.5 * SS, 13.8 * SS, 12.3 * SS, 15.6 * SS], fill=CREAM)
    d.ellipse([4.5 * SS, 9.5 * SS, 6.5 * SS, 12.5 * SS], fill=CREAM)  # 반짝
    return small(img, n, n)


def flag():
    n = 22
    img = big(n, n)
    d = ImageDraw.Draw(img)
    # 깃대 (외곽선 → 크림)
    d.rectangle([4 * SS, 2 * SS, 7 * SS, 21 * SS], fill=OUTLINE)
    d.rectangle([5 * SS, 3 * SS, 6 * SS, 20 * SS], fill=CREAM)
    d.ellipse([3 * SS, 0.5 * SS, 8 * SS, 5.5 * SS], fill=OUTLINE)
    d.ellipse([4 * SS, 1.5 * SS, 7 * SS, 4.5 * SS], fill=COLORS["gold"])
    # 물결 깃발 (berry)
    top = [(6.5, 4), (11, 3), (15, 5), (20, 4)]
    bottom = [(20, 12), (15, 13), (11, 11), (6.5, 12.5)]
    d.polygon([(x * SS, y * SS) for x, y in [(6.5, 3)] + [(x, y - 1) for x, y in top[1:]] + [(21, 4), (21, 12.5)]
               + [(x, y + 1) for x, y in bottom] + [(6.5, 13.5)]], fill=OUTLINE)
    d.polygon([(x * SS, y * SS) for x, y in top + bottom], fill=COLORS["berry"])
    d.polygon([(x * SS, y * SS) for x, y in [(7, 4.5), (11, 3.8), (15, 5.8), (19.5, 4.8), (19.5, 6.5), (15, 7.5), (11, 5.8), (7, 6.5)]],
              fill=hexrgba("ffb3d4"))
    return small(img, n, n)


def panel():
    n = 32
    img = big(n, n)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, n * SS - 1, n * SS - 1], radius=8 * SS, fill=hexrgba("120e24", 230))   # 바깥 외곽선
    d.rounded_rectangle([1 * SS, 1 * SS, (n - 1) * SS - 1, (n - 1) * SS - 1], radius=7 * SS, fill=CREAM)  # 크림 테두리
    d.rounded_rectangle([2.5 * SS, 2.5 * SS, (n - 2.5) * SS - 1, (n - 2.5) * SS - 1], radius=5.5 * SS,
                        fill=hexrgba("2a2150", 215))  # night 반투명 안쪽
    # 위쪽 안쪽에 옅은 lilac 띠 (유리 같은 반짝)
    d.rounded_rectangle([4 * SS, 3.5 * SS, (n - 4) * SS - 1, 6 * SS], radius=1.2 * SS, fill=hexrgba("a68cf0", 70))
    return small(img, n, n)


def button(state):
    """둥근 젤리 버튼. state: normal / hover / active. 아래 3픽셀은 두께(누르면 납작해짐)."""
    n = 32
    img = big(n, n)
    d = ImageDraw.Draw(img)
    body = {"normal": ("5a46a8", "8a70e0"), "hover": ("6e58c4", "a68cf0"), "active": ("4a3a90", "7058c0")}[state]
    top = 2 if state == "active" else 0  # 눌리면 윗면이 내려간다
    # 바깥 외곽선 + 아래 두께 (진한 보라)
    d.rounded_rectangle([0, 0, n * SS - 1, n * SS - 1], radius=9 * SS, fill=OUTLINE)
    d.rounded_rectangle([1 * SS, (1 + top) * SS, (n - 1) * SS - 1, (n - 1) * SS - 1], radius=8 * SS, fill=hexrgba("2a2150"))
    # 크림 테두리 → 몸통 (아래쪽 진한 색, 위쪽 밝은 색)
    face_bottom = n - 4 if state != "active" else n - 2
    d.rounded_rectangle([1 * SS, (1 + top) * SS, (n - 1) * SS - 1, face_bottom * SS - 1], radius=8 * SS,
                        fill=CREAM if state != "hover" else hexrgba("ffffff"))
    d.rounded_rectangle([2.5 * SS, (2.5 + top) * SS, (n - 2.5) * SS - 1, (face_bottom - 1.5) * SS - 1], radius=6.5 * SS,
                        fill=hexrgba(body[0]))
    d.rounded_rectangle([2.5 * SS, (2.5 + top) * SS, (n - 2.5) * SS - 1, (face_bottom - 1.5) * SS * 0.62 + (2.5 + top) * SS * 0.38 - 1],
                        radius=6.5 * SS, fill=hexrgba(body[1]))
    # 위쪽 반짝 띠 + 왼쪽 위 반짝 점
    d.rounded_rectangle([5 * SS, (4 + top) * SS, (n - 5) * SS - 1, (6.2 + top) * SS], radius=1.1 * SS,
                        fill=hexrgba("fff4d6", 150 if state != "active" else 90))
    d.ellipse([4.5 * SS, (4 + top) * SS, 6.8 * SS, (6.3 + top) * SS], fill=CREAM)
    out = small(img, n, n)
    if state == "hover":  # 바깥으로 살짝 번지는 빛 대신 테두리 안쪽을 더 밝게 (9분할이라 바깥 여백은 쓰지 않는다)
        glow = big(n, n)
        g = ImageDraw.Draw(glow)
        g.rounded_rectangle([3 * SS, 3 * SS, (n - 3) * SS - 1, (n - 5) * SS - 1], radius=6 * SS,
                            outline=hexrgba("ffffff", 110), width=2 * SS)
        out = Image.alpha_composite(out, small(glow, n, n).filter(ImageFilter.GaussianBlur(0.8)))
    return out


def card_frame():
    n = 32
    ring = big(n, n)
    d = ImageDraw.Draw(ring)
    d.rounded_rectangle([0, 0, n * SS - 1, n * SS - 1], radius=8 * SS, outline=WHITE, width=int(2.2 * SS))
    inner = big(n, n)
    g = ImageDraw.Draw(inner)
    g.rounded_rectangle([2 * SS, 2 * SS, (n - 2) * SS - 1, (n - 2) * SS - 1], radius=6.5 * SS,
                        outline=(255, 255, 255, 120), width=int(2 * SS))
    inner = small(inner, n, n).filter(ImageFilter.GaussianBlur(1.0))
    return Image.alpha_composite(inner, small(ring, n, n))


def pill():
    n = 24
    img = big(n, n)
    d = ImageDraw.Draw(img)
    capsule(d, 0, 0, n, n, fill=hexrgba("fff4d6", 110))            # 옅은 크림 테두리
    capsule(d, 1, 1, n - 1, n - 1, fill=hexrgba("1a1430", 185))    # 반투명 night 몸통
    capsule(d, 3, 2.5, n - 3, 8, fill=hexrgba("a68cf0", 45))       # 위쪽 유리 반짝
    return small(img, n, n)


def room_tile():
    n = 16
    img = big(n, n)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([0, 0, n * SS - 1, n * SS - 1], radius=4.5 * SS, fill=OUTLINE)
    d.rounded_rectangle([1 * SS, 1 * SS, (n - 1) * SS - 1, (n - 1) * SS - 1], radius=3.5 * SS, fill=(235, 235, 240, 255))
    d.rounded_rectangle([2 * SS, 1.8 * SS, (n - 2) * SS - 1, 4.5 * SS], radius=1.5 * SS, fill=WHITE)  # 위쪽 반짝
    return small(img, n, n)


def map_chest():
    n = 16
    img = big(n, n)
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([1 * SS, 3 * SS, 15 * SS, 14.5 * SS], radius=2 * SS, fill=OUTLINE)
    d.rounded_rectangle([2.2 * SS, 4.2 * SS, 13.8 * SS, 13.3 * SS], radius=1.4 * SS, fill=COLORS["soil"])
    d.rectangle([2.2 * SS, 7.2 * SS, 13.8 * SS, 8.6 * SS], fill=COLORS["gold"])   # 뚜껑 띠
    d.rectangle([2.2 * SS, 4.2 * SS, 13.8 * SS, 5.4 * SS], fill=COLORS["sand"])   # 뚜껑 윗면
    d.rounded_rectangle([6.6 * SS, 6.4 * SS, 9.4 * SS, 10.4 * SS], radius=0.8 * SS, fill=COLORS["gold"])  # 자물쇠
    d.ellipse([7.5 * SS, 8 * SS, 8.5 * SS, 9 * SS], fill=OUTLINE)
    return small(img, n, n)


def map_helmet():
    n = 16
    img = big(n, n)
    d = ImageDraw.Draw(img)
    d.pieslice([1 * SS, 2 * SS, 15 * SS, 20 * SS], 180, 360, fill=OUTLINE)          # 둥근 투구
    d.rectangle([1 * SS, 10.5 * SS, 15 * SS, 14.5 * SS], fill=OUTLINE)
    d.pieslice([2.3 * SS, 3.3 * SS, 13.7 * SS, 18.7 * SS], 180, 360, fill=(205, 210, 225, 255))
    d.rectangle([2.3 * SS, 10.5 * SS, 13.7 * SS, 13.2 * SS], fill=(205, 210, 225, 255))
    d.rectangle([3.5 * SS, 8 * SS, 12.5 * SS, 9.6 * SS], fill=OUTLINE)              # 눈 틈
    d.polygon([(8 * SS, 0), (11 * SS, 3 * SS), (8 * SS, 4.5 * SS)], fill=hexrgba("ff7326"))  # 깃털
    d.ellipse([4 * SS, 4.5 * SS, 6.5 * SS, 7 * SS], fill=WHITE)                      # 반짝
    return small(img, n, n)


def map_elite():
    n = 16
    img = big(n, n)
    d = ImageDraw.Draw(img)
    d.polygon(star_points(8, 8.6, 7.8, 3.8), fill=OUTLINE)
    d.polygon(star_points(8, 8.6, 6.2, 3.0), fill=hexrgba("bf73ff"))
    d.ellipse([6 * SS, 5 * SS, 8 * SS, 7 * SS], fill=CREAM)
    return small(img, n, n)


def map_lock():
    n = 16
    img = big(n, n)
    d = ImageDraw.Draw(img)
    d.arc([3.6 * SS, 0.8 * SS, 12.4 * SS, 10.5 * SS], 180, 360, fill=OUTLINE, width=int(3.4 * SS))      # 고리 외곽선
    d.arc([4.6 * SS, 1.8 * SS, 11.4 * SS, 9.5 * SS], 180, 360, fill=(205, 210, 225, 255), width=int(1.5 * SS))  # 은빛 고리
    d.rounded_rectangle([1.5 * SS, 6 * SS, 14.5 * SS, 15.2 * SS], radius=2.4 * SS, fill=OUTLINE)
    d.rounded_rectangle([2.7 * SS, 7.2 * SS, 13.3 * SS, 14 * SS], radius=1.6 * SS, fill=COLORS["gold"])
    d.rectangle([2.7 * SS, 7.2 * SS, 13.3 * SS, 8.4 * SS], fill=COLORS["sand"])  # 윗면 빛
    d.ellipse([6.8 * SS, 9 * SS, 9.2 * SS, 11.4 * SS], fill=OUTLINE)              # 열쇠 구멍
    d.rectangle([7.5 * SS, 10.5 * SS, 8.5 * SS, 12.8 * SS], fill=OUTLINE)
    return small(img, n, n)


def generate():
    os.makedirs(OUT, exist_ok=True)
    result = {
        "BarBack": bar_back(),
        "BarFill": bar_fill(),
        "BarFrame": bar_frame(),
        "Heart": heart(),
        "Star": star(),
        "Crown": crown(),
        "Flag": flag(),
        "Panel": panel(),
        "Button": button("normal"),
        "ButtonHover": button("hover"),
        "ButtonActive": button("active"),
        "Pill": pill(),
        "CardFrame": card_frame(),
        "RoomTile": room_tile(),
        "MapChest": map_chest(),
        "MapHelmet": map_helmet(),
        "MapElite": map_elite(),
        "MapLock": map_lock(),
    }
    for name, img in result.items():
        img.save(os.path.join(OUT, f"{name}.png"))
    return result


def preview(result, path):
    """게임처럼 9분할로 늘리고 물들인 모습을 4배로 보여 준다."""
    scale = 4

    def nine(img, w, h, bx, by):
        out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        sw, sh = img.size
        xs = [(0, bx, 0, bx), (bx, sw - bx, bx, w - bx), (sw - bx, sw, w - bx, w)]
        ys = [(0, by, 0, by), (by, sh - by, by, h - by), (sh - by, sh, h - by, h)]
        for sx0, sx1, dx0, dx1 in xs:
            for sy0, sy1, dy0, dy1 in ys:
                if sx1 <= sx0 or sy1 <= sy0 or dx1 <= dx0 or dy1 <= dy0:
                    continue
                out.alpha_composite(img.crop((sx0, sy0, sx1, sy1)).resize((dx1 - dx0, dy1 - dy0)), (dx0, dy0))
        return out

    def tint(img, col):
        px = [(a * col[0] // 255, b * col[1] // 255, c * col[2] // 255, e * col[3] // 255) for a, b, c, e in img.getdata()]
        out = Image.new("RGBA", img.size)
        out.putdata(px)
        return out

    canvas = Image.new("RGBA", (380, 60), hexrgba("3a3550"))
    bw = 140
    for i, (icon, fill_col, glow_col, ratio, trail) in enumerate([
        ("Heart", (255, 95, 150, 255), (255, 140, 190, 255), 0.55, 0.75),
        ("Star", (90, 180, 255, 255), (127, 246, 255, 255), 0.8, 0.8),
    ]):
        x = 8 + i * 186
        y = 20
        canvas.alpha_composite(result[icon], (x, y - 3))
        bx = x + 26
        canvas.alpha_composite(tint(nine(result["BarFrame"], bw + 2 * GLOW, BAR_H + 2 * GLOW, 14, 14), glow_col),
                               (bx - GLOW, y - GLOW))
        canvas.alpha_composite(nine(result["BarBack"], bw, BAR_H, 9, 0), (bx, y))
        trail_img = nine(result["BarFill"], bw, BAR_H, 9, 0).crop((0, 0, int(bw * trail), BAR_H))
        canvas.alpha_composite(trail_img, (bx, y))
        fill_img = tint(nine(result["BarFill"], bw, BAR_H, 9, 0), fill_col).crop((0, 0, int(bw * ratio), BAR_H))
        canvas.alpha_composite(fill_img, (bx, y))
    canvas.alpha_composite(result["Crown"], (350, 18))
    canvas.alpha_composite(result["Flag"], (350, 40))
    canvas = canvas.resize((canvas.width * scale, canvas.height * scale), Image.NEAREST)
    canvas.save(path)


if __name__ == "__main__":
    out = generate()
    if len(sys.argv) > 1:
        preview(out, sys.argv[1])
    print("done")
