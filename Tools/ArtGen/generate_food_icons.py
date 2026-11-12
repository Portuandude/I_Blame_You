"""I Blame You! 식당 음식 아이콘 생성기 (귀엽지만 화려하게, 유물 아이콘과 같은 24x24 규칙).

파일 이름 = FoodId 이름 (Player/Equipment/Data/VillageFood.cs). 식당 창 줄 왼쪽에 그리고(UI/SkillHUD.Village.cs),
산 음식은 플레이어 머리 위에 잠깐 떠오른다 (VfxPlayer.FoodGlint → MedalGlintFx).

  Assets/Resources/UI/HUD/Food/<FoodId>.png  24x24

    python3 Tools/ArtGen/generate_food_icons.py [미리보기.png]
"""
import os
import sys

from PIL import ImageDraw

sys.path.insert(0, os.path.dirname(__file__))
from palette import COLORS, OUTLINE, hexrgba  # noqa: E402
from generate_hud import big, small  # noqa: E402
from generate_relic_icons import N, P, E, outlined_poly, outlined_ellipse, shine, preview  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "UI", "HUD", "Food")
CREAM = COLORS["cream"]
GOLD = COLORS["gold"]


def bowl(d, soup, rim="fff4d6"):
    """둥근 그릇 + 국물 윗면."""
    d.pieslice(E(2.6, 4.6, 21.4, 23.4), 0, 180, fill=OUTLINE)
    d.pieslice(E(4, 6, 20, 22), 0, 180, fill=hexrgba(rim))
    d.ellipse(E(3.2, 11.6, 20.8, 16.4), fill=OUTLINE)
    d.ellipse(E(4.4, 12.4, 19.6, 15.6), fill=hexrgba(soup))


def steam(d, xs=(9, 12, 15)):
    for x in xs:
        d.arc(E(x - 1.2, 4, x + 1.2, 10), 90, 270, fill=CREAM, width=int(0.9 * 8))


def warm_soup(d):
    steam(d)
    bowl(d, "ff8a3d")
    d.ellipse(E(8, 12.8, 10, 14.2), fill=hexrgba("4fc24a"))   # 파
    d.ellipse(E(13, 13, 15, 14.4), fill=hexrgba("fff4d6"))
    shine(d, 6.6, 17.5, 0.9)


def star_tea(d):
    """찻잔 + 손잡이 + 위에 별."""
    d.arc(E(15, 11, 22, 18), 270, 90, fill=OUTLINE, width=int(2.4 * 8))
    d.arc(E(15.8, 11.8, 21.2, 17.2), 270, 90, fill=hexrgba("a68cf0"), width=int(1.0 * 8))
    outlined_poly(d, [(4, 10), (18, 10), (16.5, 20), (5.5, 20)], hexrgba("a68cf0"), 1.3)
    d.rectangle(E(5, 10.6, 17, 12.4), fill=hexrgba("7ff6ff"))
    d.regular_polygon((11 * 8, 5.2 * 8, 3.6 * 8), 5, fill=OUTLINE)
    d.regular_polygon((11 * 8, 5.2 * 8, 2.6 * 8), 5, fill=GOLD)
    shine(d, 7, 15, 0.9)


def mushroom_stew(d):
    """냄비 + 빨간 버섯 갓."""
    outlined_poly(d, [(3.5, 11), (20.5, 11), (19, 21), (5, 21)], hexrgba("5a46a8"), 1.3)
    d.rectangle(E(1.6, 11, 4.6, 13), fill=OUTLINE)
    d.rectangle(E(19.4, 11, 22.4, 13), fill=OUTLINE)
    d.rectangle(E(4.6, 11.6, 19.4, 13.2), fill=hexrgba("bf8c4d"))
    d.rectangle(E(10.4, 7, 13.6, 12), fill=OUTLINE)
    d.rectangle(E(11.2, 7.5, 12.8, 12), fill=CREAM)
    d.pieslice(E(6.4, 2, 17.6, 12), 180, 360, fill=OUTLINE)
    d.pieslice(E(7.6, 3.2, 16.4, 11), 180, 360, fill=hexrgba("ff5a6e"))
    for x, y in ((10, 5), (13.6, 5.6)):
        d.ellipse(E(x - 0.9, y - 0.9, x + 0.9, y + 0.9), fill=CREAM)
    shine(d, 7, 16, 0.9)


def honey_dumpling(d):
    """꼬치에 꽂은 경단 세 알 + 꿀 방울."""
    d.line(P((4, 21), (20, 3)), fill=OUTLINE, width=int(2.4 * 8))
    d.line(P((4, 21), (20, 3)), fill=hexrgba("e8b878"), width=int(1.0 * 8))
    for cx, cy, col in ((7.5, 16.5, "ffb3d4"), (12, 11.5, "fff4d6"), (16.5, 6.5, "b2f26a")):
        outlined_ellipse(d, cx - 3.2, cy - 3.2, cx + 3.2, cy + 3.2, hexrgba(col), 1.2)
        d.ellipse(E(cx - 2.6, cy - 3.2, cx + 1.4, cy - 1), fill=GOLD)   # 꿀
    d.ellipse(E(9.4, 19.4, 11.2, 21.8), fill=GOLD)                        # 떨어지는 꿀 방울
    shine(d, 11, 10.4, 0.8)


def lunchbox(d):
    """보자기에 싼 도시락 (위 매듭)."""
    outlined_poly(d, [(3, 10), (21, 10), (21, 21), (3, 21)], hexrgba("ff6fae"), 1.3)
    d.rectangle(E(3, 14.4, 21, 16), fill=hexrgba("ffb3d4"))
    for x in (6, 11, 16):
        d.ellipse(E(x, 17.4, x + 2, 19.4), fill=CREAM)
    outlined_ellipse(d, 9, 5, 15, 10.5, hexrgba("ff6fae"), 1.1)          # 매듭
    outlined_poly(d, [(8, 9), (4, 5), (6, 3.5), (10, 8)], hexrgba("ff6fae"), 0.9)
    outlined_poly(d, [(16, 9), (20, 5), (18, 3.5), (14, 8)], hexrgba("ff6fae"), 0.9)
    shine(d, 6, 12.4, 0.9)


ICONS = {
    "WarmSoup": warm_soup,
    "StarTea": star_tea,
    "MushroomStew": mushroom_stew,
    "HoneyDumpling": honey_dumpling,
    "Lunchbox": lunchbox,
}


def generate():
    os.makedirs(OUT, exist_ok=True)
    result = {}
    for name, draw_fn in ICONS.items():
        img = big(N, N)
        draw_fn(ImageDraw.Draw(img))
        result[name] = small(img, N, N)
        result[name].save(os.path.join(OUT, f"{name}.png"))
    return result


if __name__ == "__main__":
    icons = generate()
    print(f"{len(icons)}개 → {OUT}")
    if len(sys.argv) > 1:
        preview(icons, sys.argv[1])
