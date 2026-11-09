"""I Blame You! 슬라임 원본 프레임에 귀여운 얼굴을 얹는다 (Docs/ArtStyle.md: 큰 눈, 반짝임 점, 볼터치).

원본 실루엣·프레임 수·캔버스 크기·피벗은 그대로 두고, 프레임마다 몸 영역(알파)을 재서 눈 위치를 정한다.
원본은 Trash/Assets/Art/Sprites/Enemies/Slime 에 보존하고 거기서 읽어 Assets/Art/Sprites/Enemies/Slime 에 쓴다
(여러 번 돌려도 눈이 겹치지 않게). 같은 .png 이름이라 .meta(GUID)와 애니메이션 클립은 그대로 이어진다.

    python3 Tools/ArtGen/decorate_slime.py [미리보기.png]
"""
import glob
import os
import sys

from PIL import Image, ImageDraw

sys.path.insert(0, os.path.dirname(__file__))
from palette import COLORS, OUTLINE  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "Trash", "Assets", "Art", "Sprites", "Enemies", "Slime")
TARGET = os.path.join(ROOT, "Assets", "Art", "Sprites", "Enemies", "Slime")
SS = 4


def body_box(image):
    alpha = image.getchannel("A").point(lambda a: 255 if a > 128 else 0)
    return alpha.getbbox()


def face(image, mode):
    """mode: 'open'(기본), 'x'(쓰러지는 중), None(얼굴 없음)."""
    box = body_box(image)
    if box is None or mode is None:
        return image
    left, top, right, bottom = box
    w, h = right - left, bottom - top
    if h < 18 or h > w * 1.05:  # 너무 납작하거나(녹아내림) 세로로 길게 늘어난(찌르기) 프레임은 얼굴 생략
        return image

    big = image.resize((image.width * SS, image.height * SS), Image.NEAREST)
    d = ImageDraw.Draw(big)
    r = max(4.0, min(8.5, h * 0.13))
    cy = bottom - h * 0.45
    cx = left + w * 0.6  # 오른쪽을 보므로 얼굴을 살짝 앞쪽으로
    gap = r * 1.35

    def S(*v):
        return [x * SS for x in v]

    for ex in (cx - gap, cx + gap):
        if mode == "x":
            for dx, dy in ((1, 1), (1, -1)):
                d.line(S(ex - r * 0.7 * dx, cy - r * 0.7 * dy, ex + r * 0.7 * dx, cy + r * 0.7 * dy), fill=OUTLINE, width=int(2.2 * SS))
            continue
        d.ellipse(S(ex - r - 1.5, cy - r * 1.15 - 1.5, ex + r + 1.5, cy + r * 1.15 + 1.5), fill=OUTLINE)
        d.ellipse(S(ex - r, cy - r * 1.15, ex + r, cy + r * 1.15), fill=(255, 255, 255, 255))
        pr = r * 0.68
        px, py = ex + r * 0.25, cy + r * 0.1
        d.ellipse(S(px - pr, py - pr * 1.1, px + pr, py + pr * 1.1), fill=OUTLINE)
        d.ellipse(S(px - pr * 0.55, py - pr * 0.8, px - pr * 0.05, py - pr * 0.3), fill=COLORS["cream"])
        d.ellipse(S(px + pr * 0.2, py + pr * 0.25, px + pr * 0.45, py + pr * 0.5), fill=COLORS["cream"])

    # 볼터치와 작은 입
    blush = COLORS["berry"][:3] + (130,)
    for bx in (cx - gap * 1.9, cx + gap * 1.9):
        d.ellipse(S(bx - r * 0.7, cy + r * 1.1, bx + r * 0.7, cy + r * 1.55), fill=blush)
    if mode == "open":
        d.arc(S(cx - r * 0.45, cy + r * 0.7, cx + r * 0.45, cy + r * 1.4), 20, 160, fill=OUTLINE, width=int(1.6 * SS))

    # 몸 왼쪽 위 반짝임 별 (화려하게)
    sx, sy, sr = left + w * 0.24, top + h * 0.22, max(2.5, r * 0.45)
    k = 0.25
    d.polygon([(x * SS, y * SS) for x, y in [(sx, sy - sr), (sx + sr * k, sy - sr * k), (sx + sr, sy), (sx + sr * k, sy + sr * k),
                                             (sx, sy + sr), (sx - sr * k, sy + sr * k), (sx - sr, sy), (sx - sr * k, sy - sr * k)]],
              fill=(255, 255, 255, 235))
    out = big.resize(image.size, Image.LANCZOS)
    # 몸 밖으로 나간 픽셀은 원래 알파로 자른다 (볼터치가 윤곽 밖으로 번지지 않게)
    mask = image.getchannel("A")
    out.putalpha(Image.composite(out.getchannel("A"), mask, mask.point(lambda a: 255 if a > 0 else 0)))
    return out


def mode_for(anim, index):
    if anim == "Die":
        return "x" if index <= 2 else None
    return "open"


def generate():
    result = {}
    for anim in ("Idle", "Run", "Attack", "Die"):
        frames = []
        for i, path in enumerate(sorted(glob.glob(os.path.join(SOURCE, anim, "*.png")))):
            image = Image.open(path).convert("RGBA")
            out = face(image, mode_for(anim, i))
            out.save(os.path.join(TARGET, anim, os.path.basename(path)))
            frames.append(out)
        result[anim] = frames
    return result


def preview(result, path):
    cell_w, cell_h = 130, 100
    image = Image.new("RGBA", (cell_w * 10, cell_h * 4), COLORS["dusk"])
    for r, frames in enumerate(result.values()):
        for c, f in enumerate(frames):
            image.alpha_composite(f, (c * cell_w, r * cell_h + cell_h - f.height))
    image.save(path)


if __name__ == "__main__":
    if not os.path.isdir(SOURCE):
        sys.exit(f"원본이 없습니다: {SOURCE}")
    out = generate()
    if len(sys.argv) > 1:
        preview(out, sys.argv[1])
    print("done")
