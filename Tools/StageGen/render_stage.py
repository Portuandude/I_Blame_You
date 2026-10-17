"""StagePreview.cs가 만든 텍스트 지도를 PNG로 그린다.

    python3 Tools/StageGen/render_stage.py <지도.txt> <출력.png> [줄당 청크 수]

긴 스테이지를 여러 줄로 접어서 그린다. 청크 경계에 종류 이름을 적는다.
"""
import sys

from PIL import Image, ImageDraw, ImageFont

CELL = 8
CHUNK = 32
COLORS = {
    "#": (92, 78, 66), "=": (196, 160, 98), "~": (150, 110, 70), "^": (220, 220, 230),
    "F": (255, 120, 40), "<": (180, 60, 60), ">": (180, 60, 60), "C": (90, 220, 255),
    "P": (80, 255, 120), "X": (255, 240, 90),
}
ENEMY = {"s": (110, 200, 110), "g": (90, 160, 60), "a": (60, 140, 160), "o": (150, 150, 60),
         "m": (190, 90, 220), "w": (220, 60, 60)}
KIND_TINT = {"Arena": (60, 34, 40), "EliteArena": (80, 40, 30), "Boss": (90, 24, 24), "Rest": (28, 52, 64),
             "Cave": (26, 26, 34), "Start": (36, 48, 40)}
SKY = (34, 38, 52)
KIND_KO = {"Start": "시작", "Plains": "평지", "Hills": "언덕", "Pits": "낭떠러지", "SpikeField": "가시밭", "Climb": "절벽",
           "Cave": "동굴", "CrumbleBridge": "무너지는 다리", "Arena": "전투", "EliteArena": "정예 전투", "Rest": "휴식",
           "Boss": "보스"}


def load_font(size):
    for path in ("/usr/share/fonts/truetype/nanum/NanumGothicBold.ttf", "/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc",
                 "/usr/share/fonts/truetype/noto/NotoSansCJK-Bold.ttc"):
        try:
            return ImageFont.truetype(path, size)
        except OSError:
            continue
    return ImageFont.load_default()


def main():
    src, dst = sys.argv[1], sys.argv[2]
    per_row = int(sys.argv[3]) if len(sys.argv) > 3 else 11
    lines = open(src, encoding="utf-8").read().splitlines()
    kinds = lines[0].split(",")
    grid = lines[1:]
    height = len(grid)
    chunks = len(kinds)
    rows = (chunks + per_row - 1) // per_row
    band = height * CELL + 22
    img = Image.new("RGB", (per_row * CHUNK * CELL, rows * band), (16, 16, 20))
    d = ImageDraw.Draw(img)
    font = load_font(13)

    for ci, kind in enumerate(kinds):
        ox = (ci % per_row) * CHUNK * CELL
        oy = (ci // per_row) * band + 20
        tint = KIND_TINT.get(kind, SKY)
        d.rectangle([ox, oy, ox + CHUNK * CELL - 1, oy + height * CELL - 1], fill=tint)
        for y in range(height):
            row = grid[y]
            for x in range(CHUNK):
                ch = row[ci * CHUNK + x]
                if ch == ".":
                    continue
                px, py = ox + x * CELL, oy + y * CELL
                if ch in COLORS:
                    if ch == "=" or ch == "~":
                        d.rectangle([px, py, px + CELL - 1, py + 2], fill=COLORS[ch])
                    elif ch == "^":
                        d.polygon([(px, py + CELL - 1), (px + CELL // 2, py + 1), (px + CELL - 1, py + CELL - 1)], fill=COLORS[ch])
                    else:
                        d.rectangle([px, py, px + CELL - 1, py + CELL - 1], fill=COLORS[ch])
                elif ch.lower() in ENEMY:
                    color = ENEMY[ch.lower()]
                    d.ellipse([px, py - CELL // 2, px + CELL - 1, py + CELL - 1], fill=color,
                              outline=(255, 255, 255) if ch.isupper() else None)
        d.line([ox, oy, ox, oy + height * CELL], fill=(70, 70, 90))
        d.text((ox + 3, oy - 17), f"{ci + 1}. {KIND_KO.get(kind, kind)}", fill=(230, 230, 230), font=font)

    img.save(dst)


if __name__ == "__main__":
    main()
