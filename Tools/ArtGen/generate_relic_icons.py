"""I Blame You! 유물 아이콘 15종 생성기 (귀엽지만 화려하게).

유물마다 24x24 아이콘: 진한 외곽선 + 채도 높은 몸 + 크림 반짝임. 유물 상자 카드·HUD 유물 목록·일시정지 요약이
이름 앞에 그린다 (SkillHUD.RelicIcons.cs). 파일 이름 = RelicId 이름.

  Assets/Resources/UI/HUD/Relics/<RelicId>.png  24x24

    python3 Tools/ArtGen/generate_relic_icons.py [미리보기.png]
"""
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(__file__))
from palette import COLORS, ELEMENTS, OUTLINE, hexrgba  # noqa: E402
from generate_hud import SS, big, small, heart_shape, star_points  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "UI", "HUD", "Relics")
N = 24
CREAM = COLORS["cream"]
GOLD = COLORS["gold"]
AQUA = COLORS["aqua"]
BERRY = COLORS["berry"]
LILAC = COLORS["lilac"]


def P(*pts):
    return [(x * SS, y * SS) for x, y in pts]


def E(x0, y0, x1, y1):
    return [x0 * SS, y0 * SS, x1 * SS, y1 * SS]


def outlined_poly(d, pts, fill, grow=1.4):
    """외곽선: 가운데에서 바깥으로 조금 키운 다각형을 먼저 진하게."""
    cx = sum(p[0] for p in pts) / len(pts)
    cy = sum(p[1] for p in pts) / len(pts)
    big_pts = []
    for x, y in pts:
        dx, dy = x - cx, y - cy
        length = max((dx * dx + dy * dy) ** 0.5, 0.01)
        big_pts.append((x + dx / length * grow, y + dy / length * grow))
    d.polygon(P(*big_pts), fill=OUTLINE)
    d.polygon(P(*pts), fill=fill)


def outlined_ellipse(d, x0, y0, x1, y1, fill, w=1.3):
    d.ellipse(E(x0 - w, y0 - w, x1 + w, y1 + w), fill=OUTLINE)
    d.ellipse(E(x0, y0, x1, y1), fill=fill)


def shine(d, x, y, r=1.6):
    d.ellipse(E(x - r, y - r, x + r, y + r), fill=CREAM)


def mana_crystal(d):
    outlined_poly(d, [(12, 2.5), (19, 10), (12, 21.5), (5, 10)], hexrgba("4099ff"))
    d.polygon(P((12, 2.5), (19, 10), (12, 10)), fill=AQUA)
    d.polygon(P((12, 10), (19, 10), (12, 21.5)), fill=hexrgba("1f5fd1"))
    shine(d, 9.5, 8, 1.5)


def iron_cloak(d):
    outlined_poly(d, [(12, 3), (16, 5.5), (19.5, 20.5), (4.5, 20.5), (8, 5.5)], hexrgba("8a94b8"))
    d.polygon(P((12, 3), (16, 5.5), (12, 9), (8, 5.5)), fill=hexrgba("c8d0ea"))  # 두건
    d.line(P((12, 9), (12, 20)), fill=hexrgba("5a6488"), width=int(1.2 * SS))
    outlined_ellipse(d, 10.6, 8.6, 13.4, 11.4, GOLD, 0.8)                      # 단추
    shine(d, 8.5, 12, 1.2)


def swift_boots(d):
    outlined_poly(d, [(7, 4), (13, 4), (13, 14), (19.5, 15), (19.5, 20), (6, 20)], hexrgba("4fc24a"))
    d.rectangle(E(7, 4, 13, 6.5), fill=COLORS["leaf_light"])
    # 날개
    outlined_poly(d, [(13, 7), (21, 4), (19, 8), (22, 8.5), (14, 12)], CREAM, 1.0)
    shine(d, 9, 9.5, 1.3)


def wrath_charm(d):
    fire = ELEMENTS["fire"]
    outlined_poly(d, [(12, 2), (15, 7), (18.5, 6), (18.5, 14), (12, 21.5), (5.5, 14), (5.5, 6), (9, 7)], hexrgba(fire[1]))
    d.polygon(P((12, 8), (15.5, 13), (12, 19), (8.5, 13)), fill=hexrgba(fire[2]))
    # 화난 눈썹
    d.line(P((8, 10), (11, 11.5)), fill=OUTLINE, width=int(1.3 * SS))
    d.line(P((16, 10), (13, 11.5)), fill=OUTLINE, width=int(1.3 * SS))
    shine(d, 8, 6.5, 1.2)


def life_seed(d):
    outlined_ellipse(d, 6, 11, 18, 21.5, COLORS["soil"])
    d.ellipse(E(7.5, 12, 13, 16), fill=COLORS["sand"])
    # 새싹
    d.line(P((12, 11.5), (12, 6)), fill=OUTLINE, width=int(2.4 * SS))
    d.line(P((12, 11.5), (12, 6)), fill=COLORS["leaf"], width=int(1.2 * SS))
    outlined_poly(d, [(12, 6.5), (6, 2.5), (8, 7.5)], COLORS["leaf_light"], 1.0)
    outlined_poly(d, [(12, 6.5), (18, 2.5), (16, 7.5)], COLORS["leaf"], 1.0)
    shine(d, 9, 15, 1.2)


def spring_ring(d):
    d.ellipse(E(3.5, 7.5, 20.5, 22), fill=OUTLINE)
    d.ellipse(E(5, 9, 19, 20.5), fill=GOLD)
    d.ellipse(E(7.5, 11.5, 16.5, 18.5), fill=OUTLINE)
    d.ellipse(E(8.8, 12.8, 15.2, 17.2), fill=(0, 0, 0, 0))
    # 물방울 보석
    outlined_poly(d, [(12, 1.5), (15.5, 7), (12, 10), (8.5, 7)], hexrgba("4099ff"), 1.1)
    shine(d, 11, 5.5, 1.1)
    shine(d, 7, 13, 1.0)


def hawk_eye(d):
    outlined_poly(d, [(2, 12), (7, 6.5), (12, 5), (17, 6.5), (22, 12), (17, 17.5), (12, 19), (7, 17.5)], CREAM)
    outlined_ellipse(d, 7.5, 7.5, 16.5, 16.5, GOLD, 0.6)
    d.ellipse(E(9.8, 9.8, 14.2, 14.2), fill=OUTLINE)
    shine(d, 10.3, 9.8, 1.3)


def feather(d, body, vein):
    outlined_poly(d, [(18.5, 2.5), (20, 6), (15, 14), (7, 19.5), (5, 18), (9.5, 10)], body)
    d.line(P((19, 3.5), (4, 21)), fill=OUTLINE, width=int(1.6 * SS))
    d.line(P((19, 3.5), (5.5, 19.5)), fill=vein, width=int(0.7 * SS))


def featherfall(d):
    feather(d, LILAC, CREAM)
    shine(d, 14, 6.5, 1.3)


def vampire_fang(d):
    outlined_poly(d, [(6, 3), (18, 3), (16, 11), (12, 21), (8, 11)], CREAM)
    d.polygon(P((6, 3), (18, 3), (17.3, 5.5), (6.7, 5.5)), fill=hexrgba("ff9ccb"))  # 잇몸
    # 피 한 방울
    outlined_poly(d, [(16.5, 13), (19, 17), (16.5, 19.5), (14, 17)], hexrgba("e0284f"), 1.0)
    shine(d, 9.5, 8, 1.2)


def focus_bead(d):
    arc = ELEMENTS["arcane"]
    outlined_ellipse(d, 4, 4, 20, 20, hexrgba(arc[0]))
    d.ellipse(E(6.5, 6.5, 17.5, 17.5), fill=hexrgba(arc[1]))
    d.polygon(star_points(12, 12.5, 4.5, 2.0), fill=hexrgba(arc[2]))
    shine(d, 8.5, 8, 1.6)


def shadow_veil(d):
    # 보라 베일 + 초승달
    outlined_poly(d, [(12, 2.5), (18, 6), (20.5, 21), (16, 18), (12, 21.5), (8, 18), (3.5, 21), (6, 6)], hexrgba("5a46a8"))
    d.polygon(P((12, 2.5), (18, 6), (12, 9), (6, 6)), fill=LILAC)
    d.ellipse(E(8, 9.5, 16, 17.5), fill=GOLD)
    d.ellipse(E(10, 8.8, 18, 16.8), fill=hexrgba("5a46a8"))   # 초승달
    shine(d, 7.5, 7, 1.1)


def blood_pact(d):
    heart_shape(d, 11, 12.5, 9.5, OUTLINE)
    heart_shape(d, 11, 12.3, 7.9, hexrgba("e0284f"))
    heart_shape(d, 11, 13.4, 5.2, BERRY)
    # 마나 별 (체력 → 마나)
    d.polygon(star_points(18, 18, 5.5, 2.6), fill=OUTLINE)
    d.polygon(star_points(18, 18, 4.0, 1.9), fill=AQUA)
    shine(d, 7, 7.5, 1.4)


def phoenix_feather(d):
    fire = ELEMENTS["fire"]
    feather(d, hexrgba(fire[1]), hexrgba(fire[2]))
    # 불꽃 끝
    outlined_poly(d, [(20, 1.5), (22, 4.5), (19.5, 7.5), (17.5, 4.5)], GOLD, 0.9)
    shine(d, 13.5, 7.5, 1.3)


def hourglass(d):
    # 금 틀 + 하늘색 유리 + 아래로 떨어지는 금모래, 옆에 되감기 화살표
    d.rectangle(E(4.5, 1.5, 17.5, 4.5), fill=OUTLINE)
    d.rectangle(E(5.5, 2.3, 16.5, 3.7), fill=GOLD)
    d.rectangle(E(4.5, 19.5, 17.5, 22.5), fill=OUTLINE)
    d.rectangle(E(5.5, 20.3, 16.5, 21.7), fill=GOLD)
    outlined_poly(d, [(6, 4.5), (16, 4.5), (12, 12), (16, 19.5), (6, 19.5), (10, 12)], hexrgba("bfe8ff"), 1.0)
    d.polygon(P((8, 6), (14, 6), (11, 10)), fill=GOLD)       # 위 모래
    d.polygon(P((11, 15), (15, 19.3), (7, 19.3)), fill=GOLD)  # 아래 모래
    d.line(P((11, 11), (11, 15)), fill=GOLD, width=int(0.8 * SS))
    # 되감기 화살 (오른쪽 위, 반시계)
    d.arc(E(15, 7, 22.5, 14.5), 200, 110, fill=OUTLINE, width=int(2.4 * SS))
    d.arc(E(15, 7, 22.5, 14.5), 200, 110, fill=AQUA, width=int(1.2 * SS))
    outlined_poly(d, [(14, 9), (17.5, 9.5), (15, 12.5)], AQUA, 0.8)
    shine(d, 8.5, 15.5, 1.0)


def twin_ring(d):
    # 겹친 반지 두 개: 하늘(A)·주황(B) 보석
    for cx, gem in ((8.5, "5fd0ff"), (15.5, "ff9a3c")):
        d.ellipse(E(cx - 6.5, 8, cx + 6.5, 21), fill=OUTLINE)
        d.ellipse(E(cx - 5.2, 9.3, cx + 5.2, 19.7), fill=GOLD)
        d.ellipse(E(cx - 3.3, 11.2, cx + 3.3, 17.8), fill=OUTLINE)
        d.ellipse(E(cx - 2.2, 12.3, cx + 2.2, 16.7), fill=(0, 0, 0, 0))
        outlined_poly(d, [(cx, 2), (cx + 3, 5.5), (cx, 9), (cx - 3, 5.5)], hexrgba(gem), 1.0)
        shine(d, cx - 0.8, 4.6, 0.9)
    d.polygon(star_points(12, 12.5, 2.6, 1.1), fill=CREAM)


def compass(d):
    # 둥근 금테 나침반: 크림 판 위에 빨강(북)·하늘(남) 바늘, 위에 고리
    d.ellipse(E(9.5, 0.8, 14.5, 5.8), fill=OUTLINE)
    d.ellipse(E(10.7, 2.0, 13.3, 4.6), fill=GOLD)
    outlined_ellipse(d, 3, 4, 21, 22, GOLD)
    d.ellipse(E(5.2, 6.2, 18.8, 19.8), fill=CREAM)
    for x, y in ((12, 7.3), (12, 18.7), (6.3, 13), (17.7, 13)):  # 방위 눈금
        d.ellipse(E(x - 0.7, y - 0.7, x + 0.7, y + 0.7), fill=OUTLINE)
    outlined_poly(d, [(12, 7.6), (14.2, 13), (9.8, 13)], hexrgba("ff4f6a"), 0.8)   # 북 바늘
    outlined_poly(d, [(12, 18.4), (9.8, 13), (14.2, 13)], AQUA, 0.8)              # 남 바늘
    d.ellipse(E(11, 12, 13, 14), fill=OUTLINE)
    shine(d, 7.5, 8.5, 1.0)
    d.polygon(star_points(19.5, 4.5, 2.4, 1.0), fill=CREAM)                       # 반짝


ICONS = {
    "ManaCrystal": mana_crystal,
    "IronCloak": iron_cloak,
    "SwiftBoots": swift_boots,
    "WrathCharm": wrath_charm,
    "LifeSeed": life_seed,
    "SpringRing": spring_ring,
    "HawkEye": hawk_eye,
    "Featherfall": featherfall,
    "VampireFang": vampire_fang,
    "FocusBead": focus_bead,
    "ShadowVeil": shadow_veil,
    "BloodPact": blood_pact,
    "PhoenixFeather": phoenix_feather,
    "Hourglass": hourglass,
    "TwinRing": twin_ring,
    "Compass": compass,
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


def preview(result, path):
    scale = 4
    sheet = Image.new("RGBA", (len(result) * (N + 4) * scale, (N + 4) * scale), hexrgba("2a2150"))
    for i, img in enumerate(result.values()):
        sheet.alpha_composite(img.resize((N * scale, N * scale), Image.NEAREST), (i * (N + 4) * scale + 2 * scale, 2 * scale))
    sheet.save(path)


if __name__ == "__main__":
    icons = generate()
    print(f"{len(icons)}개 → {OUT}")
    if len(sys.argv) > 1:
        preview(icons, sys.argv[1])
