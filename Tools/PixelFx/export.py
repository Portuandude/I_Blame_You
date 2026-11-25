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
    gray = r.get("gray")  # 회색판 (속성 색을 곱해 쓰는 ElementRecolor용): 밝기만 남긴다
    start = r.get("start", 0.0)
    end = min(r.get("end", dur), dur)
    fps = r["fps"]
    times = r.get("times")  # 프레임마다 뽑을 시각(초)을 직접 정할 때 (애니메이션 클립 구간에 맞출 때)
    if not times:
        count = max(1, round((end - start) * fps))
        times = [start + i * (end - start) / count for i in range(count)]
    picked = [frames[min(n_src - 1, int(round(tt * src_fps)) % n_src)] for tt in times]
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
    canvas = r.get("canvas")  # [w, h]: 원래 스프라이트와 같은 고정 캔버스 (아래 가운데 = 피벗). 넘치는 부분은 잘린다
    if canvas:
        px, py = m["pivot"]
        # canvas_pivot: 출력 캔버스에서 pixelfx 피벗(발밑)이 올 픽셀 (없으면 아래 가운데). scale로 나눠떨어지게 잡는다
        cpx, cpy = r.get("canvas_pivot", [canvas[0] // 2, canvas[1]])
        lx, ty = cpx // s, cpy // s
        box = (px - lx, py - ty, px - lx + -(-canvas[0] // s), py - ty + -(-canvas[1] // s))
    dest = os.path.join(ROOT, r["dest"])
    os.makedirs(dest, exist_ok=True)
    prefix = r.get("prefix", "")
    for i, im in enumerate(cleaned):
        c = im.crop(box)
        c = c.resize((c.width * s, c.height * s), Image.NEAREST)
        if canvas:  # 피벗을 맞춘 채 정확한 크기로 (오른쪽·아래 넘친 부분을 자른다)
            c = c.crop((0, 0, canvas[0], canvas[1]))
        if r.get("flip_y"):
            c = c.transpose(Image.FLIP_TOP_BOTTOM)
        if r.get("gray"):
            a_ = c.getchannel("A"); c = c.convert("L").convert("RGBA"); c.putalpha(a_)
        name = r["file"] if r.get("file") else f"{prefix}{i:02d}.png"  # file: 한 장짜리 그림의 정해진 이름
        c.save(os.path.join(dest, name))
    stale = sorted(f for f in os.listdir(dest) if f.endswith(".png") and f >= f"{prefix}{len(cleaned):02d}.png" and f.startswith(prefix))
    if stale:
        print(f'  ! {r["dest"]}: 이번에 안 쓰는 옛 프레임 {stale} — Trash/로 옮길 것')
    print(f'{r["name"]:16s} {r["id"]:26s} {len(cleaned):3d}f {c.width}x{c.height}px -> {r["dest"]}')


def install_custom(pfx):
    """Tools/PixelFx/fx/ 의 이 게임 전용 이펙트(예: 눈 없는 슬라임)를 pixelfx 체크아웃 src/fx/ 에 복사한다."""
    import shutil
    here = os.path.join(os.path.dirname(os.path.abspath(__file__)), "fx")
    for d, _, files in os.walk(here):
        for f in files:
            if f.endswith(".js"):
                rel = os.path.relpath(os.path.join(d, f), here)
                shutil.copyfile(os.path.join(d, f), os.path.join(pfx, "src", "fx", rel))


def main():
    pfx = sys.argv[1]
    install_custom(pfx)
    only = set(sys.argv[2:])
    recipes = json.load(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "recipes.json")))
    for r in recipes:
        if not only or r["name"] in only:
            export(pfx, r)


if __name__ == "__main__":
    main()
