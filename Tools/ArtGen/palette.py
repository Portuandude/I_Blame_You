"""I Blame You! 아트 팔레트 ("귀엽지만 화려하게").

Docs/ArtStyle.md 의 팔레트를 코드로 옮긴 것. 새로 그리는 그림(generate_sprites.py, 배경, 타일, 이펙트)은
여기 색만 쓴다. 직접 실행하면 Docs/Images/ArtPalette.png 견본을 다시 만든다.

    python3 Tools/ArtGen/palette.py
"""
import os

from PIL import Image, ImageDraw, ImageFont


def hexrgba(value, alpha=255):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4)) + (alpha,)


# 메인 12색 (이름, 16진수)
MAIN = [
    ("outline", "18141a"),     # 외곽선 (마법사 Idle 원본의 검정에 맞춘 아주 진한 자주빛 검정)
    ("night", "2a2150"),       # 먼 하늘·그림자
    ("dusk", "5a46a8"),        # 중간 하늘·먼 산
    ("lilac", "a68cf0"),       # 하늘 밝은 쪽·먼 빛
    ("leaf_dark", "2c7a3a"),   # 풀 그림자
    ("leaf", "4fc24a"),        # 풀·슬라임 기본
    ("leaf_light", "b2f26a"),  # 풀 윗면·하이라이트
    ("soil_dark", "4a2a1c"),   # 흙 그림자
    ("soil", "8f5a2e"),        # 흙·나무 (마법사 망토 갈색 계열)
    ("sand", "e8b878"),        # 흙 윗면·나무 밝은 쪽
    ("cream", "fff4d6"),       # 하이라이트·눈 반짝임
    ("berry", "ff6fae"),       # 귀여운 포인트 (볼터치, 꽃, 하트)
]

# 반짝임 공용 2색
SPARKLE = [
    ("gold", "ffd84d"),        # 별·레벨업·유물 (UI 노란색과 같음)
    ("aqua", "7ff6ff"),        # 마법 테두리·반짝이 입자
]

# 속성별 (어두운 쪽, 기본, 밝은 심지). 기본색은 ElementInfo.Color와 맞춘다.
ELEMENTS = {
    "fire": ("c2361c", "ff7326", "ffe066"),
    "water": ("1f5fd1", "4099ff", "c8f4ff"),
    "wind": ("2fae74", "8cffb2", "f0fff6"),
    "earth": ("7a4a20", "bf8c4d", "ffe2a8"),
    "arcane": ("7a4ad8", "d9bfff", "fff0ff"),
}

OUTLINE = hexrgba(MAIN[0][1])
COLORS = {name: hexrgba(value) for name, value in MAIN + SPARKLE}


def element(name, tone=1):
    """속성 색. tone 0=어두운 쪽, 1=기본, 2=밝은 심지."""
    return hexrgba(ELEMENTS[name][tone])


def swatch(path):
    cell = 64
    rows = [MAIN, SPARKLE] + [[(f"{k}:{i}", v) for i, v in enumerate(ELEMENTS[k])] for k in ELEMENTS]
    width = cell * max(len(r) for r in rows)
    height = (cell + 16) * len(rows)
    image = Image.new("RGBA", (width, height), (255, 255, 255, 255))
    draw = ImageDraw.Draw(image)
    font = ImageFont.load_default()
    for r, row in enumerate(rows):
        for c, (name, value) in enumerate(row):
            x, y = c * cell, r * (cell + 16)
            draw.rectangle([x + 2, y + 2, x + cell - 3, y + cell - 3], fill=hexrgba(value), outline=OUTLINE, width=2)
            draw.text((x + 3, y + cell), name[:10], fill=(0, 0, 0, 255), font=font)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    image.save(path)


if __name__ == "__main__":
    root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
    out = os.path.join(root, "Docs", "Images", "ArtPalette.png")
    swatch(out)
    print(out)
