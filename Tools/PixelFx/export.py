"""pixelfx 이펙트를 게임 프레임 PNG 폴더로 내보낸다 (2026-11-25, 아트 규칙: 모든 그림은 pixelfx).

    python3 Tools/PixelFx/export.py <pixelfx 체크아웃 경로> [이름 ...]

recipes.json의 항목마다: pixelfx `tools/render.mjs <id> --sheet --opts ...`로 시트를 만들고,
[start, end) 구간을 게임 재생 속도(fps)로 다시 뽑아, 정수배(scale) 최근접 확대 후
pixelfx 피벗이 게임 피벗(center = 가운데, bottom = 아래 가운데)에 오도록 여백을 붙여 dest/NN.png로 쓴다.
모든 프레임의 그림 영역 합집합으로 잘라 캔버스를 최소로 한다. 루프는 구간을 이음새 없이 나눈다.

새로 생긴 PNG에는 .meta를 붙이고(안 붙이면 NPOT 그림이 찌그러진다) 픽셀 그림이라 필터를 Point로:
    python3 Tools/Dev/unity_meta.py add $(find Assets/Resources/Effects/Px -name "*.png")
    find Assets/Resources/Effects/Px -name "*.png.meta" -exec sed -i 's/^    filterMode: 1$/    filterMode: 0/' {} +
프레임 수가 줄면 남는 옛 프레임을 알려 준다. 지우지 말고 Trash/로 옮길 것 (CLAUDE.md).

pixelfx 체크아웃: git clone https://github.com/patch-gg/pixelfx (저장소에 넣지 않는다)
"""
import json, os, subprocess, sys
from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def render(pfx, rid, opts):
    cmd = ["node", "tools/render.mjs", rid, "--sheet"]
    if opts:
        cmd += ["--opts", json.dumps(opts)]
    subprocess.run(cmd, cwd=pfx, check=True, stdout=subprocess.DEVNULL)
    base = os.path.join(pfx, "records", rid, rid.split("/")[-1])
    meta = json.load(open(base + ".json"))
    sheet = Image.open(base + ".png").convert("RGBA")
    frames = [sheet.crop((f["frame"]["x"], f["frame"]["y"], f["frame"]["x"] + f["frame"]["w"], f["frame"]["y"] + f["frame"]["h"]))
              for f in meta["frames"]]
    return frames, meta["meta"]["pixelfx"]


def export(pfx, r):
    frames, m = render(pfx, r["id"], r.get("opts"))
    src_fps, n_src = m["fps"], m["frames"]
    dur = n_src / src_fps
    start = r.get("start", 0.0)
    end = min(r.get("end", dur), dur)
    fps = r["fps"]
    count = max(1, round((end - start) * fps))
    picked = [frames[min(n_src - 1, int(round((start + i * (end - start) / count) * src_fps)) % n_src)] for i in range(count)]
    if r.get("blank_last"):
        picked.append(Image.new("RGBA", picked[0].size))
    # 알파가 아주 옅은 빛 번짐은 버린다 (어두운 배경 전제의 광택이라 게임 배경 위에선 뿌연 사각형이 됨)
    cut = r.get("alpha_cut", 24)
    clip = r.get("clip")  # [x0, y0, x1, y1] 원본 좌표: 밖은 지운다 (떠 있는 구슬 밑 땅 빛 등)
    cleaned = []
    for im in picked:
        a = im.getchannel("A").point(lambda v: 0 if v < cut else v)
        if clip:
            mask = Image.new("L", im.size, 0); mask.paste(255, tuple(clip))
            a = Image.composite(a, mask, mask)
        im = im.copy(); im.putalpha(a); cleaned.append(im)
    boxes = [im.getbbox() for im in cleaned if im.getbbox()]
    x0 = min(b[0] for b in boxes); y0 = min(b[1] for b in boxes); x1 = max(b[2] for b in boxes); y1 = max(b[3] for b in boxes)
    anchor = r.get("anchor")  # 게임 피벗에 올 점: 없으면 pixelfx 피벗, "bbox"면 그림 영역 가운데, [x, y]면 그 점
    if anchor == "bbox":
        px, py = (x0 + x1) // 2, (y0 + y1) // 2
    elif anchor:
        px, py = anchor
    else:
        px, py = m["pivot"]
    if r.get("anchor_x") == "bbox":
        px = (x0 + x1) // 2
    hx = max(px - x0, x1 - px)
    if r.get("pivot", "center") == "center":
        hy = max(py - y0, y1 - py)
        box = (px - hx, py - hy, px + hx, py + hy)
    else:  # bottom: 피벗 y가 맨 아래 (땅 아래로 그린 금·그림자는 잘린다)
        box = (px - hx, y0, px + hx, py)
    s = r.get("scale", 2)
    dest = os.path.join(ROOT, r["dest"])
    os.makedirs(dest, exist_ok=True)
    prefix = r.get("prefix", "")
    for i, im in enumerate(cleaned):
        c = im.crop(box)
        c = c.resize((c.width * s, c.height * s), Image.NEAREST)
        if r.get("flip_y"):
            c = c.transpose(Image.FLIP_TOP_BOTTOM)
        c.save(os.path.join(dest, f"{prefix}{i:02d}.png"))
    stale = sorted(f for f in os.listdir(dest) if f.endswith(".png") and f >= f"{prefix}{len(cleaned):02d}.png" and f.startswith(prefix))
    if stale:
        print(f'  ! {r["dest"]}: 이번에 안 쓰는 옛 프레임 {stale} — Trash/로 옮길 것')
    print(f'{r["name"]:16s} {r["id"]:26s} {len(cleaned):3d}f {c.width}x{c.height}px -> {r["dest"]}')


def main():
    pfx = sys.argv[1]
    only = set(sys.argv[2:])
    recipes = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "recipes.json")))
    for r in recipes:
        if not only or r["name"] in only:
            export(pfx, r)


if __name__ == "__main__":
    main()
