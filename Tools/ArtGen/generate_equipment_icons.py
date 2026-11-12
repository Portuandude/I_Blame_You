"""I Blame You! 장비 아이콘 생성기 (귀엽지만 화려하게, 유물 아이콘과 같은 24x24 규칙).

부위별 기본 모양(로브·지팡이·신발) 3개를 장비마다 색·장식만 바꿔 그린다. 파일 이름 = EquipmentId 이름.
일시정지 패널 장비 칸·비교 카드·상점이 이름 앞에 그린다 (Docs/EquipmentDesign.md 8절).

  Assets/Resources/UI/HUD/Equipment/<EquipmentId>.png  24x24

    python3 Tools/ArtGen/generate_equipment_icons.py [미리보기.png]
"""
import os
import sys

from PIL import ImageDraw

sys.path.insert(0, os.path.dirname(__file__))
from palette import COLORS, ELEMENTS, OUTLINE, hexrgba  # noqa: E402
from generate_hud import big, small, star_points  # noqa: E402
from generate_relic_icons import N, P, E, outlined_poly, outlined_ellipse, shine, preview  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "UI", "HUD", "Equipment")
CREAM = COLORS["cream"]
GOLD = COLORS["gold"]


def robe(d, body, trim, gem=None):
    """두건 달린 로브: 몸 + 두건 + 밑단 장식 + 가운데 보석."""
    outlined_poly(d, [(12, 2.5), (16.5, 5), (20, 21), (4, 21), (7.5, 5)], hexrgba(body))
    d.polygon(P((12, 2.5), (16.5, 5), (12, 9), (7.5, 5)), fill=hexrgba(trim))     # 두건
    d.rectangle(E(5, 18.5, 19, 20.2), fill=hexrgba(trim))                           # 밑단
    d.line(P((12, 9), (12, 18.5)), fill=OUTLINE, width=2)
    if gem:
        outlined_ellipse(d, 10.5, 9.2, 13.5, 12.2, hexrgba(gem), 0.8)
    shine(d, 8.4, 11.5, 1.1)


def staff(d, wood, orb, glow):
    """대각선 지팡이 + 끝의 구슬."""
    outlined_poly(d, [(4, 21.5), (5.8, 22.5), (17, 9.5), (15.2, 8.2)], hexrgba(wood), 1.0)
    outlined_ellipse(d, 13, 2.5, 21, 10.5, hexrgba(orb), 1.2)
    d.ellipse(E(15, 4.5, 19, 8.5), fill=hexrgba(glow))
    shine(d, 15.4, 4.6, 1.0)


def shoe(d, body, sole, deco=None):
    """옆에서 본 신발 한 짝."""
    outlined_poly(d, [(6, 6), (12.5, 6), (12.5, 13.5), (20, 15), (20.5, 19.5), (5, 19.5)], hexrgba(body))
    d.rectangle(E(5, 18, 20.5, 19.5), fill=hexrgba(sole))
    d.rectangle(E(6, 6, 12.5, 8), fill=CREAM)
    if deco:
        deco(d)
    shine(d, 8.5, 10.5, 1.1)


def fire(i):
    return ELEMENTS["fire"][i]


def water(i):
    return ELEMENTS["water"][i]


def wind(i):
    return ELEMENTS["wind"][i]


def earth(i):
    return ELEMENTS["earth"][i]


def feather(d):
    outlined_poly(d, [(13, 9), (21, 4.5), (19.5, 8.5), (22, 9), (14, 12.5)], CREAM, 1.0)


def mud(d):
    for x, y in ((9, 15.5), (14.5, 16.5), (18, 17)):
        d.ellipse(E(x - 1.2, y - 1, x + 1.2, y + 1), fill=hexrgba(earth(0)))


def bolt(d):
    outlined_poly(d, [(16, 3), (13, 9.5), (16, 9.5), (13.5, 14.5), (20.5, 7.5), (17, 7.5), (19, 3)], GOLD, 0.9)


ICONS = {
    "ApprenticeRobe": lambda d: robe(d, "7f8fd6", "c8d0ea"),
    "MossRobe": lambda d: robe(d, "4f9a4a", earth(1), "8cffb2"),
    "MistRobe": lambda d: robe(d, water(1), water(2), "ffffff"),
    "FlameRobe": lambda d: robe(d, fire(0), fire(2), fire(1)),
    "WoodStaff": lambda d: staff(d, earth(1), earth(0), earth(2)),
    "EmberStaff": lambda d: staff(d, earth(0), fire(1), fire(2)),
    "DropletStaff": lambda d: staff(d, earth(0), water(1), water(2)),
    "WhirlStaff": lambda d: staff(d, earth(0), wind(1), wind(2)),
    "ClothShoes": lambda d: shoe(d, "c98a5c", earth(0)),
    "FeatherShoes": lambda d: shoe(d, wind(0), earth(0), feather),
    "MudBoots": lambda d: shoe(d, earth(1), earth(0), mud),
    "BoltShoes": lambda d: shoe(d, "5a4ad8", "2a2150", bolt),
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
