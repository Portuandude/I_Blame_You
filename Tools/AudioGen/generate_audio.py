"""I Blame You! 임시 사운드 생성기 (numpy만 사용하는 간단한 합성).

배경음악 2곡(메뉴/던전, 끊김 없이 반복되는 루프)과
마법 발동음 5종(Cast_<속성>), 적중음 5종(Hit_<속성>)을 WAV로 만들어
Assets/Resources/Audio/{Music,SFX} 에 저장한다. 게임은 런타임에 Resources에서 바로 읽는다.
진짜 사운드가 생기면 같은 이름의 파일(wav/ogg/mp3)로 바꾸면 된다.

    python3 Tools/AudioGen/generate_audio.py
"""
import os
import wave

import numpy as np

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Resources", "Audio")
SR = 22050
RNG = np.random.default_rng(42)


def reseed(name):
    global RNG
    RNG = np.random.default_rng(sum(ord(c) * (i + 1) for i, c in enumerate(name)))


# ---------------------------------------------------------------------------
# 기본 도구

def t_axis(seconds):
    return np.arange(int(seconds * SR)) / SR


def note_hz(name):
    """'A4', 'C#3' 같은 음 이름 → 주파수."""
    names = {"C": -9, "C#": -8, "D": -7, "D#": -6, "E": -5, "F": -4, "F#": -3, "G": -2, "G#": -1, "A": 0, "A#": 1, "B": 2}
    pitch, octave = name[:-1], int(name[-1])
    return 440.0 * 2 ** ((names[pitch] + (octave - 4) * 12) / 12)


def phase_of(freq, n):
    """주파수(상수 또는 배열)를 적분한 위상 (0~1 주기)."""
    f = np.broadcast_to(np.asarray(freq, dtype=float), (n,))
    return np.cumsum(f) / SR


def osc(kind, freq, n, duty=0.5):
    ph = phase_of(freq, n) % 1.0
    if kind == "sine":
        return np.sin(2 * np.pi * ph)
    if kind == "square":
        return np.where(ph < duty, 1.0, -1.0)
    if kind == "saw":
        return 2 * ph - 1
    if kind == "tri":
        return 4 * np.abs(ph - 0.5) - 1
    raise ValueError(kind)


def noise(n):
    return RNG.uniform(-1, 1, n)


def env_adsr(n, a=0.01, d=0.1, s=0.7, r=0.1):
    a_n, d_n, r_n = int(a * SR), int(d * SR), int(r * SR)
    s_n = max(0, n - a_n - d_n - r_n)
    e = np.concatenate([
        np.linspace(0, 1, a_n, endpoint=False),
        np.linspace(1, s, d_n, endpoint=False),
        np.full(s_n, s),
        np.linspace(s, 0, r_n),
    ])
    return np.pad(e, (0, max(0, n - len(e))))[:n]


def env_exp(n, decay):
    return np.exp(-np.arange(n) / SR / decay)


def lowpass(x, cutoff):
    """1차 저역통과. cutoff는 상수 또는 샘플별 배열(Hz)."""
    c = np.broadcast_to(np.asarray(cutoff, dtype=float), x.shape)
    alpha = 1 - np.exp(-2 * np.pi * c / SR)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += alpha[i] * (x[i] - acc)
        y[i] = acc
    return y


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def bandpass(x, low, high):
    return lowpass(highpass(x, low), high)


def normalize(x, peak=0.89):
    m = np.max(np.abs(x))
    return x * (peak / m) if m > 0 else x


def loudness(x, rms=0.2):
    """효과음끼리 체감 크기를 맞춘다: RMS를 맞춘 뒤 튀는 순간 피크는 tanh로 부드럽게 눌러 준다."""
    cur = np.sqrt(np.mean(x ** 2))
    if cur > 0:
        x = x * (rms / cur)
    return np.tanh(x * 1.2) * 0.89


def fade_edges(x, ms=4):
    n = int(ms / 1000 * SR)
    x = x.copy()
    x[:n] *= np.linspace(0, 1, n)
    x[-n:] *= np.linspace(1, 0, n)
    return x


def save_wav(x, *parts):
    path = os.path.join(OUT, *parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    data = (np.clip(x, -1, 1) * 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


def mix_at(buf, sound, start):
    """루프 버퍼에 sound를 start 샘플부터 더한다. 끝을 넘치면 앞으로 감아서 루프 이음새를 자연스럽게 한다."""
    n = len(buf)
    idx = (np.arange(len(sound)) + start) % n
    np.add.at(buf, idx, sound)


# ---------------------------------------------------------------------------
# 효과음

def cast_fire():
    n = int(0.55 * SR)
    sweep = np.linspace(400, 3500, n)
    whoosh = bandpass(noise(n), sweep * 0.5, sweep) * env_adsr(n, 0.05, 0.2, 0.6, 0.25)
    crackle = (RNG.random(n) < 0.004) * RNG.uniform(-1, 1, n)
    crackle = highpass(lowpass(crackle, 4000), 800) * 6 * env_exp(n, 0.3)
    body = osc("saw", np.linspace(110, 220, n), n) * 0.25
    body = lowpass(body, 900) * env_adsr(n, 0.02, 0.15, 0.4, 0.3)
    return normalize(fade_edges(whoosh * 1.2 + crackle + body))


def cast_water():
    n = int(0.5 * SR)
    t = np.arange(n) / SR
    gliss = 300 * 2 ** (t * 3.2) * (1 + 0.04 * np.sin(2 * np.pi * 18 * t))
    tone = osc("sine", gliss, n) * env_adsr(n, 0.01, 0.1, 0.6, 0.25)
    bubbles = np.zeros(n)
    for k in range(6):
        start = int(RNG.uniform(0, 0.35) * SR)
        m = int(0.06 * SR)
        f = RNG.uniform(500, 1100)
        b = osc("sine", np.linspace(f, f * 1.8, m), m) * env_exp(m, 0.02)
        bubbles[start:start + m] += b[: n - start] * 0.5
    splash = bandpass(noise(n), 1500, 6000) * env_exp(n, 0.08) * 0.4
    return normalize(fade_edges(tone * 0.7 + bubbles + splash))


def cast_wind():
    n = int(0.45 * SR)
    t = np.arange(n) / SR
    center = 900 + 2600 * np.sin(np.pi * t / t[-1])
    air = bandpass(noise(n), center * 0.6, center * 1.4) * env_adsr(n, 0.04, 0.1, 0.8, 0.2)
    whistle = osc("sine", center * 1.1, n) * 0.12 * env_adsr(n, 0.08, 0.1, 0.5, 0.15)
    return normalize(fade_edges(air + whistle))


def cast_earth():
    n = int(0.6 * SR)
    t = np.arange(n) / SR
    rumble = lowpass(noise(n), 220) * env_adsr(n, 0.15, 0.2, 0.8, 0.2) * 3
    drone = osc("tri", 55 + 10 * t, n) * 0.5 * env_adsr(n, 0.1, 0.2, 0.7, 0.2)
    grit = (RNG.random(n) < 0.01) * RNG.uniform(-1, 1, n)
    grit = bandpass(grit, 300, 2000) * 3 * env_adsr(n, 0.2, 0.1, 0.6, 0.2)
    return normalize(fade_edges(rumble + drone + grit))


def cast_neutral():
    """무속성 마력탄 (평타라 자주 들림): 수정(크리스탈)을 톡 친 듯한 맑은 울림. 시작은 또렷하되 고역은 눌러서 거슬리지 않게, 0.25초."""
    n = int(0.25 * SR)
    t = np.arange(n) / SR
    # 유리처럼 배음이 어긋난 부분음들: 높은 부분음일수록 빨리 사라져 '팅' 다음엔 둥근 여운만 남는다
    partials = [(1.0, 1.0, 14), (2.76, 0.35, 26), (5.40, 0.12, 45)]
    base = 1150
    ring = np.zeros(n)
    for ratio, amp, decay in partials:
        ring += np.sin(2 * np.pi * base * ratio * t) * amp * np.exp(-t * decay)
    ring = lowpass(ring, 5000)
    ring *= env_adsr(n, 0.003, 0.0, 1.0, 0.0)
    # 무게감을 주는 한 옥타브 아래 음 (너무 가볍지 않게)
    body = osc("sine", base * 0.5, n) * np.exp(-t * 18) * env_adsr(n, 0.006, 0.0, 1.0, 0.0) * 0.4
    # 날아가는 느낌의 아주 옅은 바람
    air = lowpass(bandpass(noise(n), 600, 2400), 2500) * env_adsr(n, 0.02, 0.06, 0.3, 0.08) * 0.12
    return normalize(fade_edges(ring + body + air))


def hit_fire():
    n = int(0.6 * SR)
    t = np.arange(n) / SR
    boom = osc("sine", 120 * np.exp(-t * 6) + 40, n) * env_exp(n, 0.15)
    blast = lowpass(noise(n), 2500 * np.exp(-t * 4) + 300) * env_exp(n, 0.12) * 1.5
    crackle = (RNG.random(n) < 0.006) * RNG.uniform(-1, 1, n)
    crackle = highpass(crackle, 1500) * 5 * env_exp(n, 0.25)
    return normalize(fade_edges(boom + blast + crackle))


def hit_water():
    n = int(0.45 * SR)
    t = np.arange(n) / SR
    splash = bandpass(noise(n), 1200, 7000) * env_exp(n, 0.07) * 1.2
    plop = osc("sine", 700 * np.exp(-t * 14) + 150, n) * env_exp(n, 0.08)
    drops = np.zeros(n)
    for k in range(5):
        start = int(RNG.uniform(0.05, 0.3) * SR)
        m = int(0.04 * SR)
        f = RNG.uniform(900, 1600)
        d = osc("sine", np.linspace(f, f * 1.5, m), m) * env_exp(m, 0.012) * 0.35
        drops[start:start + m] += d[: n - start]
    return normalize(fade_edges(splash + plop + drops))


def hit_wind():
    n = int(0.35 * SR)
    t = np.arange(n) / SR
    center = 4000 * np.exp(-t * 6) + 600
    swish = bandpass(noise(n), center * 0.5, center) * env_adsr(n, 0.005, 0.08, 0.4, 0.2) * 1.4
    thump = osc("sine", 160 * np.exp(-t * 10) + 60, n) * env_exp(n, 0.05) * 0.6
    return normalize(fade_edges(swish + thump))


def hit_earth():
    n = int(0.6 * SR)
    t = np.arange(n) / SR
    thud = osc("sine", 90 * np.exp(-t * 8) + 35, n) * env_exp(n, 0.18) * 1.3
    crunch = lowpass(noise(n), 1800) * env_exp(n, 0.06) * 1.2
    rocks = np.zeros(n)
    for k in range(10):
        start = int(RNG.uniform(0.03, 0.4) * SR)
        m = int(0.025 * SR)
        g = bandpass(noise(m), 400, 2500) * env_exp(m, 0.008)
        rocks[start:start + m] += g[: n - start] * RNG.uniform(0.4, 0.9)
    return normalize(fade_edges(thud + crunch + rocks))


def hit_neutral():
    """무속성 적중: 묵직한 '퍽' + 짧은 저음 공명."""
    n = int(0.4 * SR)
    t = np.arange(n) / SR
    punch = osc("sine", 110 * np.exp(-t * 14) + 50, n) * env_exp(n, 0.1) * 1.3
    body = lowpass(noise(n), 700) * env_exp(n, 0.04)
    ring_ = osc("tri", note_hz("A2"), n) * env_exp(n, 0.15) * 0.35
    return normalize(fade_edges(punch + body + ring_))


# ---------------------------------------------------------------------------
# 배경음악

def kick():
    n = int(0.3 * SR)
    t = np.arange(n) / SR
    return osc("sine", 150 * np.exp(-t * 25) + 45, n) * env_exp(n, 0.12)


def snare():
    n = int(0.22 * SR)
    t = np.arange(n) / SR
    tone = osc("tri", 190 * np.exp(-t * 10) + 120, n) * env_exp(n, 0.05) * 0.5
    return tone + bandpass(noise(n), 1500, 7000) * env_exp(n, 0.07)


def hat(open_=False):
    n = int((0.18 if open_ else 0.05) * SR)
    return highpass(noise(n), 6000) * env_exp(n, 0.06 if open_ else 0.015)


def tone(kind, name, seconds, a=0.01, d=0.08, s=0.6, r=0.08, duty=0.5, vib=0.0):
    n = int(seconds * SR)
    f = note_hz(name)
    if vib:
        t = np.arange(n) / SR
        f = f * (1 + vib * np.sin(2 * np.pi * 5.5 * t) * np.clip(t * 4, 0, 1))
    return osc(kind, f, n, duty) * env_adsr(n, a, d, s, r)


def pad_chord(names, seconds):
    n = int(seconds * SR)
    out = np.zeros(n)
    for name in names:
        f = note_hz(name)
        for det in (-0.004, 0.004):
            out += osc("saw", f * (1 + det), n)
    out = lowpass(out / (len(names) * 2), 1100)
    return out * env_adsr(n, 0.25, 0.3, 0.8, 0.4)


def dungeon_theme():
    """던전(전투) 테마: 124 BPM, A단조, 16마디 루프. Am-F-C-G 진행 + 마지막 4마디는 Am-F-G-E."""
    bpm = 124
    beat = 60 / bpm
    bars = 16
    n = int(round(bars * 4 * beat * SR))
    music = np.zeros(n)
    progression = [
        ("A", ["A3", "C4", "E4"]), ("F", ["F3", "A3", "C4"]), ("C", ["C4", "E4", "G4"]), ("G", ["G3", "B3", "D4"]),
    ] * 3 + [("A", ["A3", "C4", "E4"]), ("F", ["F3", "A3", "C4"]), ("G", ["G3", "B3", "D4"]), ("E", ["E3", "G#3", "B3"])]
    bass_root = {"A": "A2", "F": "F2", "C": "C3", "G": "G2", "E": "E2"}

    melody_a = [("E5", 1), ("A5", 1), ("G5", 0.5), ("E5", 0.5), ("D5", 1),
                ("C5", 1.5), ("D5", 0.5), ("E5", 1), ("A4", 1),
                ("G4", 1), ("C5", 1), ("E5", 1), ("G5", 1),
                ("F#5", 0.5), ("G5", 0.5), ("D5", 2), (None, 1)]
    melody_b = [("A5", 1.5), ("G5", 0.5), ("E5", 1), ("C5", 1),
                ("D5", 1), ("F5", 1), ("E5", 1), ("C5", 1),
                ("E5", 0.5), ("D5", 0.5), ("C5", 1), ("G4", 1), ("C5", 1),
                ("B4", 1), ("D5", 1), ("G5", 2)]
    melody_end = [("E5", 1), ("C5", 1), ("A4", 2),
                  ("F4", 1), ("A4", 1), ("C5", 2),
                  ("D5", 1), ("B4", 1), ("G4", 2),
                  ("G#4", 1), ("B4", 1), ("E5", 2)]

    for bar, (root, chord) in enumerate(progression):
        bar_start = bar * 4 * beat
        # 패드
        mix_at(music, pad_chord(chord, 4 * beat) * 0.18, int(bar_start * SR))
        # 베이스: 8분음표, 옥타브 왕복
        for k in range(8):
            name = bass_root[root]
            if k % 2 == 1:
                name = name[:-1] + str(int(name[-1]) + 1)
            b = tone("square", name, beat / 2 * 0.9, 0.005, 0.05, 0.5, 0.03, duty=0.25)
            mix_at(music, lowpass(b, 1200) * 0.22, int((bar_start + k * beat / 2) * SR))
        # 드럼: 킥 1·3박(+3.5), 스네어 2·4박, 하이햇 8분
        for b_ in (0, 2, 2.5):
            mix_at(music, kick() * 0.55, int((bar_start + b_ * beat) * SR))
        for b_ in (1, 3):
            mix_at(music, snare() * 0.3, int((bar_start + b_ * beat) * SR))
        for k in range(8):
            mix_at(music, hat(k == 7) * (0.12 if k % 2 else 0.08), int((bar_start + k * beat / 2) * SR))
        # 아르페지오 (16분음표, 화음 구성음 위아래)
        arp = chord + [chord[1][:-1] + str(int(chord[1][-1]) + 1)]
        for k in range(16):
            name = arp[[0, 1, 2, 3, 2, 1, 2, 3][k % 8]]
            name = name[:-1] + str(int(name[-1]) + 1)
            a_ = tone("square", name, beat / 4 * 0.8, 0.002, 0.04, 0.3, 0.02, duty=0.125)
            mix_at(music, a_ * 0.045, int((bar_start + k * beat / 4) * SR))

    def lay_melody(notes, start_bar, volume):
        pos = start_bar * 4 * beat
        for name, beats in notes:
            if name:
                m = tone("square", name, beats * beat * 0.95, 0.01, 0.1, 0.55, 0.06, duty=0.5, vib=0.006)
                mix_at(music, lowpass(m, 3200) * volume, int(pos * SR))
            pos += beats * beat

    lay_melody(melody_a, 4, 0.13)
    lay_melody(melody_b, 8, 0.13)
    lay_melody(melody_end, 12, 0.13)
    return normalize(music, 0.7)


def menu_theme():
    """메뉴 테마: 84 BPM, 잔잔한 벨 아르페지오 + 패드. Am-Em-F-G ×2, 끝 4마디 Dm-Am-F-E."""
    bpm = 84
    beat = 60 / bpm
    progression = [["A3", "C4", "E4"], ["E3", "G3", "B3"], ["F3", "A3", "C4"], ["G3", "B3", "D4"]] * 2 + \
                  [["D3", "F3", "A3"], ["A3", "C4", "E4"], ["F3", "A3", "C4"], ["E3", "G#3", "B3"]]
    n = int(round(len(progression) * 4 * beat * SR))
    music = np.zeros(n)
    for bar, chord in enumerate(progression):
        bar_start = bar * 4 * beat
        mix_at(music, pad_chord(chord, 4 * beat) * 0.22, int(bar_start * SR))
        low = chord[0][:-1] + str(int(chord[0][-1]) - 1)
        mix_at(music, tone("sine", low, 4 * beat, 0.05, 0.5, 0.6, 0.5) * 0.25, int(bar_start * SR))
        up = [c[:-1] + str(int(c[-1]) + 1) for c in chord]
        pattern = [up[0], up[1], up[2], up[1], chord[2], up[2], up[1], up[0]]
        for k, name in enumerate(pattern):
            m = int(beat / 2 * 2.5 * SR)
            f = note_hz(name)
            bell = (osc("sine", f, m) + 0.3 * osc("sine", f * 3.01, m) * env_exp(m, 0.08)) * env_exp(m, 0.5)
            mix_at(music, bell * 0.09, int((bar_start + k * beat / 2) * SR))
    return normalize(music, 0.6)


def main():
    sfx = {
        "Cast_Fire": cast_fire, "Cast_Water": cast_water, "Cast_Wind": cast_wind, "Cast_Earth": cast_earth,
        "Cast_Neutral": cast_neutral,
        "Hit_Fire": hit_fire, "Hit_Water": hit_water, "Hit_Wind": hit_wind, "Hit_Earth": hit_earth,
        "Hit_Neutral": hit_neutral,
    }
    # 소리마다 난수 씨앗을 따로 둬서 한 소리를 고쳐도 다른 소리가 바뀌지 않게 한다.
    soft = {"Cast_Neutral": 0.13}  # 평타처럼 자주 나는 소리는 작고, tanh 압축 없이 부드럽게
    for name, fn in sfx.items():
        reseed(name)
        sound = fn()
        if name in soft:
            sound = normalize(sound, 1.0)
            sound *= soft[name] / np.sqrt(np.mean(sound ** 2))
        else:
            sound = loudness(sound)
        save_wav(fade_edges(sound), "SFX", f"{name}.wav")
    reseed("Menu")
    save_wav(menu_theme(), "Music", "Menu.wav")
    reseed("Dungeon")
    save_wav(dungeon_theme(), "Music", "Dungeon.wav")
    print("done")


if __name__ == "__main__":
    main()
