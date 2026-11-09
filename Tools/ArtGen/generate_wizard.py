"""플레이어 마법사 프레임 생성기 (귀엽지만 화려하게, Docs/ArtStyle.md).

사람이 그린 마법사 Idle(갈색 후드, 어두운 얼굴에 빛나는 노란 눈, 회색 장갑, 허리띠, 보라 수정 지팡이)의 톤을
generate_sprites.py의 Canvas(굵은 외곽선 + 3단 셀 셰이딩)로 다시 그려 모든 동작을 같은 몸으로 맞춘다.
파일 이름·프레임 수·캔버스(300x256)·발 위치(y=193, 중앙 피벗)는 예전 그대로라 코드는 바뀌지 않는다.
Unity에서 Tools > I Blame You > Generate Sprites And Animations 를 다시 실행하면 클립에 반영된다.

    python3 Tools/ArtGen/generate_wizard.py [확인용_시트.png]
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from generate_sprites import OUTLINE, SS, Canvas, rgba, shade  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Art", "Sprites", "Characters", "Wizard")
W, H = 300, 256
FOOT_X, FOOT_Y = 150, 256 - 193  # 발 = 위에서 193px

ROBE = rgba("8f5a2e")       # palette soil
ROBE_DARK = rgba("5e3a1f")
ROBE_LIGHT = rgba("b8783e")
FACE = rgba("120d12")
EYE = rgba("ffe14a")
EYE_GLOW = rgba("ffd84d", 120)
GLOVE = rgba("c9c6b4")
BELT = rgba("4a2c18")
BUCKLE = rgba("a59c8c")
WOOD = rgba("6e4b2e")
CRYSTAL = rgba("a68cf0")    # palette lilac
CRYSTAL_LIGHT = rgba("e6dcff")
CRYSTAL_CORE = rgba("7ff6ff")  # palette aqua
AQUA = rgba("7ff6ff")
FEET = rgba("2e2630")


class Pose:
    def __init__(self, **kw):
        self.bob = 0.0          # 몸 전체 위아래
        self.lean = 0.0         # 머리를 앞(+x)으로 미는 정도
        self.stretch = 1.0      # 세로 늘림(>1) / 찌그러짐(<1)
        self.foot_f = (8.0, 0.0)    # 앞발 (x, 들림)
        self.foot_b = (-12.0, 0.0)  # 뒷발
        self.staff = 84.0       # 지팡이 각도 (도, 0 = 오른쪽, 90 = 위)
        self.staff_hand = (40.0, 46.0)  # 지팡이 쥔 손 위치 (몸 기준)
        self.back_hand = (-30.0, 34.0)
        self.glow = 1.0         # 수정 빛 0~1
        self.flash = 0.0        # 공격 순간 수정 번쩍임 0~1
        self.tip = 0.0          # 후드 끝자락 펄럭임 (+ 위로, - 아래로)
        self.blink = False
        self.__dict__.update(kw)


def draw_wizard(pose):
    c = Canvas(W, H, foot_x=FOOT_X, foot_y=FOOT_Y, outline=3)
    st = pose.stretch
    by = pose.bob

    def Y(y):  # 발 기준 높이를 늘림/찌그러짐에 맞춰 바꿈
        return y * st + by

    def X(x, y):  # 높이가 높을수록 lean만큼 앞으로
        return x / (st ** 0.5) + pose.lean * (y / 100.0)

    # 지팡이 (몸 뒤) + 수정
    hx, hy = pose.staff_hand
    hand = (X(hx, hy), Y(hy))
    a = math.radians(pose.staff)
    top = (hand[0] + math.cos(a) * 62, hand[1] + math.sin(a) * 62)
    bottom = (hand[0] - math.cos(a) * 40, hand[1] - math.sin(a) * 40)
    c.limb(bottom, top, 5, WOOD)
    c.limb(bottom, top, 1.6, shade(WOOD, 1.3), outline=False)

    # 발 (로브 끝자락 아래 작은 검은 발)
    for fx, lift in (pose.foot_b, pose.foot_f):
        c.ellipse(X(fx, 4) + 3, 8.5 + lift + by, 9, 5.5, FEET, highlight=False)

    # 몸 (로브): 둥근 종 모양
    body_c = (X(0, 32), Y(30))
    c.ellipse(body_c[0], body_c[1], 28, 28 * st ** 0.5, ROBE)
    c.ellipse(body_c[0] + 8, body_c[1] - 10, 14, 10, ROBE_DARK, outline=False, highlight=False)
    # 허리띠 + 버클
    belt_y = Y(24)
    c.limb((X(-26, 24), belt_y + 1), (X(27, 24), belt_y - 1), 7, BELT, outline=False)
    bx = X(4, 24)
    c.polygon([(bx - 7, belt_y + 6), (bx + 7, belt_y + 6), (bx + 7, belt_y - 6), (bx - 7, belt_y - 6)], BUCKLE)
    c.polygon([(bx - 3, belt_y + 2.5), (bx + 3, belt_y + 2.5), (bx + 3, belt_y - 2.5), (bx - 3, belt_y - 2.5)], BELT, outline=False)

    # 뒷손 (장갑)
    bh = (X(pose.back_hand[0], pose.back_hand[1]), Y(pose.back_hand[1]))
    c.limb((X(-16, 44), Y(44)), bh, 9, ROBE_DARK)
    c.ellipse(bh[0], bh[1], 10, 8.5, GLOVE)

    # 후드: 뒤로 늘어진 끝자락 → 큰 머리
    head = (X(4, 92), Y(92))
    tip_y = head[1] + 26 + pose.tip
    # 후드 꼭지: 머리 왼쪽 위에서 뒤로 솟은 뾰족한 끝 (원본 그림처럼)
    c.polygon([(head[0] - 10, head[1] + 38), (head[0] - 40, tip_y + 14), (head[0] - 36, head[1] + 8)], ROBE)
    c.ellipse(head[0], head[1], 40, 42 * st ** 0.3, ROBE)
    c.ellipse(head[0] - 12, head[1] + 20, 16, 10, ROBE_LIGHT, outline=False, highlight=False)
    # 얼굴 (후드 속 어둠) — 오른쪽으로 치우침
    face = (head[0] + 9, head[1] - 7)
    c.ellipse(face[0], face[1], 29, 29, FACE, outline=False, highlight=False)
    # 후드 테두리 빛 (오른쪽 아래, aqua)
    c.polyline(c.arc_points(head[0], head[1], 38, -65, 5, 10), 1.6, rgba("7ff6ff", 170), outline=False)

    # 빛나는 노란 눈
    eye_h = 1.6 if pose.blink else 7.5
    for ex in (face[0] - 10, face[0] + 13):
        if not pose.blink:
            c.ellipse(ex, face[1] + 2, 9, 10, EYE_GLOW, outline=False, highlight=False)
        c.ellipse(ex, face[1] + 2, 6.2, eye_h, EYE, outline=False, highlight=False)
        if not pose.blink:
            c.ellipse(ex - 1.8, face[1] + 4.5, 1.6, 1.8, rgba("fff4d6"), outline=False, highlight=False)

    # 앞손 (지팡이 쥔 장갑)
    c.limb((X(14, 48), Y(48)), hand, 9, ROBE)
    c.ellipse(hand[0], hand[1], 10, 9, GLOVE)
    c.limb((hand[0] - 6, hand[1] + 2), (hand[0] + 6, hand[1] + 2), 1.2, shade(GLOVE, 0.7), outline=False)

    img = c.result()
    return add_crystal(img, top, pose.glow, pose.flash, a)


def to_px(x, y):
    return FOOT_X + x, H - FOOT_Y - y


def add_crystal(img, top, glow, flash, angle):
    """지팡이 끝 보라 수정 + 빛 (빛만 블러해서 깐다)."""
    cx, cy = to_px(*top)
    ux, uy = math.cos(angle), -math.sin(angle)  # 화면 좌표의 지팡이 방향
    px_, py_ = -uy, ux

    def pt(along, side):
        return (cx + ux * along + px_ * side, cy + uy * along + py_ * side)

    light = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(light)
    strength = max(glow, flash)
    if strength > 0:
        r = 14 + 10 * strength + 6 * flash
        ld.ellipse([cx + ux * 8 - r, cy + uy * 8 - r, cx + ux * 8 + r, cy + uy * 8 + r],
                   fill=(170, 185, 255, int(150 * strength)))
        light = light.filter(ImageFilter.GaussianBlur(7))
    out = Image.alpha_composite(light, img)
    d = ImageDraw.Draw(out)
    shape = [pt(-2, -9), pt(10, -11), pt(24, -4), pt(22, 7), pt(8, 10), pt(-2, 7)]
    d.polygon([pt(-5, -12), pt(11, -14), pt(28, -5), pt(25, 10), pt(8, 13), pt(-5, 10)], fill=OUTLINE)
    d.polygon(shape, fill=CRYSTAL)
    d.polygon([pt(4, -6), pt(12, -8), pt(18, -2), pt(10, 2)], fill=CRYSTAL_LIGHT)
    if strength > 0:
        core = tuple(int(CRYSTAL[i] + (CRYSTAL_CORE[i] - CRYSTAL[i]) * strength) for i in range(3)) + (255,)
        d.polygon([pt(8, 1), pt(16, 0), pt(18, 5), pt(10, 6)], fill=core)
    if flash > 0.5:  # 4갈래 반짝임
        sx, sy = pt(12, 0)
        rr = 16 * flash
        d.polygon([(sx, sy - rr), (sx + rr * 0.22, sy - rr * 0.22), (sx + rr, sy), (sx + rr * 0.22, sy + rr * 0.22),
                   (sx, sy + rr), (sx - rr * 0.22, sy + rr * 0.22), (sx - rr, sy), (sx - rr * 0.22, sy - rr * 0.22)],
                  fill=(255, 244, 214, 255))
    return out


# ---- 동작별 포즈 ----

def idle_poses():
    # 0 → 6 으로 수정 빛이 켜진다 (GameAssetSetup이 왕복 재생). 숨쉬기 출렁임은 아주 작게.
    poses = []
    for i in range(7):
        t = i / 6
        poses.append(Pose(glow=t, bob=t * 1.5, stretch=1.0 + 0.012 * t, tip=-2 + 3 * t, blink=(i == 2)))
    return poses


def walk_poses():
    poses = []
    for i in range(8):
        ph = 2 * math.pi * i / 8
        s = math.sin(ph)
        poses.append(Pose(bob=abs(math.cos(ph)) * 2.5, lean=2.0,
                          foot_f=(6 + 10 * s, max(0, s) * 4), foot_b=(-10 - 10 * s, max(0, -s) * 4),
                          staff=84 + 4 * s, staff_hand=(40 + 2 * s, 46 + abs(s) * 2),
                          back_hand=(-30 - 6 * s, 34 + abs(s) * 2), tip=-3 * abs(s), glow=0.6))
    return poses


def run_poses():
    poses = []
    for i in range(8):
        ph = 2 * math.pi * i / 8
        s = math.sin(ph)
        poses.append(Pose(bob=abs(math.cos(ph)) * 4, lean=12.0, stretch=1.0 - 0.03 * abs(math.cos(ph)),
                          foot_f=(8 + 16 * s, max(0, s) * 7), foot_b=(-12 - 16 * s, max(0, -s) * 7),
                          staff=150 - 6 * s, staff_hand=(30, 44), back_hand=(-38 - 4 * s, 40),
                          tip=10 + 4 * s, glow=0.8))
    return poses


def jump_poses():
    rise = Pose(stretch=1.12, foot_f=(6, 8), foot_b=(-8, 10), staff=100, staff_hand=(40, 54),
                back_hand=(-34, 52), tip=-14, glow=0.8)
    fall = Pose(stretch=0.95, foot_f=(10, 0), foot_b=(-14, 0), staff=70, staff_hand=(42, 60),
                back_hand=(-36, 62), tip=18, glow=0.8)
    return rise, fall


def dash_poses():
    poses = []
    for i in range(6):
        t = i / 6
        k = 0.45 + 0.55 * math.sin(math.pi * (0.1 + 0.8 * t))  # 가운데에서 가장 길게, 프레임마다 조금씩 다름
        poses.append(Pose(lean=22 * k + 4, bob=2 * math.sin(2 * math.pi * t), stretch=1.0 - 0.12 * k, foot_f=(14, 6), foot_b=(-22, 10),
                          staff=172, staff_hand=(26, 40), back_hand=(-40, 44), tip=20 * k, glow=1.0))
    return poses


def attack_poses():
    # 0.5초: 지팡이를 뒤로 들어 올림(0~2) → 앞으로 내지르며 번쩍(3~4) → 돌아옴(5~7)
    keys = [  # staff, hand(x, y), lean, stretch, flash
        (100, (34, 56), -2, 0.97, 0.0),
        (125, (24, 64), -5, 0.95, 0.0),
        (140, (18, 70), -6, 0.94, 0.0),
        (20, (30, 54), 8, 1.06, 1.0),
        (15, (32, 52), 9, 1.04, 0.8),
        (40, (40, 50), 5, 1.0, 0.3),
        (65, (44, 48), 2, 1.0, 0.0),
        (80, (41, 46), 0, 1.0, 0.0),
    ]
    return [Pose(staff=s, staff_hand=h, lean=l, stretch=st, flash=f, glow=0.7, tip=-4 * l / 9,
                 back_hand=(-30, 34 + (4 if f > 0.5 else 0))) for s, h, l, st, f in keys]


def ghost(img, color, alpha):
    """잔상: 모양만 남기고 속성색으로 물들인 반투명 복사본."""
    a = img.getchannel("A").point(lambda v: int(v * alpha))
    tint = Image.new("RGBA", img.size, color[:3] + (0,))
    tint.putalpha(a)
    return tint


def dash_frames():
    frames = []
    for pose in dash_poses():
        body = draw_wizard(pose)
        out = Image.new("RGBA", body.size, (0, 0, 0, 0))
        for k, alpha in ((3, 0.12), (2, 0.3), (1, 0.55)):  # ArtStyle 잔상 60% → 30% → 10%
            g = ghost(body, AQUA, alpha)
            out.alpha_composite(g, (-18 * k, 0))
        out.alpha_composite(body)
        frames.append(out)
    return frames


def save(img, *parts):
    path = os.path.join(OUT, *parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img.save(path)


def generate():
    for i, p in enumerate(idle_poses()):
        save(draw_wizard(p), "Idle", f"wizard_idle_{i:02d}.png")
    for i, p in enumerate(walk_poses()):
        save(draw_wizard(p), "Walk", f"wizard_walk_{i:02d}.png")
    for i, p in enumerate(run_poses()):
        save(draw_wizard(p), "Run", f"wizard_run_{i:02d}.png")
    rise, fall = jump_poses()
    save(draw_wizard(rise), "Jump", "wizard_rise.png")
    save(draw_wizard(fall), "Jump", "wizard_fall.png")
    for i, img in enumerate(dash_frames()):
        save(img, "Dash", f"wizard_dash_{i:02d}.png")
    for i, p in enumerate(attack_poses()):
        save(draw_wizard(p), "Attack", f"wizard_attack_{i:02d}.png")


def contact_sheet(path):
    rows = []
    for folder, prefix in (("Idle", "wizard_idle_"), ("Walk", "wizard_walk_"), ("Run", "wizard_run_"),
                           ("Jump", "wizard_"), ("Dash", "wizard_dash_"), ("Attack", "wizard_attack_")):
        d = os.path.join(OUT, folder)
        files = sorted(f for f in os.listdir(d) if f.startswith(prefix) and f.endswith(".png"))
        rows.append([Image.open(os.path.join(d, f)) for f in files])
    sheet = Image.new("RGBA", (W * 8, H * len(rows)), (70, 80, 95, 255))
    for r, row in enumerate(rows):
        for i, im in enumerate(row):
            sheet.alpha_composite(im, (W * i, H * r))
    sheet.save(path)


if __name__ == "__main__":
    generate()
    if len(sys.argv) > 1:
        contact_sheet(sys.argv[1])
    print("done")
