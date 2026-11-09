"""I Blame You! HUD 체력·마나 바 그림 생성기 (귀엽지만 화려하게).

IMGUI(SkillHUD.Bars.cs)가 9분할(GUIStyle.border)로 늘려 그리는 둥근 캡슐 조각과 아이콘을 만든다.
그림은 화면 픽셀 1:1 크기로 그린다 (9분할 테두리 픽셀 수 = 코드의 RectOffset 값과 맞출 것).

  Assets/Resources/UI/HUD/BarBack.png   36x18  어두운 캡슐 (바 바탕). 9분할 좌우 9픽셀
  Assets/Resources/UI/HUD/BarFill.png   36x18  흰 캡슐 + 윗면 하이라이트. 코드가 체력(분홍)·마나(하늘)로 물들인다. 9분할 좌우 9픽셀
  Assets/Resources/UI/HUD/BarFrame.png  46x28  외곽선 + 크림 테두리 + 바깥 흰 빛. 코드가 바마다 빛 색으로 물들인다. 9분할 좌우 14, 위아래 14
  Assets/Resources/UI/HUD/Heart.png     24x24  체력 하트
  Assets/Resources/UI/HUD/Star.png      24x24  마나 별

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
GLOW = 5  # 테두리 바깥 빛 여백 (BarFrame 크기 = 바 + 2*GLOW)
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


def generate():
    os.makedirs(OUT, exist_ok=True)
    result = {
        "BarBack": bar_back(),
        "BarFill": bar_fill(),
        "BarFrame": bar_frame(),
        "Heart": heart(),
        "Star": star(),
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
    canvas = canvas.resize((canvas.width * scale, canvas.height * scale), Image.NEAREST)
    canvas.save(path)


if __name__ == "__main__":
    out = generate()
    if len(sys.argv) > 1:
        preview(out, sys.argv[1])
    print("done")
