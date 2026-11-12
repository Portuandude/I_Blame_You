"""I Blame You! NPC 마을 건물 그림 생성기 (귀엽지만 화려하게).

마을 방(RoomKind.Village)에 서는 두 건물. 16픽셀 = 1유닛 (Stage/VillageBuilding.cs가 같은 값으로 자른다).

  Assets/Resources/Village/Restaurant.png  56x56  식당: 빨강·크림 줄무늬 차양, 김 나는 그릇 간판
  Assets/Resources/Village/Shop.png        56x56  장비 상점: 파란 지붕, 별 지팡이 간판
  Assets/Resources/Village/Lamp.png        12x40  가로등 (따뜻한 노란 불빛, Stage/VillageDecor.cs)
  Assets/Resources/Village/Flowers0~2.png  16x8   꽃 덤불 세 가지 (분홍·노랑·하늘)
  Assets/Resources/Village/Fence.png       16x10  나무 울타리 한 칸 (가로로 이어 붙인다)
  Assets/Resources/Village/Fountain.png    40x28  작은 분수 (돌 그릇 + 물줄기)

    python3 Tools/ArtGen/generate_village.py
"""
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(__file__))
from palette import COLORS, OUTLINE, hexrgba  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Village")
N = 56
SS = 8
CREAM = COLORS["cream"]


def big():
    return Image.new("RGBA", (N * SS, N * SS), (0, 0, 0, 0))


def r(d, x0, y0, x1, y1, fill, rad=0):
    box = [x0 * SS, y0 * SS, x1 * SS - 1, y1 * SS - 1]
    if rad:
        d.rounded_rectangle(box, radius=rad * SS, fill=fill)
    else:
        d.rectangle(box, fill=fill)


def walls(d, wall):
    r(d, 5, 24, 51, 56, OUTLINE, 2)               # 벽 외곽선
    r(d, 6.5, 25.5, 49.5, 54.5, wall, 1.5)        # 벽
    r(d, 22, 36, 34, 54.5, OUTLINE, 1.5)          # 문 외곽선
    r(d, 23.3, 37.3, 32.7, 54.5, COLORS["soil"], 1)
    r(d, 30, 45, 31.5, 46.5, COLORS["gold"])      # 문고리
    for x in (10, 38):                            # 창 두 개 (노란 불빛)
        r(d, x, 32, x + 8, 40, OUTLINE, 1)
        r(d, x + 1.2, 33.2, x + 6.8, 38.8, COLORS["gold"], 0.6)
        r(d, x + 1.2, 33.2, x + 3.5, 35.5, CREAM)


def restaurant():
    img = big()
    d = ImageDraw.Draw(img)
    walls(d, CREAM)
    # 줄무늬 차양 (빨강·크림), 아래는 물결
    r(d, 1, 14, 55, 27, OUTLINE, 2)
    for i in range(9):
        col = hexrgba("ff5a6e") if i % 2 == 0 else CREAM
        r(d, 2.5 + i * 5.7, 15.5, 2.5 + (i + 1) * 5.7, 25.5, col)
    for i in range(9):
        cx = 2.5 + i * 5.7 + 2.85
        col = hexrgba("ff5a6e") if i % 2 == 0 else CREAM
        d.ellipse([(cx - 2.85) * SS, 23 * SS, (cx + 2.85) * SS, 28.5 * SS], fill=col)
    # 지붕 위 간판: 김 나는 그릇
    r(d, 17, 1, 39, 14.5, OUTLINE, 3)
    r(d, 18.5, 2.5, 37.5, 13, COLORS["sand"], 2.2)
    d.pieslice([21 * SS, 3 * SS, 35 * SS, 13 * SS], 0, 180, fill=hexrgba("ff8a3d"))   # 그릇
    r(d, 21, 7.6, 35, 8.6, OUTLINE)
    for x in (25, 28, 31):                                                         # 김
        d.arc([(x - 1) * SS, 3 * SS, (x + 1) * SS, 7 * SS], 90, 270, fill=CREAM, width=int(0.9 * SS))
    return img.resize((N, N), Image.LANCZOS)


def shop():
    img = big()
    d = ImageDraw.Draw(img)
    walls(d, hexrgba("f2e2c4"))
    # 파란 세모 지붕
    d.polygon([(0, 27 * SS), (28 * SS, 8 * SS), (56 * SS, 27 * SS)], fill=OUTLINE)
    d.polygon([(3.5 * SS, 25.5 * SS), (28 * SS, 10 * SS), (52.5 * SS, 25.5 * SS)], fill=hexrgba("4f8bff"))
    d.polygon([(12 * SS, 20 * SS), (28 * SS, 10 * SS), (31 * SS, 12 * SS)], fill=COLORS["aqua"])  # 지붕 빛
    # 간판: 별 지팡이
    r(d, 19, 0, 37, 11, OUTLINE, 3)
    r(d, 20.5, 1.5, 35.5, 9.5, COLORS["lilac"], 2)
    d.line([(23 * SS, 8.5 * SS), (31 * SS, 3.5 * SS)], fill=COLORS["soil"], width=int(1.4 * SS))
    d.regular_polygon((31.5 * SS, 3.6 * SS, 2.6 * SS), 5, fill=COLORS["gold"])
    return img.resize((N, N), Image.LANCZOS)


def canvas(w, h):
    return Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))


def lamp():
    w, h = 12, 40
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    r(d, 4.5, 12, 7.5, 40, OUTLINE)                   # 기둥 외곽선
    r(d, 5.3, 12, 6.7, 39.2, COLORS["night"])
    r(d, 3, 37, 9, 40, OUTLINE, 1)                    # 받침
    d.ellipse([0, 0, w * SS - 1, 13 * SS], fill=hexrgba("ffd84d", 70))   # 은은한 빛무리
    r(d, 2.5, 3, 9.5, 12.5, OUTLINE, 2)               # 등 외곽선
    r(d, 3.5, 4, 8.5, 11.5, COLORS["gold"], 1.5)
    r(d, 4.2, 4.8, 6, 7.5, COLORS["cream"], 0.6)      # 반짝
    d.polygon([(2 * SS, 3.5 * SS), (6 * SS, 0.6 * SS), (10 * SS, 3.5 * SS)], fill=OUTLINE)  # 모자
    return img.resize((w, h), Image.LANCZOS)


def flowers(variant):
    w, h = 16, 8
    petal = [COLORS["berry"], COLORS["gold"], COLORS["aqua"]][variant]
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    d.ellipse([0.5 * SS, 3 * SS, 15.5 * SS, 8 * SS - 1], fill=COLORS["leaf_dark"])   # 덤불
    d.ellipse([1.5 * SS, 3.6 * SS, 14.5 * SS, 7.2 * SS], fill=COLORS["leaf"])
    for cx, cy in ((4, 3.2), (8, 2.2), (12, 3.4)):
        d.ellipse([(cx - 2) * SS, (cy - 2) * SS, (cx + 2) * SS, (cy + 2) * SS], fill=OUTLINE)
        d.ellipse([(cx - 1.4) * SS, (cy - 1.4) * SS, (cx + 1.4) * SS, (cy + 1.4) * SS], fill=petal)
        d.ellipse([(cx - 0.6) * SS, (cy - 0.6) * SS, (cx + 0.6) * SS, (cy + 0.6) * SS], fill=COLORS["cream"])
    return img.resize((w, h), Image.LANCZOS)


def fence():
    w, h = 16, 10
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    r(d, 0, 3, 16, 5.5, OUTLINE)                      # 가로대 두 줄 (양 끝까지 → 이어 붙인다)
    r(d, 0, 3.6, 16, 4.9, COLORS["sand"])
    r(d, 0, 7, 16, 9.5, OUTLINE)
    r(d, 0, 7.6, 16, 8.9, COLORS["sand"])
    for x in (2, 10):                                 # 말뚝 두 개
        d.polygon([(x * SS, 2 * SS), (x * SS + 2 * SS, 0), (x * SS + 4 * SS, 2 * SS),
                   (x * SS + 4 * SS, 10 * SS), (x * SS, 10 * SS)], fill=OUTLINE)
        r(d, x + 0.8, 2, x + 3.2, 10, COLORS["soil"])
        r(d, x + 0.8, 2, x + 1.8, 10, COLORS["sand"])
    return img.resize((w, h), Image.LANCZOS)


def fountain():
    w, h = 40, 28
    water = hexrgba("4099ff")
    img = canvas(w, h)
    d = ImageDraw.Draw(img)
    # 물줄기 (가운데 위로, 양옆으로 떨어짐)
    d.arc([8 * SS, 2 * SS, 20 * SS, 22 * SS], 180, 270, fill=hexrgba("c8f4ff"), width=int(1.6 * SS))
    d.arc([20 * SS, 2 * SS, 32 * SS, 22 * SS], 270, 360, fill=hexrgba("c8f4ff"), width=int(1.6 * SS))
    r(d, 18.5, 3, 21.5, 16, hexrgba("c8f4ff"), 1.5)
    r(d, 17, 12, 23, 18, OUTLINE, 1)                  # 가운데 기둥
    r(d, 18, 12.5, 22, 18, COLORS["lilac"])
    # 돌 그릇
    r(d, 2, 16, 38, 28, OUTLINE, 3)
    r(d, 3.4, 17.4, 36.6, 26.6, hexrgba("c9c2d6"), 2.4)
    r(d, 4.5, 17.8, 35.5, 20.6, water, 1)             # 물 윗면
    r(d, 7, 18.4, 12, 19.4, COLORS["aqua"], 0.5)      # 물 반짝
    r(d, 26, 18.6, 30, 19.4, COLORS["aqua"], 0.5)
    for x in (9, 20, 31):                             # 돌 무늬
        r(d, x - 2.5, 23, x + 2.5, 24.2, hexrgba("9d93b3"), 0.6)
    return img.resize((w, h), Image.LANCZOS)


def generate():
    os.makedirs(OUT, exist_ok=True)
    restaurant().save(os.path.join(OUT, "Restaurant.png"))
    shop().save(os.path.join(OUT, "Shop.png"))
    lamp().save(os.path.join(OUT, "Lamp.png"))
    for i in range(3):
        flowers(i).save(os.path.join(OUT, f"Flowers{i}.png"))
    fence().save(os.path.join(OUT, "Fence.png"))
    fountain().save(os.path.join(OUT, "Fountain.png"))


if __name__ == "__main__":
    generate()
    print("done")
