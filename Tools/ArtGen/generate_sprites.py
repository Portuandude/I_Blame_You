"""I Blame You! 임시 아트 생성기.

고블린 4종(고블린/궁수/병사/대전사)의 Idle/Run/Attack/Die 프레임과
스킬·투사체 이펙트 프레임을 그려 Assets/Art/Sprites 아래에 PNG로 저장한다.
디버프 아이콘과 마법 비행/적중 이펙트는 런타임 로드용으로 Assets/Resources 아래에 저장한다.
진짜 아트가 생기면 같은 경로/이름의 PNG로 바꾸고 Unity에서
Tools > I Blame You > Generate Sprites And Animations 를 다시 실행하면 된다.

    python3 Tools/ArtGen/generate_sprites.py

스타일은 기존 슬라임/마법사에 맞춘 굵은 외곽선 + 단순 셀 셰이딩.
모든 캐릭터는 오른쪽을 보고, 발이 캔버스 맨 아래 가운데에 닿는다 (하단 중앙 피벗).
"""
import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SPRITES = os.path.join(ROOT, "Assets", "Art", "Sprites")
SS = 4  # 슈퍼샘플링 배율 (크게 그린 뒤 줄여서 외곽선을 부드럽게)
OUTLINE = (24, 20, 26, 255)


def rgba(hex_color, alpha=255):
    hex_color = hex_color.lstrip("#")
    return tuple(int(hex_color[i:i + 2], 16) for i in (0, 2, 4)) + (alpha,)


def shade(color, factor):
    r, g, b, a = color
    if factor >= 1:
        return (int(r + (255 - r) * (factor - 1)), int(g + (255 - g) * (factor - 1)), int(b + (255 - b) * (factor - 1)), a)
    return (int(r * factor), int(g * factor), int(b * factor), a)


class Canvas:
    """발(0,0) 기준, y가 위로 증가하는 좌표(최종 픽셀 단위)로 그리는 캔버스."""

    def __init__(self, width, height, foot_x=None, foot_y=0, outline=3):
        self.w, self.h = width, height
        self.image = Image.new("RGBA", (width * SS, height * SS), (0, 0, 0, 0))
        self.d = ImageDraw.Draw(self.image)
        self.fx = width / 2 if foot_x is None else foot_x
        self.fy = foot_y
        self.o = outline

    def p(self, x, y):
        return ((self.fx + x) * SS, (self.h - self.fy - y) * SS)

    def ellipse(self, cx, cy, rx, ry, fill, outline=True, highlight=True):
        x, y = self.p(cx, cy)
        if outline:
            o = self.o * SS
            self.d.ellipse([x - rx * SS - o, y - ry * SS - o, x + rx * SS + o, y + ry * SS + o], fill=OUTLINE)
        self.d.ellipse([x - rx * SS, y - ry * SS, x + rx * SS, y + ry * SS], fill=fill)
        if highlight and rx > 4 and ry > 4:
            hx, hy = self.p(cx - rx * 0.3, cy + ry * 0.35)
            self.d.ellipse([hx - rx * 0.35 * SS, hy - ry * 0.22 * SS, hx + rx * 0.35 * SS, hy + ry * 0.22 * SS],
                           fill=shade(fill, 1.25))

    def limb(self, a, b, width, fill, outline=True):
        pa, pb = self.p(*a), self.p(*b)
        if outline:
            ow = (width + 2 * self.o) * SS
            self.d.line([pa, pb], fill=OUTLINE, width=int(ow))
            for q in (pa, pb):
                self.d.ellipse([q[0] - ow / 2, q[1] - ow / 2, q[0] + ow / 2, q[1] + ow / 2], fill=OUTLINE)
        w = width * SS
        self.d.line([pa, pb], fill=fill, width=int(w))
        for q in (pa, pb):
            self.d.ellipse([q[0] - w / 2, q[1] - w / 2, q[0] + w / 2, q[1] + w / 2], fill=fill)

    def polygon(self, points, fill, outline=True):
        pts = [self.p(*q) for q in points]
        self.d.polygon(pts, fill=fill)
        if outline:
            self.d.line(pts + [pts[0]], fill=OUTLINE, width=int(self.o * SS * 1.4), joint="curve")

    def line(self, a, b, width, fill):
        self.d.line([self.p(*a), self.p(*b)], fill=fill, width=int(width * SS))

    def arc_points(self, cx, cy, r, start_deg, end_deg, steps=24):
        pts = []
        for i in range(steps + 1):
            t = math.radians(start_deg + (end_deg - start_deg) * i / steps)
            pts.append((cx + math.cos(t) * r, cy + math.sin(t) * r))
        return pts

    def polyline(self, points, width, fill, outline=True):
        pts = [self.p(*q) for q in points]
        if outline:
            self.d.line(pts, fill=OUTLINE, width=int((width + 2 * self.o) * SS), joint="curve")
        self.d.line(pts, fill=fill, width=int(width * SS), joint="curve")

    def result(self):
        return self.image.resize((self.w, self.h), Image.LANCZOS)


def offset(origin, angle_deg, length):
    """origin에서 '아래 방향 기준 angle(앞쪽이 +)'으로 length만큼 뻗은 점."""
    a = math.radians(angle_deg)
    return (origin[0] + math.sin(a) * length, origin[1] - math.cos(a) * length)


def save(image, *parts):
    path = os.path.join(SPRITES, *parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.save(path)


# --------------------------------------------------------------------------------------
# 고블린
# --------------------------------------------------------------------------------------

SKIN = rgba("8fbf3a")
CLOTH = rgba("7a4a2a")
STEEL = rgba("b8c0c8")
WOOD = rgba("8a5a2e")


class Pose:
    def __init__(self, **kw):
        self.bob = 0.0
        self.lean = 0.0           # 몸통을 앞으로 미는 정도(px)
        self.leg_front = 4.0      # 앞다리 각도
        self.leg_back = -4.0
        self.knee_front = 0.0     # 정강이 추가 굽힘(뒤로)
        self.knee_back = 0.0
        self.arm_front = 20.0     # 앞팔 위팔 각도
        self.fore_front = 50.0    # 앞팔 아래팔 각도(절대)
        self.arm_back = -15.0
        self.fore_back = 10.0
        self.weapon = 70.0        # 무기 각도(절대, 아래 기준)
        self.mouth_open = False
        self.draw = 0.0           # 활 시위 당김 0~1
        self.arrow = True
        self.squint = False
        self.__dict__.update(kw)


class GoblinSpec:
    def __init__(self, name, prefix, width, height, scale=1.0, kind="goblin"):
        self.name, self.prefix = name, prefix
        self.width, self.height = width, height
        self.scale = scale
        self.kind = kind


def draw_goblin(spec, pose):
    s = spec.scale
    c = Canvas(spec.width, spec.height, outline=3 if s < 1.2 else 3.5)
    skin = SKIN if spec.kind != "warrior" else rgba("7aa83a")
    dark_skin = shade(skin, 0.75)

    hip = (pose.lean * 0.5, (22 + pose.bob) * s)
    chest = (pose.lean + 2 * s, (40 + pose.bob) * s)
    head = (pose.lean * 1.2 + 4 * s, (64 + pose.bob) * s)
    shoulder_f = (chest[0] + 6 * s, chest[1] + 4 * s)
    shoulder_b = (chest[0] - 6 * s, chest[1] + 4 * s)

    def leg(angle, knee, color):
        knee_pt = offset(hip, angle, 11 * s)
        foot = offset(knee_pt, angle - knee, 11 * s)
        c.limb(hip, knee_pt, 8 * s, color)
        c.limb(knee_pt, foot, 7 * s, color)
        c.ellipse(foot[0] + 3 * s, foot[1] + 1.5 * s, 6 * s, 3.5 * s, shade(CLOTH, 0.7), highlight=False)

    def arm(upper, fore, color):
        sh = shoulder_f if color is skin else shoulder_b
        elbow = offset(sh, upper, 9 * s)
        hand = offset(elbow, fore, 9 * s)
        c.limb(sh, elbow, 7 * s, color)
        c.limb(elbow, hand, 6.5 * s, color)
        c.ellipse(hand[0], hand[1], 4.5 * s, 4.5 * s, color, highlight=False)
        return hand

    # 대전사 망토 (몸 뒤)
    if spec.kind == "warrior":
        cape = rgba("a83030")
        c.polygon([(chest[0] - 4 * s, chest[1] + 10 * s), (chest[0] + 4 * s, chest[1] + 10 * s),
                   (hip[0] - 8 * s, 6 * s), (hip[0] - 24 * s - pose.lean, 10 * s + pose.bob)], cape)

    # 뒤쪽 팔/다리 (어둡게)
    leg(pose.leg_back, pose.knee_back, dark_skin)
    back_hand = arm(pose.arm_back, pose.fore_back, dark_skin)
    if spec.kind == "soldier":
        c.ellipse(back_hand[0] + 2 * s, back_hand[1] + 2 * s, 10 * s, 13 * s, WOOD)
        c.ellipse(back_hand[0] + 2 * s, back_hand[1] + 2 * s, 4 * s, 4 * s, STEEL, highlight=False)
    leg(pose.leg_front, pose.knee_front, skin)

    # 몸통 + 허리천
    torso_color = skin
    if spec.kind == "soldier":
        torso_color = rgba("8a6a44")
    elif spec.kind == "warrior":
        torso_color = rgba("8d939c")
    elif spec.kind == "archer":
        torso_color = rgba("4f6b33")
    c.ellipse(chest[0], chest[1] - 4 * s, 13 * s, 15 * s, torso_color)
    if spec.kind == "warrior":
        c.ellipse(chest[0] + 3 * s, chest[1] + 2 * s, 7 * s, 5 * s, shade(torso_color, 1.15), highlight=False)
    c.polygon([(hip[0] - 12 * s, hip[1] + 4 * s), (hip[0] + 12 * s, hip[1] + 4 * s),
               (hip[0] + 9 * s, hip[1] - 9 * s), (hip[0] + 1 * s, hip[1] - 5 * s), (hip[0] - 9 * s, hip[1] - 10 * s)], CLOTH)
    c.limb((hip[0] - 12 * s, hip[1] + 4 * s), (hip[0] + 12 * s, hip[1] + 4 * s), 3 * s, rgba("4a2c18"), outline=False)

    # 머리: 귀 → 얼굴 → 눈/코/입
    hx, hy = head
    ear = skin
    c.polygon([(hx - 12 * s, hy + 6 * s), (hx - 38 * s, hy + 14 * s), (hx - 14 * s, hy - 4 * s)], ear)
    c.polygon([(hx + 14 * s, hy + 6 * s), (hx + 36 * s, hy + 16 * s), (hx + 16 * s, hy - 2 * s)], ear)
    c.polygon([(hx + 17 * s, hy + 5 * s), (hx + 30 * s, hy + 13 * s), (hx + 18 * s, hy + 1 * s)], shade(ear, 0.8), outline=False)

    if spec.kind == "archer":
        hood = rgba("3d5a2a")
        c.ellipse(hx - 3 * s, hy + 2 * s, 23 * s, 22 * s, hood)
    c.ellipse(hx, hy, 20 * s, 18 * s, skin)
    # 코
    c.ellipse(hx + 19 * s, hy - 2 * s, 6 * s, 4.5 * s, shade(skin, 0.9), highlight=False)
    # 눈
    eye_y = hy + 4 * s
    eye_h = 2.2 * s if pose.squint else 4.5 * s
    c.ellipse(hx + 8 * s, eye_y, 4.5 * s, eye_h, rgba("ffe14d"), highlight=False)
    if not pose.squint:
        c.ellipse(hx + 10 * s, eye_y, 1.8 * s, 2.5 * s, OUTLINE, outline=False, highlight=False)
    c.limb((hx + 2 * s, eye_y + 7 * s), (hx + 13 * s, eye_y + 5 * s), 2.2 * s, OUTLINE, outline=False)  # 찡그린 눈썹
    # 입 + 송곳니
    if pose.mouth_open:
        c.ellipse(hx + 11 * s, hy - 9 * s, 6 * s, 4 * s, rgba("5a1a1a"), highlight=False)
    else:
        c.limb((hx + 5 * s, hy - 9 * s), (hx + 15 * s, hy - 8 * s), 2 * s, OUTLINE, outline=False)
    c.polygon([(hx + 8 * s, hy - 8 * s), (hx + 10 * s, hy - 3.5 * s), (hx + 12 * s, hy - 8 * s)], rgba("fff6dc"), outline=False)

    # 투구
    if spec.kind == "soldier":
        c.polygon(c.arc_points(hx, hy + 2 * s, 21 * s, 0, 180, 16) + [(hx - 23 * s, hy + 2 * s)], STEEL)
        c.limb((hx - 23 * s, hy + 3 * s), (hx + 23 * s, hy + 3 * s), 4 * s, shade(STEEL, 0.8))
    elif spec.kind == "warrior":
        c.polygon(c.arc_points(hx, hy + 3 * s, 21 * s, 0, 180, 16), rgba("6d737c"))
        horn = rgba("efe2c0")
        c.polygon([(hx - 14 * s, hy + 18 * s), (hx - 30 * s, hy + 34 * s), (hx - 20 * s, hy + 14 * s)], horn)
        c.polygon([(hx + 14 * s, hy + 18 * s), (hx + 28 * s, hy + 36 * s), (hx + 20 * s, hy + 14 * s)], horn)
        c.limb((hx - 22 * s, hy + 4 * s), (hx + 22 * s, hy + 4 * s), 4 * s, rgba("50555c"))
    elif spec.kind == "archer":
        c.polygon(c.arc_points(hx - 2 * s, hy + 4 * s, 21 * s, 20, 175, 14) + [(hx - 8 * s, hy + 12 * s)], rgba("3d5a2a"))

    # 앞쪽 팔 + 무기
    hand = arm(pose.arm_front, pose.fore_front, skin)
    draw_weapon(c, spec, pose, hand, back_hand, s)
    return c.result()


def draw_weapon(c, spec, pose, hand, back_hand, s):
    if spec.kind == "goblin":
        tip = offset(hand, pose.weapon, 20 * s)
        hilt = offset(hand, pose.weapon + 180, 5 * s)
        c.limb(hilt, hand, 3.5 * s, WOOD)
        c.polygon([offset(hand, pose.weapon - 90, 3 * s), tip, offset(hand, pose.weapon + 90, 3 * s)], STEEL)
    elif spec.kind == "soldier":
        butt = offset(hand, pose.weapon + 180, 30 * s)
        tip = offset(hand, pose.weapon, 48 * s)
        c.limb(butt, tip, 3.5 * s, WOOD)
        head_tip = offset(tip, pose.weapon, 14 * s)
        c.polygon([offset(tip, pose.weapon - 90, 5 * s), head_tip, offset(tip, pose.weapon + 90, 5 * s)], STEEL)
    elif spec.kind == "warrior":
        butt = offset(hand, pose.weapon + 180, 10 * s)
        top = offset(hand, pose.weapon, 34 * s)
        c.limb(butt, top, 5 * s, WOOD)
        a = pose.weapon
        blade = [offset(top, a + 90, 2 * s), offset(offset(top, a + 90, 20 * s), a, 8 * s),
                 offset(offset(top, a + 90, 24 * s), a, -4 * s), offset(offset(top, a + 90, 20 * s), a, -16 * s),
                 offset(offset(top, a + 90, 2 * s), a, -10 * s)]
        c.polygon(blade, STEEL)
        c.polygon([offset(top, a - 90, 2 * s), offset(top, a - 90, 9 * s), offset(offset(top, a - 90, 2 * s), a, -6 * s)],
                  shade(STEEL, 0.85))
    elif spec.kind == "archer":
        # 활은 뒤쪽 손이 앞으로 뻗어 잡고, 앞쪽 손이 시위를 당긴다.
        grip = back_hand
        bow_r = 26 * s
        center = (grip[0] - bow_r * 0.75, grip[1])
        pts = c.arc_points(center[0], center[1], bow_r, -48, 48, 18)
        c.polyline(pts, 3.5 * s, WOOD)
        top, bottom = pts[-1], pts[0]
        pull = (grip[0] - (8 + 18 * pose.draw) * s, grip[1])
        string = rgba("efe6d0")
        if pose.draw > 0.01:
            c.line(top, pull, 1.2 * s, string)
            c.line(pull, bottom, 1.2 * s, string)
        else:
            c.line(top, bottom, 1.2 * s, string)
        if pose.arrow and pose.draw > 0.01:
            c.limb((pull[0], pull[1]), (grip[0] + 14 * s, grip[1]), 1.6 * s, WOOD, outline=False)
            c.polygon([(grip[0] + 14 * s, grip[1] + 3 * s), (grip[0] + 20 * s, grip[1]), (grip[0] + 14 * s, grip[1] - 3 * s)], STEEL,
                      outline=False)


def run_pose(t, spec):
    phi = 2 * math.pi * t
    swing = math.sin(phi)
    p = Pose(bob=abs(math.sin(phi)) * 2.0, lean=3.0,
             leg_front=34 * swing, leg_back=-34 * swing,
             knee_front=max(0, -math.cos(phi)) * 40, knee_back=max(0, math.cos(phi)) * 40,
             arm_front=25 - 30 * swing, fore_front=60 - 25 * swing,
             arm_back=-10 + 30 * swing, fore_back=20 + 20 * swing,
             mouth_open=True)
    if spec.kind == "goblin":
        p.weapon = 80 - 20 * swing
    elif spec.kind == "soldier":
        p.arm_front, p.fore_front, p.weapon = 15 - 15 * swing, 85, 82
    elif spec.kind == "warrior":
        p.arm_front, p.fore_front, p.weapon = 10 - 20 * swing, 70, 150
    elif spec.kind == "archer":
        p.arm_back, p.fore_back = 70, 90
    return p


def idle_pose(t, spec):
    b = math.sin(2 * math.pi * t)
    p = Pose(bob=b * 1.2, squint=(t > 0.8))
    if spec.kind == "soldier":
        p.arm_front, p.fore_front, p.weapon = 10, 80, 85
    elif spec.kind == "warrior":
        p.arm_front, p.fore_front, p.weapon = 5 + b * 3, 60, 150
    elif spec.kind == "archer":
        p.arm_back, p.fore_back, p.arm_front, p.fore_front = 60, 85, 10, 40
    else:
        p.weapon = 75 + b * 4
    return p


def attack_poses(spec):
    if spec.kind == "goblin":  # 단검 휘두르기: 3프레임 예비 → 휘둘러 판정 → 회복
        keys = [(40, 40, 160, 0), (100, 130, 200, -1), (150, 170, 230, -2), (80, 90, 60, 4), (60, 70, 50, 3), (30, 55, 70, 1)]
        return [Pose(arm_front=a, fore_front=f, weapon=w, lean=l, mouth_open=i >= 3) for i, (a, f, w, l) in enumerate(keys)]
    if spec.kind == "soldier":  # 창 찌르기
        keys = [(0, 80, 85, 0), (-25, 70, 88, -3), (-40, 65, 90, -5), (55, 92, 90, 7), (45, 90, 90, 6), (15, 82, 86, 2)]
        return [Pose(arm_front=a, fore_front=f, weapon=w, lean=l, mouth_open=i == 3,
                     leg_front=12 if i == 3 else 4) for i, (a, f, w, l) in enumerate(keys)]
    if spec.kind == "warrior":  # 도끼 내려찍기
        keys = [(60, 120, 170, -1), (130, 170, 210, -3), (170, 190, 220, -4), (60, 70, 70, 6), (45, 55, 55, 5), (20, 60, 120, 2)]
        return [Pose(arm_front=a, fore_front=f, weapon=w, lean=l, mouth_open=i >= 3, squint=i == 3)
                for i, (a, f, w, l) in enumerate(keys)]
    # 궁수: 시위 당기기 4프레임 → 놓기 → 회복
    poses = []
    for i, d in enumerate([0.15, 0.45, 0.75, 1.0]):
        poses.append(Pose(arm_back=88, fore_back=90, arm_front=70, fore_front=90 + 30 * d, draw=d, squint=i >= 2))
    poses.append(Pose(arm_back=88, fore_back=92, arm_front=80, fore_front=150, draw=0.0, arrow=False, mouth_open=True))
    poses.append(Pose(arm_back=70, fore_back=85, arm_front=40, fore_front=80, draw=0.0, arrow=False))
    return poses


def die_frames(spec):
    frames = []
    angles = [0, 12, 35, 62, 82, 90]
    alphas = [1, 1, 1, 1, 0.75, 0.4]
    for i, (angle, alpha) in enumerate(zip(angles, alphas)):
        pose = Pose(arm_front=60 + i * 15, fore_front=90 + i * 10, arm_back=-40, fore_back=-20,
                    leg_front=10 + i * 3, mouth_open=True, squint=True)
        img = draw_goblin(spec, pose)
        foot = (spec.width / 2, spec.height - 1)
        img = img.rotate(angle, center=foot, resample=Image.BICUBIC)
        lift = int(round(angle / 90 * 7 * spec.scale))
        if lift:
            shifted = Image.new("RGBA", img.size, (0, 0, 0, 0))
            shifted.paste(img, (0, -lift))
            img = shifted
        if alpha < 1:
            a = img.getchannel("A").point(lambda v: int(v * alpha))
            img.putalpha(a)
        frames.append(img)
    return frames


def generate_goblins():
    specs = [
        GoblinSpec("Goblin", "goblin", 160, 128, 1.0, "goblin"),
        GoblinSpec("GoblinArcher", "goblin_archer", 160, 128, 1.0, "archer"),
        GoblinSpec("GoblinSoldier", "goblin_soldier", 256, 136, 1.08, "soldier"),
        GoblinSpec("GoblinWarrior", "goblin_warrior", 300, 200, 1.45, "warrior"),
    ]
    for spec in specs:
        base = ("Enemies", spec.name)
        for i in range(6):
            save(draw_goblin(spec, idle_pose(i / 6, spec)), *base, "Idle", f"{spec.prefix}_idle_{i:02d}.png")
        for i in range(8):
            save(draw_goblin(spec, run_pose(i / 8, spec)), *base, "Run", f"{spec.prefix}_run_{i:02d}.png")
        for i, pose in enumerate(attack_poses(spec)):
            save(draw_goblin(spec, pose), *base, "Attack", f"{spec.prefix}_attack_{i:02d}.png")
        for i, img in enumerate(die_frames(spec)):
            save(img, *base, "Die", f"{spec.prefix}_die_{i:02d}.png")


# --------------------------------------------------------------------------------------
# 이펙트 (모두 +x 방향을 향함, 중앙 피벗 — 땅 가시만 하단 중앙)
# --------------------------------------------------------------------------------------

def glow(img, radius):
    blurred = img.filter(ImageFilter.GaussianBlur(radius))
    out = Image.alpha_composite(blurred, img)
    return out


def blank(w, h):
    return Image.new("RGBA", (w * SS, h * SS), (0, 0, 0, 0))


def finish(img, w, h, glow_radius=0):
    small = img.resize((w, h), Image.LANCZOS)
    return glow(small, glow_radius) if glow_radius else small


def fireball_frames():
    rnd = random.Random(7)
    frames = []
    w, h = 96, 64
    for i in range(6):
        img = blank(w, h)
        d = ImageDraw.Draw(img)
        cx, cy = 62 * SS, 32 * SS
        # 꼬리: 뒤로 갈수록 작고 붉은 불덩이들
        for k in range(14):
            t = k / 13
            x = cx - (8 + t * 50) * SS + rnd.uniform(-3, 3) * SS
            y = cy + math.sin(t * 7 + i * 1.3) * 5 * SS * t
            r = (16 - 12 * t) * SS
            col = (255, int(200 - 150 * t), int(60 - 50 * t), int(220 - 170 * t))
            d.ellipse([x - r, y - r, x + r, y + r], fill=col)
        flicker = 1 + 0.08 * math.sin(i * 2.1)
        for r, col in [(19, (230, 70, 30, 255)), (15, (255, 140, 40, 255)), (10, (255, 215, 90, 255)), (5, (255, 250, 220, 255))]:
            rr = r * SS * flicker
            d.ellipse([cx - rr, cy - rr, cx + rr, cy + rr], fill=col)
        frames.append(finish(img, w, h, 2))
    return frames


def water_frames():
    frames = []
    w, h = 112, 36
    for i in range(4):
        img = blank(w, h)
        d = ImageDraw.Draw(img)
        cy = h / 2 * SS
        body = [(6 * SS, cy), (30 * SS, cy - 7 * SS), (88 * SS, cy - 6 * SS), (108 * SS, cy), (88 * SS, cy + 6 * SS),
                (30 * SS, cy + 7 * SS)]
        d.polygon(body, fill=(40, 120, 230, 235))
        inner = [(14 * SS, cy), (34 * SS, cy - 4 * SS), (86 * SS, cy - 3 * SS), (102 * SS, cy), (86 * SS, cy + 3 * SS),
                 (34 * SS, cy + 4 * SS)]
        d.polygon(inner, fill=(120, 200, 255, 255))
        for k in range(5):
            x = (20 + k * 16 + i * 4) % 90 * SS + 10 * SS
            d.line([(x, cy - 2 * SS), (x + 8 * SS, cy - 2 * SS)], fill=(235, 250, 255, 255), width=2 * SS)
        for k in range(3):
            x = (8 + k * 9 + i * 3) * SS
            r = (3 - k * 0.6) * SS
            d.ellipse([x - r, cy + (k - 1) * 6 * SS - r, x + r, cy + (k - 1) * 6 * SS + r], fill=(150, 210, 255, 200))
        frames.append(finish(img, w, h, 2))
    return frames


def wind_frames():
    frames = []
    w = h = 200
    for i in range(6):
        img = blank(w, h)
        d = ImageDraw.Draw(img)
        c = w / 2 * SS
        for k in range(4):
            r = (40 + k * 18) * SS
            start = -60 + i * 12 + k * 8
            alpha = 230 - k * 45
            d.arc([c - r - 20 * SS, c - r, c + r - 20 * SS, c + r], start=start, end=start + 120,
                  fill=(225, 255, 235, alpha), width=int((10 - k * 2) * SS))
        for k in range(8):
            ang = math.radians(i * 30 + k * 45)
            x, y = c + math.cos(ang) * 60 * SS, c + math.sin(ang) * 60 * SS
            d.ellipse([x - 3 * SS, y - 3 * SS, x + 3 * SS, y + 3 * SS], fill=(200, 255, 220, 200))
        frames.append(finish(img, w, h, 3))
    return frames


def earth_frames():
    frames = []
    w, h = 320, 170
    heights = [0.0, 0.15, 0.55, 1.0, 1.0, 0.85, 0.5, 0.2]
    rnd = random.Random(3)
    spikes = [(-120, 0.6), (-75, 0.85), (-30, 1.0), (15, 0.9), (60, 1.0), (105, 0.7)]
    for i, hk in enumerate(heights):
        c = Canvas(w, h, outline=3)
        # 갈라진 땅
        c.polygon([(-150, 0), (150, 0), (140, 8), (-140, 8)], rgba("6b4a2b"))
        for x, scale in spikes:
            top = 140 * scale * hk
            if top < 4:
                continue
            width = 26 * scale
            c.polygon([(x - width, 2), (x + rnd.uniform(-6, 6), top), (x + width, 2)], rgba("9a7a52"))
            c.polygon([(x - width * 0.2, 4), (x + rnd.uniform(-3, 3), top * 0.85), (x + width * 0.55, 4)], rgba("c4a476"), outline=False)
        if i >= 5:
            for k in range(10):
                x = rnd.uniform(-140, 140)
                y = rnd.uniform(10, 80) * (1.2 - hk)
                c.ellipse(x, y, 4, 3, rgba("8a6a42"), highlight=False)
        img = c.result()
        if i == len(heights) - 1:
            img.putalpha(img.getchannel("A").point(lambda v: int(v * 0.6)))
        frames.append(img)
    return frames


def arcane_frames():
    frames = []
    w = h = 56
    for i in range(6):
        img = blank(w, h)
        d = ImageDraw.Draw(img)
        c = w / 2 * SS
        for r, col in [(16, (120, 60, 220, 255)), (12, (170, 110, 255, 255)), (6, (240, 220, 255, 255))]:
            d.ellipse([c - r * SS, c - r * SS, c + r * SS, c + r * SS], fill=col)
        for k in range(4):
            ang = math.radians(i * 30 + k * 90)
            x, y = c + math.cos(ang) * 22 * SS, c + math.sin(ang) * 22 * SS
            d.polygon([(x, y - 4 * SS), (x + 2 * SS, y), (x, y + 4 * SS), (x - 2 * SS, y)], fill=(230, 200, 255, 255))
        frames.append(finish(img, w, h, 2))
    return frames


def shield_frames():
    """흰색 방어막 (게임에서 속성 색을 곱한다)."""
    frames = []
    w = h = 160
    for i in range(6):
        img = blank(w, h)
        d = ImageDraw.Draw(img)
        c = w / 2 * SS
        r = 72 * SS
        d.ellipse([c - r, c - r, c + r, c + r], fill=(255, 255, 255, 60))
        d.ellipse([c - r, c - r, c + r, c + r], outline=(255, 255, 255, 230), width=5 * SS)
        # 육각 무늬
        for gy in range(-3, 4):
            for gx in range(-3, 4):
                x = c + (gx * 26 + (13 if gy % 2 else 0)) * SS
                y = c + gy * 22 * SS
                if (x - c) ** 2 + (y - c) ** 2 > (r * 0.9) ** 2:
                    continue
                hexagon = [(x + math.cos(math.radians(60 * k + 30)) * 12 * SS, y + math.sin(math.radians(60 * k + 30)) * 12 * SS)
                           for k in range(6)]
                d.line(hexagon + [hexagon[0]], fill=(255, 255, 255, 70), width=2 * SS)
        # 돌아가는 반사광
        start = i * 60
        d.arc([c - r * 0.8, c - r * 0.8, c + r * 0.8, c + r * 0.8], start=start, end=start + 50, fill=(255, 255, 255, 200),
              width=6 * SS)
        frames.append(finish(img, w, h, 2))
    return frames


def teleport_frames():
    frames = []
    w = h = 128
    rnd = random.Random(11)
    for i in range(8):
        t = i / 7
        img = blank(w, h)
        d = ImageDraw.Draw(img)
        c = w / 2 * SS
        alpha = int(255 * (1 - t) ** 0.7)
        r = (14 + 46 * t) * SS
        d.ellipse([c - r, c - r * 1.2, c + r, c + r * 1.2], outline=(190, 140, 255, alpha), width=int((8 - 5 * t) * SS))
        core = (20 * (1 - t) + 2) * SS
        d.ellipse([c - core, c - core * 1.4, c + core, c + core * 1.4], fill=(240, 220, 255, alpha))
        for k in range(10):
            ang = rnd.uniform(0, math.tau)
            dist = rnd.uniform(0.4, 1.0) * r
            x, y = c + math.cos(ang) * dist, c + math.sin(ang) * dist
            s = rnd.uniform(2, 4) * SS
            d.ellipse([x - s, y - s, x + s, y + s], fill=(220, 190, 255, alpha))
        frames.append(finish(img, w, h, 3))
    return frames


def arrow_frames():
    c = Canvas(56, 16, foot_x=28, foot_y=8, outline=1.5)
    c.limb((-22, 0), (16, 0), 2.2, WOOD)
    c.polygon([(14, 4), (26, 0), (14, -4)], STEEL)
    c.polygon([(-24, 0), (-18, 5), (-12, 0)], rgba("e0e0e0"))
    c.polygon([(-24, 0), (-18, -5), (-12, 0)], rgba("c8c8c8"))
    return [c.result()]


def slash_frames():
    frames = []
    w = h = 120
    for i in range(5):
        img = blank(w, h)
        d = ImageDraw.Draw(img)
        c = w / 2 * SS
        r = 46 * SS
        start = -100 + i * 25
        alpha = 255 if i < 3 else 255 - (i - 2) * 90
        d.arc([c - r, c - r, c + r, c + r], start=start, end=start + 90, fill=(255, 255, 255, alpha), width=10 * SS)
        d.arc([c - r * 0.85, c - r * 0.85, c + r * 0.85, c + r * 0.85], start=start + 10, end=start + 80,
              fill=(255, 255, 255, alpha // 2), width=4 * SS)
        frames.append(finish(img, w, h, 2))
    return frames


def generate_effects():
    sets = {
        ("Fireball", "fx_fireball_"): fireball_frames(),
        ("WaterLance", "fx_water_"): water_frames(),
        ("WindGust", "fx_wind_"): wind_frames(),
        ("EarthSpike", "fx_earth_"): earth_frames(),
        ("ArcaneOrb", "fx_arcane_"): arcane_frames(),
        ("Shield", "fx_shield_"): shield_frames(),
        ("Teleport", "fx_teleport_"): teleport_frames(),
        ("Arrow", "fx_arrow_"): arrow_frames(),
        ("Slash", "fx_slash_"): slash_frames(),
    }
    for (folder, prefix), frames in sets.items():
        for i, img in enumerate(frames):
            save(img, "Effects", folder, f"{prefix}{i:02d}.png")


def contact_sheet(path):
    """확인용: 생성한 프레임을 한 장에 모은다 (저장소에는 넣지 않음)."""
    rows = []
    for name in ["Goblin", "GoblinArcher", "GoblinSoldier", "GoblinWarrior"]:
        for anim in ["Idle", "Run", "Attack", "Die"]:
            folder = os.path.join(SPRITES, "Enemies", name, anim)
            files = sorted(f for f in os.listdir(folder) if f.endswith(".png"))
            rows.append([Image.open(os.path.join(folder, f)) for f in files])
    for folder in ["Fireball", "WaterLance", "WindGust", "EarthSpike", "ArcaneOrb", "Shield", "Teleport", "Arrow", "Slash"]:
        fpath = os.path.join(SPRITES, "Effects", folder)
        files = sorted(f for f in os.listdir(fpath) if f.endswith(".png"))
        rows.append([Image.open(os.path.join(fpath, f)) for f in files])
    width = max(sum(im.width + 4 for im in row) for row in rows)
    height = sum(max(im.height for im in row) + 4 for row in rows)
    sheet = Image.new("RGBA", (width, height), (70, 80, 95, 255))
    y = 0
    for row in rows:
        x = 0
        for im in row:
            sheet.alpha_composite(im, (x, y))
            x += im.width + 4
        y += max(im.height for im in row) + 4
    sheet.save(path)


# ---------------------------------------------------------------------------
# 디버프 아이콘 (Assets/Resources/StatusIcons). 사각형 바탕 + 가운데 그림.
# 런타임에 Resources.Load로 읽어 적 머리 위에 띄운다 (StatusIconBar).

ICON = 64


def icon_base(fill, border):
    img = blank(ICON, ICON)
    d = ImageDraw.Draw(img)
    m = 3 * SS
    d.rounded_rectangle([m, m, ICON * SS - m, ICON * SS - m], radius=8 * SS, fill=rgba(fill),
                        outline=OUTLINE, width=3 * SS)
    d.rounded_rectangle([m + 4 * SS, m + 4 * SS, ICON * SS - m - 4 * SS, ICON * SS - m - 4 * SS],
                        radius=6 * SS, outline=rgba(border), width=2 * SS)
    return img, d


def burn_icon():
    img, d = icon_base("#5a1e0e", "#c8501e")
    s = SS
    outer = [(32, 10), (42, 24), (47, 36), (44, 47), (36, 54), (28, 54), (20, 47), (17, 37), (21, 27), (26, 33), (27, 22)]
    inner = [(32, 26), (38, 36), (39, 45), (34, 51), (29, 51), (25, 45), (26, 37), (30, 40)]
    d.polygon([(x * s, y * s) for x, y in outer], fill=rgba("#ff7a1a"), outline=OUTLINE, width=3 * s)
    d.polygon([(x * s, y * s) for x, y in inner], fill=rgba("#ffd84a"))
    return finish(img, ICON, ICON)


def wet_icon():
    img, d = icon_base("#0e2a52", "#3c8ce6")
    s = SS
    pts = [(32, 10)]
    for i in range(0, 181, 10):
        a = math.radians(i)
        pts.append((32 + 15 * math.cos(a), 38 + 15 * math.sin(a)))
    pts = pts[:1] + [(47, 38)] + pts[2:] + [(17, 38)]
    d.polygon([(x * s, y * s) for x, y in pts], fill=rgba("#4fa8ff"), outline=OUTLINE, width=3 * s)
    d.ellipse([24 * s, 34 * s, 30 * s, 44 * s], fill=rgba("#d6ecff"))
    return finish(img, ICON, ICON)


def stun_icon():
    img, d = icon_base("#3a2a14", "#b4874a")
    s = SS
    pts = []
    for i in range(10):
        r = 20 if i % 2 == 0 else 9
        a = math.radians(-90 + i * 36)
        pts.append((32 + r * math.cos(a), 33 + r * math.sin(a)))
    d.polygon([(x * s, y * s) for x, y in pts], fill=rgba("#ffd23c"), outline=OUTLINE, width=3 * s)
    d.ellipse([28 * s, 29 * s, 36 * s, 37 * s], fill=rgba("#fff3b0"))
    return finish(img, ICON, ICON)


def generate_status_icons():
    folder = os.path.join(ROOT, "Assets", "Resources", "StatusIcons")
    os.makedirs(folder, exist_ok=True)
    for name, fn in (("Burn", burn_icon), ("Wet", wet_icon), ("Stun", stun_icon)):
        fn().save(os.path.join(folder, f"{name}.png"))


# ---------------------------------------------------------------------------
# 마법 투사체 비행 애니메이션 / 적중 이펙트 (Assets/Resources/Effects/{Flight,Hit}/<속성>/NN.png)
# 런타임에 Resources에서 읽어 바로 쓰므로 Unity에서 Generate를 다시 돌릴 필요가 없다.
# 비행 프레임은 오른쪽을 향하고 중심이 투사체 위치다. 100px = 1유닛.

FX_FRAMES = 8


def ring(d, cx, cy, r, width, col):
    d.ellipse([cx - r, cy - r, cx + r, cy + r], outline=col, width=max(1, int(width)))


def dot(d, x, y, r, col):
    d.ellipse([x - r, y - r, x + r, y + r], fill=col)


def fire_flight():
    frames = []
    w, h = 120, 64
    for i in range(FX_FRAMES):
        rnd = random.Random(100 + i)
        img = blank(w, h)
        d = ImageDraw.Draw(img)
        cx, cy = 84 * SS, 32 * SS
        ph = i / FX_FRAMES * math.tau
        # 뒤로 흩날리는 불꽃 혀 3갈래
        for k, off in enumerate((-9, 0, 9)):
            pts = []
            for j in range(12):
                t = j / 11
                x = cx - t * (62 + 8 * math.sin(ph + k)) * SS
                y = cy + (off * (1 - t * 0.3) + math.sin(t * 6 + ph * 2 + k * 2) * 4 * t) * SS
                pts.append((x, y))
            for j, (x, y) in enumerate(pts):
                t = j / 11
                r = (12 - 10 * t) * SS
                col = (255, int(190 - 140 * t), int(50 - 40 * t), int(230 - 200 * t))
                dot(d, x, y, r, col)
        # 불씨
        for k in range(6):
            t = ((i / FX_FRAMES) + k / 6) % 1
            x = cx - (20 + t * 70) * SS
            y = cy + (rnd.uniform(-14, 14) - t * 6) * SS
            dot(d, x, y, (2.5 - 1.5 * t) * SS, (255, 230, 120, int(255 * (1 - t))))
        pulse = 1 + 0.1 * math.sin(ph * 2)
        for r, col in [(20, (220, 60, 25, 255)), (16, (255, 120, 35, 255)), (11, (255, 200, 80, 255)), (6, (255, 250, 220, 255))]:
            dot(d, cx, cy, r * SS * pulse, col)
        frames.append(finish(img, w, h, 3))
    return frames


def water_flight():
    frames = []
    w, h = 112, 48
    for i in range(FX_FRAMES):
        img = blank(w, h)
        d = ImageDraw.Draw(img)
        ph = i / FX_FRAMES * math.tau
        cx, cy = 80 * SS, 24 * SS
        # 물방울 몸통 (앞이 둥글고 뒤로 가늘어짐), 출렁임
        wob = math.sin(ph * 2) * 1.5
        body = []
        for a in range(-90, 91, 10):
            r = 14 + wob * math.cos(math.radians(a) * 2)
            body.append((cx + math.cos(math.radians(a)) * r * SS, cy + math.sin(math.radians(a)) * r * SS))
        body.append((cx - 60 * SS, cy + math.sin(ph) * 2 * SS))
        d.polygon(body, fill=(40, 120, 230, 235))
        inner = [(cx + (x - cx) * 0.62 + 3 * SS, cy + (y - cy) * 0.55) for x, y in body]
        d.polygon(inner, fill=(110, 190, 255, 255))
        dot(d, cx + 5 * SS, cy - 5 * SS, 3.5 * SS, (235, 248, 255, 255))
        # 감기는 물줄기 링
        for k in range(2):
            a = ph + k * math.pi
            x = cx - (18 + 22 * k) * SS
            ry = 12 * math.cos(a) * SS
            d.arc([x - 5 * SS, cy - abs(ry) - 1, x + 5 * SS, cy + abs(ry) + 1], 90 if ry > 0 else 270, 270 if ry > 0 else 450,
                  fill=(200, 235, 255, 200), width=2 * SS)
        # 뒤에 떨어지는 물방울
        for k in range(4):
            t = ((i / FX_FRAMES) + k / 4) % 1
            x = cx - (30 + t * 50) * SS
            y = cy + (t * 14 - 4 + k % 2 * 4) * SS
            dot(d, x, y, (3 - 2 * t) * SS, (150, 210, 255, int(230 * (1 - t))))
        frames.append(finish(img, w, h, 2))
    return frames


def wind_flight():
    frames = []
    w, h = 104, 72
    for i in range(FX_FRAMES):
        img = blank(w, h)
        d = ImageDraw.Draw(img)
        ph = i / FX_FRAMES
        cx, cy = 66 * SS, 36 * SS
        # 앞을 향한 초승달 칼날 2겹
        for k, (r, a, wd) in enumerate([(26, 235, 7), (19, 200, 4)]):
            jitter = math.sin(ph * math.tau * 2 + k) * 6
            d.arc([cx - r * SS - 14 * SS, cy - r * SS, cx + r * SS - 14 * SS, cy + r * SS], -70 + jitter, 70 + jitter,
                  fill=(225, 255, 235, a), width=wd * SS)
        # 흐르는 속도선
        for k in range(6):
            t = (ph + k / 6) % 1
            y = cy + (k - 2.5) * 7 * SS
            x0 = cx - (10 + t * 70) * SS
            ln = (24 - 14 * t) * SS
            d.line([(x0, y), (x0 - ln, y)], fill=(200, 255, 220, int(220 * (1 - t))), width=2 * SS)
        # 소용돌이 점
        for k in range(5):
            a = (ph + k / 5) * math.tau
            dot(d, cx - 8 * SS + math.cos(a) * 12 * SS, cy + math.sin(a) * 12 * SS, 2.2 * SS, (240, 255, 245, 230))
        frames.append(finish(img, w, h, 3))
    return frames


def arcane_flight():
    frames = []
    w = h = 72
    for i in range(FX_FRAMES):
        img = blank(w, h)
        d = ImageDraw.Draw(img)
        ph = i / FX_FRAMES * math.tau
        c = w / 2 * SS
        pulse = 1 + 0.12 * math.sin(ph * 2)
        for r, col in [(17, (110, 50, 210, 255)), (13, (165, 105, 255, 255)), (7, (240, 225, 255, 255))]:
            dot(d, c, c, r * SS * pulse, col)
        ring(d, c, c, 24 * SS, 2 * SS, (200, 160, 255, 160))
        for k in range(3):
            a = ph + k * math.tau / 3
            x, y = c + math.cos(a) * 24 * SS, c + math.sin(a) * 24 * SS
            d.polygon([(x, y - 5 * SS), (x + 3 * SS, y), (x, y + 5 * SS), (x - 3 * SS, y)], fill=(235, 205, 255, 255))
        for k in range(3):
            a = -ph * 1.5 + k * math.tau / 3
            dot(d, c + math.cos(a) * 30 * SS, c + math.sin(a) * 30 * SS, 2 * SS, (255, 240, 255, 200))
        frames.append(finish(img, w, h, 3))
    return frames


def fire_hit():
    frames = []
    w = h = 140
    rnd = random.Random(11)
    petals = [(rnd.uniform(0, 360), rnd.uniform(0.7, 1.0)) for _ in range(9)]
    for i in range(FX_FRAMES):
        t = i / (FX_FRAMES - 1)
        img = blank(w, h)
        d = ImageDraw.Draw(img)
        c = w / 2 * SS
        fade = 1 - max(0, t - 0.4) / 0.6
        # 연기
        for k in range(6):
            a = math.radians(k * 60 + 20)
            r = (20 + 40 * t) * SS
            dot(d, c + math.cos(a) * r, c + math.sin(a) * r - 10 * t * SS, (10 + 14 * t) * SS, (70, 60, 60, int(140 * fade * t)))
        # 불꽃 꽃잎
        for a, s in petals:
            rad = math.radians(a)
            ln = (14 + 46 * math.sqrt(t)) * s * SS
            x, y = c + math.cos(rad) * ln, c + math.sin(rad) * ln
            dot(d, x, y, (12 - 8 * t) * s * SS, (255, int(160 - 80 * t), 40, int(255 * fade)))
        core = (26 * (1 - t) + 6) * SS
        dot(d, c, c, core, (255, 120, 40, int(255 * fade)))
        dot(d, c, c, core * 0.6, (255, 230, 150, int(255 * fade)))
        ring(d, c, c, (12 + 52 * t) * SS, (8 - 6 * t) * SS, (255, 200, 90, int(220 * (1 - t))))
        frames.append(finish(img, w, h, 3))
    return frames


def water_hit():
    frames = []
    w = h = 140
    rnd = random.Random(12)
    drops = [(rnd.uniform(200, 340), rnd.uniform(55, 85)) for _ in range(10)]
    for i in range(FX_FRAMES):
        t = i / (FX_FRAMES - 1)
        img = blank(w, h)
        d = ImageDraw.Draw(img)
        c = w / 2 * SS
        ring(d, c, c, (10 + 50 * t) * SS, (7 - 5 * t) * SS, (120, 200, 255, int(230 * (1 - t))))
        ring(d, c, c, (6 + 30 * t) * SS, (4 - 3 * t) * SS, (220, 245, 255, int(200 * (1 - t))))
        for a, sp in drops:
            rad = math.radians(a)
            x = c + math.cos(rad) * sp * t * SS
            y = c + math.sin(rad) * sp * t * SS + 60 * t * t * SS  # 포물선으로 떨어짐
            dot(d, x, y, (5 - 3 * t) * SS, (90, 170, 255, int(255 * (1 - t * 0.8))))
            dot(d, x - 1 * SS, y - 1 * SS, (2 - t) * SS, (230, 245, 255, int(255 * (1 - t))))
        if t < 0.4:
            dot(d, c, c, (16 - 30 * t) * SS, (180, 225, 255, 220))
        frames.append(finish(img, w, h, 2))
    return frames


def wind_hit():
    frames = []
    w = h = 140
    for i in range(FX_FRAMES):
        t = i / (FX_FRAMES - 1)
        img = blank(w, h)
        d = ImageDraw.Draw(img)
        c = w / 2 * SS
        for k in range(3):
            r = (14 + 18 * k + 26 * t) * SS
            start = t * 300 + k * 120
            d.arc([c - r, c - r, c + r, c + r], start, start + 140 - 60 * t,
                  fill=(225, 255, 235, int((230 - 50 * k) * (1 - t))), width=int((6 - k * 1.5) * SS))
        for k in range(8):
            a = math.radians(k * 45 + t * 90)
            r0, r1 = (10 + 40 * t) * SS, (22 + 50 * t) * SS
            d.line([(c + math.cos(a) * r0, c + math.sin(a) * r0), (c + math.cos(a) * r1, c + math.sin(a) * r1)],
                   fill=(200, 255, 220, int(200 * (1 - t))), width=2 * SS)
        frames.append(finish(img, w, h, 3))
    return frames


def earth_hit():
    frames = []
    w = h = 140
    rnd = random.Random(14)
    rocks = [(rnd.uniform(190, 350), rnd.uniform(50, 80), rnd.uniform(4, 8), rnd.uniform(0, 360)) for _ in range(9)]
    for i in range(FX_FRAMES):
        t = i / (FX_FRAMES - 1)
        img = blank(w, h)
        d = ImageDraw.Draw(img)
        c = w / 2 * SS
        # 먼지 구름
        for k in range(7):
            a = math.radians(k * 51)
            r = (10 + 34 * t) * SS
            dot(d, c + math.cos(a) * r, c + math.sin(a) * r * 0.6 + 8 * SS, (12 + 10 * t) * SS,
                (160, 130, 90, int(170 * (1 - t))))
        # 튀는 돌조각
        for a, sp, size, rot in rocks:
            rad = math.radians(a)
            x = c + math.cos(rad) * sp * t * SS
            y = c + math.sin(rad) * sp * t * SS + 70 * t * t * SS
            pts = []
            for j in range(5):
                aa = math.radians(rot + t * 360 + j * 72)
                rr = size * (1 if j % 2 else 0.7) * SS
                pts.append((x + math.cos(aa) * rr, y + math.sin(aa) * rr))
            d.polygon(pts, fill=(140, 105, 65, int(255 * (1 - t * 0.7))), outline=(40, 30, 20, int(255 * (1 - t * 0.7))))
        if t < 0.35:
            star = []
            for j in range(12):
                rr = (30 if j % 2 == 0 else 12) * (1 - t) * SS
                aa = math.radians(j * 30)
                star.append((c + math.cos(aa) * rr, c + math.sin(aa) * rr))
            d.polygon(star, fill=(230, 200, 140, 230))
        frames.append(finish(img, w, h, 2))
    return frames


def arcane_hit():
    frames = []
    w = h = 140
    for i in range(FX_FRAMES):
        t = i / (FX_FRAMES - 1)
        img = blank(w, h)
        d = ImageDraw.Draw(img)
        c = w / 2 * SS
        ring(d, c, c, (10 + 50 * t) * SS, (6 - 4 * t) * SS, (190, 140, 255, int(230 * (1 - t))))
        star = []
        for j in range(16):
            rr = (40 if j % 2 == 0 else 12) * (0.4 + 0.8 * t) * SS
            aa = math.radians(j * 22.5 + t * 40)
            star.append((c + math.cos(aa) * rr, c + math.sin(aa) * rr))
        d.polygon(star, fill=(200, 160, 255, int(220 * (1 - t))))
        dot(d, c, c, (14 * (1 - t) + 3) * SS, (250, 240, 255, int(255 * (1 - t))))
        for k in range(10):
            a = math.radians(k * 36 + 15)
            r = (20 + 45 * t) * SS
            x, y = c + math.cos(a) * r, c + math.sin(a) * r
            s2 = 4 * (1 - t) * SS
            d.polygon([(x, y - s2), (x + s2 / 2, y), (x, y + s2), (x - s2 / 2, y)], fill=(240, 220, 255, int(255 * (1 - t))))
        frames.append(finish(img, w, h, 3))
    return frames


def generate_spell_fx():
    base = os.path.join(ROOT, "Assets", "Resources", "Effects")
    sets = {
        ("Flight", "Fire"): fire_flight(),
        ("Flight", "Water"): water_flight(),
        ("Flight", "Wind"): wind_flight(),
        ("Flight", "Neutral"): arcane_flight(),
        ("Hit", "Fire"): fire_hit(),
        ("Hit", "Water"): water_hit(),
        ("Hit", "Wind"): wind_hit(),
        ("Hit", "Earth"): earth_hit(),
        ("Hit", "Neutral"): arcane_hit(),
    }
    for (kind, element), frames in sets.items():
        folder = os.path.join(base, kind, element)
        os.makedirs(folder, exist_ok=True)
        for i, img in enumerate(frames):
            img.save(os.path.join(folder, f"{i:02d}.png"))
    return sets


# ---------------------------------------------------------------------------
# 속성 슬라임용 회색 프레임 (Assets/Resources/Enemies/SlimeGray/<원본 파일명>.png)
# 원본(초록) 슬라임 프레임의 밝기만 남겨 회색으로 만든다. 게임에서 이 회색 프레임에 속성 색을 곱해
# 불/물/바람/땅/무 슬라임을 만든다 (ElementRecolor). 원본 파일은 건드리지 않는다.

def slime_gray(src):
    img = Image.open(src).convert("RGBA")
    px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            lum = (0.299 * r + 0.587 * g + 0.114 * b) / 255
            # 외곽선(아주 어두운 부분)은 그대로 어둡게, 몸통은 밝게 올려서 색을 곱했을 때 잘 보이게 한다.
            v = lum if lum < 0.12 else min(1.0, 0.35 + (lum / 0.62) ** 0.8 * 0.65)
            c = int(v * 255)
            px[x, y] = (c, c, c, a)
    return img


def generate_slime_gray():
    src_root = os.path.join(SPRITES, "Enemies", "Slime")
    dst = os.path.join(ROOT, "Assets", "Resources", "Enemies", "SlimeGray")
    os.makedirs(dst, exist_ok=True)
    for folder, _, files in os.walk(src_root):
        for name in sorted(files):
            if name.endswith(".png"):
                slime_gray(os.path.join(folder, name)).save(os.path.join(dst, name))


if __name__ == "__main__":
    import sys

    generate_goblins()
    generate_effects()
    generate_status_icons()
    generate_spell_fx()
    generate_slime_gray()
    if len(sys.argv) > 1:
        contact_sheet(sys.argv[1])
    print("done")
