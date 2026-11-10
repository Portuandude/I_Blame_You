"""I Blame You! 임시 사운드 생성기 (numpy만 사용하는 간단한 합성).

배경음악 2곡(메뉴/던전, 끊김 없이 반복되는 루프)과
마법 발동음 5종(Cast_<속성>), 적중음 5종(Hit_<속성>), 동작음(Jump, Dash, Hurt, EnemyDie, LevelUp, RelicOpen)을 WAV로 만들어
Assets/Resources/Audio/{Music,SFX} 에 저장한다. 게임은 런타임에 Resources에서 바로 읽는다.
진짜 사운드가 생기면 같은 이름의 파일(wav/ogg/mp3)로 바꾸면 된다.

    python3 Tools/AudioGen/generate_audio.py

주의: Hit_Neutral.wav는 저장소에 이전 버전을 일부러 남겨 두었다. 전체를 다시 만들면 바뀌므로, 새 소리만 필요하면 다시 만든 뒤 `git checkout Assets/Resources/Audio/SFX/Hit_Neutral.wav`로 되돌릴 것.
"""
import os
import wave

import sys

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
    """무속성 마력탄 (평타라 자주 들림): 공을 던질 때의 짧고 부드러운 '훅'. 고역을 거의 없애고 천천히 부풀었다 빠진다, 0.2초."""
    n = int(0.2 * SR)
    t = np.arange(n) / SR
    # 팔을 휘두르는 바람: 낮은 대역 잡음을 저역통과로 여러 번 걸러 날카로운 '쉭' 성분을 없앤다
    center = 700 * np.exp(-t * 5) + 280
    swing = bandpass(noise(n), center * 0.5, center)
    for _ in range(3):
        swing = lowpass(swing, 1100)
    swing *= env_adsr(n, 0.045, 0.05, 0.5, 0.1)
    # 손을 떠나는 순간의 아주 작은 저음 (클릭이 생기지 않게 5ms에 걸쳐 시작)
    release = osc("sine", 120 * np.exp(-t * 20) + 75, n) * env_adsr(n, 0.005, 0.03, 0.0, 0.0) * 0.3
    return normalize(fade_edges(swing + release))


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
    n = int(0.5 * SR)
    t = np.arange(n) / SR
    zap = osc("square", 1400 * np.exp(-t * 9) + 300, n, duty=0.3) * env_exp(n, 0.06) * 0.35
    zap = lowpass(zap, 5000)
    bell = (osc("sine", note_hz("E6"), n) + osc("sine", note_hz("B6"), n) * 0.6) * env_exp(n, 0.15) * 0.5
    burst = highpass(noise(n), 2500) * env_exp(n, 0.04) * 0.5
    return normalize(fade_edges(zap + bell + burst))


# ---- 동작 효과음 (점프/대쉬/피격/적 사망/레벨업) ----

def jump():
    n = int(0.18 * SR)
    t = np.arange(n) / SR
    chirp = osc("square", 260 + 900 * t / t[-1], n, duty=0.25) * env_adsr(n, 0.005, 0.05, 0.5, 0.08) * 0.5
    puff = lowpass(noise(n), 1500) * env_exp(n, 0.03) * 0.4
    return normalize(fade_edges(lowpass(chirp, 4000) + puff))


def dash():
    n = int(0.3 * SR)
    t = np.arange(n) / SR
    center = 900 + 3200 * np.exp(-((t - 0.07) / 0.06) ** 2)
    swoosh = bandpass(noise(n), center * 0.5, center) * env_adsr(n, 0.02, 0.08, 0.5, 0.15) * 1.5
    return normalize(fade_edges(swoosh))


def hurt():
    n = int(0.28 * SR)
    t = np.arange(n) / SR
    thump = osc("sine", 180 * np.exp(-t * 12) + 70, n) * env_exp(n, 0.08) * 1.0
    grit = lowpass(osc("saw", 220 * np.exp(-t * 4), n), 2500) * env_exp(n, 0.06) * 0.5
    snap = highpass(noise(n), 2000) * env_exp(n, 0.015) * 0.5
    return normalize(fade_edges(thump + grit + snap))


def land():
    """높은 곳에서 떨어져 착지: 폭신한 '퐁' (낮은 사인 낙하 + 부드러운 흙먼지). 귀엽게, 아프지 않게."""
    n = int(0.22 * SR)
    t = np.arange(n) / SR
    thud = osc("sine", 140 * np.exp(-t * 18) + 55, n) * env_exp(n, 0.07)
    puff = lowpass(noise(n), 700) * env_adsr(n, 0.004, 0.05, 0.3, 0.08) * 0.35
    pad = osc("tri", 330 * np.exp(-t * 9) + 180, n) * env_exp(n, 0.03) * 0.18  # 통통 튀는 느낌
    return normalize(fade_edges(thud + puff + pad))


def squish_hit():
    """슬라임이 맞을 때: 말랑한 '뿌직' (아래로 휘는 블룹 + 젖은 물소리 잡음)."""
    n = int(0.2 * SR)
    t = np.arange(n) / SR
    bloop = osc("sine", 520 * np.exp(-t * 14) + 160 + 60 * np.sin(2 * np.pi * 28 * t), n) * env_adsr(n, 0.003, 0.06, 0.4, 0.08)
    wet = bandpass(noise(n), 500, 2200) * env_exp(n, 0.035) * 0.55
    pop = osc("sine", 900 * np.exp(-t * 60) + 300, n) * env_exp(n, 0.012) * 0.3
    return normalize(fade_edges(lowpass(bloop + wet + pop, 3500)))


def enemy_die():
    n = int(0.55 * SR)
    t = np.arange(n) / SR
    fall = osc("square", 520 * np.exp(-t * 4) + 60, n, duty=0.4) * env_adsr(n, 0.005, 0.15, 0.4, 0.3) * 0.35
    splat = lowpass(noise(n), 900) * env_exp(n, 0.12) * 0.9
    return normalize(fade_edges(lowpass(fall, 3000) + splat))


def level_up():
    notes = ["C5", "E5", "G5", "C6"]
    step = 0.085
    n = int((step * len(notes) + 0.45) * SR)
    out = np.zeros(n)
    for k, name in enumerate(notes):
        m = int(0.5 * SR)
        f = note_hz(name)
        tone_ = (osc("square", f, m, duty=0.5) * 0.25 + osc("sine", f * 2, m) * 0.3) * env_adsr(m, 0.005, 0.1, 0.35, 0.3)
        start = int(k * step * SR)
        end = min(n, start + m)
        out[start:end] += tone_[: end - start]
    return normalize(fade_edges(lowpass(out, 6000)))


def relic_open():
    """유물 상자: 밝은 하프 같은 상승 아르페지오 + 반짝이는 고음 노이즈 꼬리."""
    notes = ["G5", "B5", "D6", "G6", "B6"]
    step = 0.06
    n = int((step * len(notes) + 0.8) * SR)
    out = np.zeros(n)
    for k, name in enumerate(notes):
        m = int(0.7 * SR)
        f = note_hz(name)
        pluck = (osc("sine", f, m) * 0.5 + osc("tri", f * 2, m) * 0.2) * env_exp(m, 7.0)
        start = int(k * step * SR)
        end = min(n, start + m)
        out[start:end] += pluck[: end - start]
    tail = bandpass(noise(n), 5000, 9000) * env_exp(n, 4.0) * 0.12
    return normalize(fade_edges(lowpass(out + tail, 9000)))


def switch_on():
    """마법 스위치 룬이 켜짐: 맑은 종 두 음(5도 위로) + 반짝이는 고음 꼬리."""
    n = int(0.9 * SR)
    out = np.zeros(n)
    for k, name in enumerate(["E6", "B6"]):
        m = int(0.75 * SR)
        f = note_hz(name)
        bell = (osc("sine", f, m) * 0.55 + osc("sine", f * 2.76, m) * 0.18 * env_exp(m, 14.0)) * env_exp(m, 5.0)
        mix_at(out, bell, int(k * 0.09 * SR))
    tail = bandpass(noise(n), 6000, 10000) * env_exp(n, 6.0) * 0.08
    return normalize(fade_edges(lowpass(out + tail, 10000)))


def switch_tick():
    """스위치 가동이 끝나 감: 짧고 가벼운 나무 똑딱 (0.5초마다 울린다)."""
    n = int(0.12 * SR)
    click = osc("tri", note_hz("A5"), n) * env_exp(n, 45.0)
    knock = bandpass(noise(n), 1500, 4000) * env_exp(n, 80.0) * 0.5
    return normalize(fade_edges(click + knock))


def checkpoint():  # 체크포인트 제거(2026-11-10)로 main()에서 뺐다. 샘 연출 등에 다시 쓸 수 있게 남김
    """체크포인트 켜짐: 따뜻한 패드가 부풀며 맑은 종 아르페지오(C-E-G-C)가 올라가고 반짝이 꼬리, 1.3초."""
    n = int(1.3 * SR)
    out = np.zeros(n)
    m = int(1.1 * SR)
    pad = sum(osc("sine", note_hz(x), m) for x in ("C4", "E4", "G4")) / 3
    mix_at(out, pad * env_adsr(m, 0.25, 0.3, 0.6, 0.5) * 0.35, 0)
    for k, name in enumerate(["C5", "E5", "G5", "C6"]):
        b = int(0.8 * SR)
        f = note_hz(name)
        bell = (osc("sine", f, b) * 0.5 + osc("sine", f * 2.76, b) * 0.15 * env_exp(b, 0.08)) * env_exp(b, 0.3)
        mix_at(out, bell, int(k * 0.08 * SR))
    tail = bandpass(noise(n), 6000, 10000) * env_exp(n, 0.35) * 0.06
    return normalize(fade_edges(lowpass(out + tail, 9000)))


def gate_open():
    """봉인문 열림: 마력 벽이 풀리는 아래로 쓸리는 휘익(노이즈) + 낮은 웅 + 마지막에 밝은 종 두 음, 1.1초."""
    n = int(1.1 * SR)
    t = np.arange(n) / SR
    sweep = lowpass(noise(n), 6000 * np.exp(-t * 3.0) + 300) * env_adsr(n, 0.02, 0.2, 0.5, 0.6) * 0.5
    hum = osc("sine", 110 * np.exp(-t * 0.8), n) * env_exp(n, 0.35) * 0.5
    out = sweep + hum
    for k, name in enumerate(["A5", "E6"]):
        m = int(0.6 * SR)
        f = note_hz(name)
        mix_at(out, osc("sine", f, m) * env_exp(m, 0.2) * 0.4, int((0.35 + k * 0.1) * SR))
    return normalize(fade_edges(out))


def pickup():
    """보상 구슬·회복 줍기: 동글동글한 두 음 뽀록(위로 튀는 사인), 0.3초."""
    n = int(0.3 * SR)
    out = np.zeros(n)
    for k, (lo, hi) in enumerate([(600, 900), (900, 1400)]):
        m = int(0.14 * SR)
        t = np.arange(m) / SR
        f = lo + (hi - lo) * np.clip(t / 0.05, 0, 1)
        mix_at(out, osc("sine", f, m) * env_adsr(m, 0.004, 0.04, 0.5, 0.08), int(k * 0.08 * SR))
    return normalize(fade_edges(out))


def star_shard():
    """별똥 조각이 플레이어에 닿을 때: 작고 맑은 "띵" (높은 사인 + 5도 위 배음, 빠른 감쇠), 0.35초."""
    n = int(0.35 * SR)
    out = osc("sine", 1568.0, n) * env_exp(n, 0.08)            # G6
    out += 0.45 * osc("sine", 2349.0, n) * env_exp(n, 0.05)   # D7
    out += 0.2 * osc("sine", 3136.0, n) * env_exp(n, 0.03)    # G7 반짝임
    return normalize(fade_edges(out))


def arena_lock():
    """전투 구역 잠김: 무거운 문이 쿵 내려앉는 소리 + 쇠 울림, 0.7초."""
    n = int(0.7 * SR)
    t = np.arange(n) / SR
    thump = osc("sine", 90 * np.exp(-t * 8) + 40, n) * env_exp(n, 0.12)
    crash = lowpass(noise(n), 2500) * env_exp(n, 0.05) * 0.6
    clang = sum(osc("square", f, n, 0.3) * w for f, w in ((311, 0.15), (467, 0.1), (703, 0.06))) * env_exp(n, 0.18)
    return normalize(fade_edges(thump + crash + lowpass(clang, 3000)))


def elite_warn():
    """정예 등장 경고: 낮은 두 음 '둥-둥' (트레몰로 톱니 + 쿵), 무섭지만 귀엽게 짧게."""
    n = int(0.75 * SR)
    out = np.zeros(n)
    for start, name in ((0.0, "A2"), (0.3, "D#2")):
        m = int(0.42 * SR)
        t = np.arange(m) / SR
        body = osc("saw", note_hz(name), m) * (0.6 + 0.4 * np.sin(2 * np.pi * 14 * t))
        body = lowpass(body, 900) * env_adsr(m, 0.01, 0.1, 0.6, 0.15) * 0.6
        boom = osc("sine", 90 * np.exp(-t * 10) + 40, m) * env_exp(m, 0.1)
        mix_at(out, body + boom, int(start * SR))
    return normalize(fade_edges(out))


def jingle_clear():
    """클리어 팡파르: 빠른 상승 C-E-G-C 셋잇단 + 길게 끄는 C장조 화음(나팔 같은 사각파 + 종), 반짝이 꼬리, 약 2.8초."""
    n = int(2.8 * SR)
    out = np.zeros(n)
    step = 0.11
    for k, name in enumerate(["C5", "E5", "G5"]):
        m = int(step * 1.1 * SR)
        mix_at(out, lowpass(tone("square", name, step * 1.1, 0.005, 0.03, 0.6, 0.02, duty=0.25), 3500) * 0.35, int(k * step * SR))
    hold = 1.9
    start = int(3 * step * SR)
    for name, w in (("C6", 0.32), ("G5", 0.2), ("E5", 0.2), ("C4", 0.25)):
        brass = lowpass(tone("square", name, hold, 0.02, 0.2, 0.55, 0.9, duty=0.3, vib=0.01), 3000) * w
        mix_at(out, brass, start)
    for k, name in enumerate(["C6", "E6", "G6", "C7"]):
        m = int(0.9 * SR)
        f = note_hz(name)
        mix_at(out, osc("sine", f, m) * env_exp(m, 0.25) * 0.18, start + int((0.15 + k * 0.07) * SR))
    for b_ in (0, 3 * step):
        mix_at(out, snare() * 0.25, int(b_ * SR))
    mix_at(out, kick() * 0.5, start)
    tail = bandpass(noise(n), 6000, 10000) * env_exp(n, 0.8) * 0.04
    return normalize(fade_edges(out + tail))


def jingle_defeat():
    """게임오버: 느리게 내려가는 단조 멜로디(E-D#-D-C#… 반음씩 처지는 '와와와와') + 낮은 A 빈 5도 화음으로 끝, 약 2.6초."""
    n = int(2.6 * SR)
    out = np.zeros(n)
    notes = [("E4", 0.3), ("D#4", 0.3), ("D4", 0.3), ("C#4", 1.4)]
    pos = 0.0
    for name, length in notes:
        f = note_hz(name)
        m = int(length * SR)
        t = np.arange(m) / SR
        wah = osc("tri", f * (1 + 0.012 * np.sin(2 * np.pi * 6 * t)), m) * 0.6 + osc("square", f, m, 0.5) * 0.15
        mix_at(out, lowpass(wah, 1800) * env_adsr(m, 0.02, 0.1, 0.7, min(0.4, length * 0.6)) * 0.5, int(pos * SR))
        pos += length
    chord_start = int(1.2 * SR)
    m = n - chord_start
    for name in ("A2", "E3", "A3"):
        mix_at(out, osc("sine", note_hz(name), m) * env_adsr(m, 0.05, 0.3, 0.5, 0.9) * 0.18, chord_start)
    return normalize(fade_edges(out))


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


def dungeon2_theme():
    """후반 던전 테마 (진행도 50% 이후, 중간보스 뒤): 138 BPM, E단조, 16마디 루프. 같은 분위기를 더 빠르고 화려하게:
    16분 하이햇, 4박 킥 + 오프비트 베이스, 위쪽 반짝이 아르페지오, 후반 8마디는 3도 위 화음 멜로디.
    Em-C-G-D ×3, 끝 4마디는 Em-C-D-B (B 화음으로 루프 처음 Em에 돌아온다)."""
    bpm = 138
    beat = 60 / bpm
    progression = [
        ("E", ["E4", "G4", "B4"]), ("C", ["C4", "E4", "G4"]), ("G", ["G3", "B3", "D4"]), ("D", ["D4", "F#4", "A4"]),
    ] * 3 + [("E", ["E4", "G4", "B4"]), ("C", ["C4", "E4", "G4"]), ("D", ["D4", "F#4", "A4"]), ("B", ["B3", "D#4", "F#4"])]
    bass_root = {"E": "E2", "C": "C2", "G": "G2", "D": "D2", "B": "B1"}
    n = int(round(len(progression) * 4 * beat * SR))
    music = np.zeros(n)

    for bar, (root, chord) in enumerate(progression):
        bar_start = bar * 4 * beat
        mix_at(music, pad_chord(chord, 4 * beat) * 0.17, int(bar_start * SR))
        # 베이스: 8분음표, 뒷박은 옥타브 위 (통통 튀게)
        for k in range(8):
            name = bass_root[root]
            if k % 2 == 1:
                name = name[:-1] + str(int(name[-1]) + 1)
            b = tone("square", name, beat / 2 * 0.85, 0.004, 0.05, 0.5, 0.03, duty=0.25)
            mix_at(music, lowpass(b, 1300) * 0.22, int((bar_start + k * beat / 2) * SR))
        # 드럼: 킥 매 박, 스네어 2·4박, 하이햇 16분 (8마디마다 마지막 박 스네어 필)
        for b_ in (0, 1, 2, 3):
            mix_at(music, kick() * 0.5, int((bar_start + b_ * beat) * SR))
        for b_ in (1, 3):
            mix_at(music, snare() * 0.3, int((bar_start + b_ * beat) * SR))
        if bar % 8 == 7:
            for k in range(4):
                mix_at(music, snare() * (0.12 + 0.05 * k), int((bar_start + (3 + k / 4) * beat) * SR))
        for k in range(16):
            mix_at(music, hat(k == 14) * (0.08 if k % 2 else 0.05), int((bar_start + k * beat / 4) * SR))
        # 반짝이 아르페지오 (16분, 두 옥타브 위 — 화려하게)
        up = [c[:-1] + str(int(c[-1]) + 1) for c in chord]
        for k in range(16):
            name = up[[0, 1, 2, 1, 2, 0, 1, 2][k % 8]]
            a_ = tone("square", name, beat / 4 * 0.75, 0.002, 0.04, 0.3, 0.02, duty=0.125)
            mix_at(music, a_ * 0.05, int((bar_start + k * beat / 4) * SR))

    def lay_melody(notes, start_bar, volume, duty=0.5):
        pos = start_bar * 4 * beat
        for name, beats in notes:
            if name:
                m = tone("square", name, beats * beat * 0.93, 0.008, 0.08, 0.58, 0.05, duty=duty, vib=0.007)
                mix_at(music, lowpass(m, 3400) * volume, int(pos * SR))
            pos += beats * beat

    melody_a = [("B4", 0.5), ("E5", 0.5), ("G5", 1), ("F#5", 0.5), ("E5", 0.5), ("B4", 1),
                ("C5", 1), ("E5", 0.5), ("G5", 0.5), ("C6", 1), ("B5", 1),
                ("D5", 1), ("G5", 1), ("F#5", 0.5), ("G5", 0.5), ("A5", 1),
                ("F#5", 1.5), ("D5", 0.5), ("A5", 2)]
    melody_b = [("G5", 1), ("B5", 1), ("A5", 0.5), ("G5", 0.5), ("E5", 1),
                ("E5", 0.5), ("G5", 0.5), ("C6", 1), ("B5", 1), ("G5", 1),
                ("B4", 0.5), ("D5", 0.5), ("G5", 1), ("A5", 1), ("B5", 1),
                ("A5", 1), ("F#5", 1), ("D5", 1), ("F#5", 1)]
    melody_end = [("E6", 2), ("B5", 1), ("G5", 1),
                  ("C6", 1), ("G5", 1), ("E5", 2),
                  ("D5", 1), ("F#5", 1), ("A5", 1), ("D6", 1),
                  ("D#5", 1), ("F#5", 1), ("B5", 2)]
    # 화음 멜로디 (3도 아래 부드러운 펄스): 후반 8마디만
    harmony_b = [(None, 4), ("E5", 2), ("G5", 2), ("D5", 2), ("F#5", 2), ("D5", 2), ("A4", 2)]
    lay_melody(melody_a, 4, 0.12)
    lay_melody(melody_b, 8, 0.12)
    lay_melody(harmony_b, 8, 0.06, duty=0.25)
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


def boss_theme():
    """보스(중간보스·슬라임 킹) 테마: 152 BPM, D단조, 16마디 루프. 16분음표 베이스 질주 + 매 박 킥 + 반음 올라가는 긴장 멜로디.
    Dm-Bb-C-A 진행 ×3, 끝 4마디는 Dm-Bb-Gm-A (A 화음으로 루프 처음 Dm에 돌아온다)."""
    bpm = 152
    beat = 60 / bpm
    progression = [
        ("D", ["D4", "F4", "A4"]), ("Bb", ["A#3", "D4", "F4"]), ("C", ["C4", "E4", "G4"]), ("A", ["A3", "C#4", "E4"]),
    ] * 3 + [("D", ["D4", "F4", "A4"]), ("Bb", ["A#3", "D4", "F4"]), ("G", ["G3", "A#3", "D4"]), ("A", ["A3", "C#4", "E4"])]
    bass_root = {"D": "D2", "Bb": "A#1", "C": "C2", "A": "A1", "G": "G1"}
    n = int(round(len(progression) * 4 * beat * SR))
    music = np.zeros(n)

    for bar, (root, chord) in enumerate(progression):
        bar_start = bar * 4 * beat
        # 패드 (어둡게 낮춘 톱니)
        mix_at(music, lowpass(pad_chord(chord, 4 * beat), 800) * 0.16, int(bar_start * SR))
        # 베이스: 16분음표 질주, 4번째마다 옥타브 위
        for k in range(16):
            name = bass_root[root]
            if k % 4 == 3:
                name = name[:-1] + str(int(name[-1]) + 1)
            b = tone("saw", name, beat / 4 * 0.85, 0.003, 0.04, 0.5, 0.02)
            mix_at(music, lowpass(b, 900) * 0.2, int((bar_start + k * beat / 4) * SR))
        # 드럼: 킥 매 박(+마지막 8분), 스네어 2·4박, 하이햇 16분
        for b_ in (0, 1, 2, 3, 3.5):
            mix_at(music, kick() * 0.6, int((bar_start + b_ * beat) * SR))
        for b_ in (1, 3):
            mix_at(music, snare() * 0.34, int((bar_start + b_ * beat) * SR))
        if bar % 4 == 3:  # 4마디마다 스네어 필
            for k in range(4):
                mix_at(music, snare() * (0.14 + 0.05 * k), int((bar_start + (3 + k / 4) * beat) * SR))
        for k in range(16):
            mix_at(music, hat(False) * (0.09 if k % 2 else 0.05), int((bar_start + k * beat / 4) * SR))
        # 위쪽 반짝이는 8분 아르페지오 (화려하게)
        up = [c[:-1] + str(int(c[-1]) + 1) for c in chord]
        for k in range(8):
            name = up[[0, 1, 2, 1, 0, 2, 1, 2][k]]
            a_ = tone("square", name, beat / 2 * 0.7, 0.002, 0.05, 0.25, 0.02, duty=0.125)
            mix_at(music, a_ * 0.04, int((bar_start + k * beat / 2) * SR))

    def lay_melody(notes, start_bar, volume):
        pos = start_bar * 4 * beat
        for name, beats in notes:
            if name:
                m = tone("square", name, beats * beat * 0.92, 0.008, 0.08, 0.6, 0.05, duty=0.25, vib=0.008)
                mix_at(music, lowpass(m, 3500) * volume, int(pos * SR))
            pos += beats * beat

    melody_a = [("D5", 1), ("F5", 0.5), ("E5", 0.5), ("D5", 1), ("A4", 1),
                ("A#4", 1), ("D5", 1), ("F5", 1.5), ("G5", 0.5),
                ("E5", 1), ("G5", 1), ("C6", 1), ("A#5", 0.5), ("A5", 0.5),
                ("A5", 1.5), ("G#5", 0.5), ("A5", 1), ("C#5", 1)]
    melody_b = [("A5", 1), ("G5", 0.5), ("F5", 0.5), ("E5", 1), ("D5", 1),
                ("F5", 1), ("A#5", 1), ("A5", 1), ("F5", 1),
                ("G5", 0.5), ("A5", 0.5), ("A#5", 0.5), ("C6", 0.5), ("A#5", 1), ("G5", 1),
                ("E5", 1), ("C#5", 1), ("E5", 1), ("A5", 1)]
    melody_end = [("D6", 2), ("A5", 1), ("F5", 1),
                  ("D5", 1), ("F5", 1), ("A#5", 2),
                  ("A#5", 1), ("A5", 1), ("G5", 1), ("D5", 1),
                  ("C#5", 1), ("E5", 1), ("G5", 1), ("A5", 1)]
    lay_melody(melody_a, 4, 0.12)
    lay_melody(melody_b, 8, 0.12)
    lay_melody(melody_end, 12, 0.13)
    return normalize(music, 0.72)


def main():
    sfx = {
        "Cast_Fire": cast_fire, "Cast_Water": cast_water, "Cast_Wind": cast_wind, "Cast_Earth": cast_earth,
        "Cast_Neutral": cast_neutral,
        "Hit_Fire": hit_fire, "Hit_Water": hit_water, "Hit_Wind": hit_wind, "Hit_Earth": hit_earth,
        "Hit_Neutral": hit_neutral,
        "Jump": jump, "Dash": dash, "Hurt": hurt, "EnemyDie": enemy_die, "LevelUp": level_up,
        "RelicOpen": relic_open, "Switch_On": switch_on, "Switch_Tick": switch_tick,
        "GateOpen": gate_open, "Pickup": pickup, "ArenaLock": arena_lock, "StarShard": star_shard,
        "Land": land, "SquishHit": squish_hit, "EliteWarn": elite_warn,
        "Jingle_Clear": jingle_clear, "Jingle_Defeat": jingle_defeat,
    }
    only = [a for a in sys.argv[1:] if not a.startswith("-")]  # 이름을 주면 그 소리만 만든다 (나머지 파일은 그대로)
    if only:
        sfx = {k: v for k, v in sfx.items() if k in only}
    # 소리마다 난수 씨앗을 따로 둬서 한 소리를 고쳐도 다른 소리가 바뀌지 않게 한다.
    soft = {"Cast_Neutral": 0.13, "Jump": 0.1, "Dash": 0.12, "Switch_Tick": 0.08, "Land": 0.11, "SquishHit": 0.12, "StarShard": 0.1}  # 평타처럼 자주 나는 소리는 작고, tanh 압축 없이 부드럽게
    for name, fn in sfx.items():
        reseed(name)
        sound = fn()
        if name in soft:
            sound = normalize(sound, 1.0)
            sound *= soft[name] / np.sqrt(np.mean(sound ** 2))
        else:
            sound = loudness(sound)
        save_wav(fade_edges(sound), "SFX", f"{name}.wav")
    music = {"Menu": menu_theme, "Dungeon": dungeon_theme, "Dungeon2": dungeon2_theme, "Boss": boss_theme}
    if only:
        music = {k: v for k, v in music.items() if k in only}  # 곡 이름도 지정할 수 있다 (예: Boss)
    for name, fn in music.items():
        reseed(name)
        save_wav(fn(), "Music", f"{name}.wav")
    print("done")


if __name__ == "__main__":
    main()
