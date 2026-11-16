"""I Blame You! 두 번째 중간보스 프리즘 젤리 그림 (Docs/MidBossDraft.md 후보 D).

회색 슬라임 프레임(Assets/Resources/Enemies/SlimeGray, 이름·크기·피벗 그대로)을 수정 젤리로 바꿔
Assets/Resources/Enemies/PrismJelly 에 쓴다. ElementRecolor가 원본 이름으로 바꿔 끼우고 지금 색(속성)을 곱한다.
- 눈 없음 (2026-11-12 사람 지시)
- 몸 속은 밝고 반투명 (외곽선은 그대로 진하게) → 색을 곱하면 맑은 젤리
- 몸 안 비스듬한 굴절 빛줄기 두 줄 + 테두리 안쪽 무지개 띠(밝은 파스텔이라 색을 곱해도 무늬가 남는다) + 반짝임 별
같은 입력이면 같은 그림 (난수 없음).

    python3 Tools/ArtGen/generate_prism_jelly.py [미리보기.png]
"""
import glob
import math
import os
import sys

from PIL import Image

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOURCE = os.path.join(ROOT, "Assets", "Resources", "Enemies", "SlimeGray")
TARGET = os.path.join(ROOT, "Assets", "Resources", "Enemies", "PrismJelly")

RAINBOW = [(255, 190, 190), (255, 225, 175), (255, 250, 185), (195, 255, 205), (185, 225, 255), (220, 200, 255)]


def edge_distance(alpha, w, h):
    """몸 안 각 픽셀이 바깥(투명)까지 몇 칸인지 (최대 6, 맨해튼 BFS)."""
    INF = 99
    dist = [[INF] * w for _ in range(h)]
    frontier = []
    for y in range(h):
        for x in range(w):
            if alpha[y][x] <= 128:
                dist[y][x] = 0
                frontier.append((x, y))
    for step in range(1, 7):
        nxt = []
        for x, y in frontier:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                nx, ny = x + dx, y + dy
                if 0 <= nx < w and 0 <= ny < h and dist[ny][nx] == INF:
                    dist[ny][nx] = step
                    nxt.append((nx, ny))
        frontier = nxt
    return dist


def crystal(img):
    img = img.convert("RGBA")
    w, h = img.size
    px = img.load()
    alpha = [[px[x, y][3] for x in range(w)] for y in range(h)]
    box = img.getchannel("A").point(lambda a: 255 if a > 128 else 0).getbbox()
    if box is None:
        return img
    left, top, right, bottom = box
    bw, bh = max(1, right - left), max(1, bottom - top)
    dist = edge_distance(alpha, w, h)
    out = Image.new("RGBA", (w, h))
    op = out.load()
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            v = r  # 회색판이라 r = g = b
            d = dist[y][x]
            if v < 40 or d <= 1:  # 외곽선
                op[x, y] = (r, g, b, a)
                continue
            # 몸 속: 밝게, 살짝 반투명
            c = min(255, int(v * 0.55 + 120))
            col = (c, c, c)
            na = int(a * 0.86)
            # 테두리 안쪽 무지개 띠 (가장자리에서 2~3칸, 둘레를 따라 색이 돈다)
            if 2 <= d <= 3:
                ang = math.atan2(y - (top + bh * 0.55), x - (left + bw * 0.5))
                i = int(((ang + math.pi) / (2 * math.pi)) * len(RAINBOW) * 2) % len(RAINBOW)
                col = RAINBOW[i]
                na = a
            # 비스듬한 굴절 빛줄기 두 줄 (오른쪽 위 → 왼쪽 아래)
            u = (x - left) / bw + (y - top) / bh * 0.6
            if abs(u - 0.55) < 0.035 or abs(u - 0.78) < 0.02:
                col = (255, 255, 255)
                na = a
            op[x, y] = col + (na,)
    # 왼쪽 위 반짝임 별 (십자 + 가운데)
    sx, sy = int(left + bw * 0.27), int(top + bh * 0.3)
    arm = max(2, int(min(bw, bh) * 0.08))
    for k in range(-arm, arm + 1):
        for (x, y) in ((sx + k, sy), (sx, sy + k)):
            if 0 <= x < w and 0 <= y < h and op[x, y][3] > 0:
                op[x, y] = (255, 255, 255, 255)
    return out


def generate():
    os.makedirs(TARGET, exist_ok=True)
    frames = []
    for path in sorted(glob.glob(os.path.join(SOURCE, "*.png"))):
        image = crystal(Image.open(path))
        image.save(os.path.join(TARGET, os.path.basename(path)))
        frames.append((os.path.basename(path), image))
    return frames


def preview(frames, path):
    tints = [(255, 115, 38), (64, 153, 255), (140, 255, 178), (191, 140, 77)]  # ElementInfo.Color 불·물·바람·땅
    picks = [f for n, f in frames if n in ("slime_idle_00.png", "slime_attack_04.png", "slime_hurt_00.png")]
    cw, ch = max(f.width for f in picks), max(f.height for f in picks)
    sheet = Image.new("RGBA", (cw * len(picks) * 2, ch * len(tints) * 2), (40, 40, 52, 255))
    for row, tint in enumerate(tints):
        for col, frame in enumerate(picks):
            r, g, b, a = frame.split()
            tinted = Image.merge("RGBA", (r.point(lambda v: v * tint[0] // 255), g.point(lambda v: v * tint[1] // 255),
                                          b.point(lambda v: v * tint[2] // 255), a))
            big = tinted.resize((frame.width * 2, frame.height * 2), Image.NEAREST)
            sheet.alpha_composite(big, (col * cw * 2, row * ch * 2))
    sheet.save(path)


if __name__ == "__main__":
    result = generate()
    print(f"{len(result)}장 → {TARGET}")
    if len(sys.argv) > 1:
        preview(result, sys.argv[1])
