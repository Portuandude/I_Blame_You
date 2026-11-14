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


def coin():
    """골드 동전이 플레이어에 닿을 때: 짧은 동전 "칭" (네모파 B5 → E6 두 음, 아주 짧게), 0.18초. 동전이 여럿이면 연달아 난다."""
    n = int(0.18 * SR)
    out = np.zeros(n)
    m1 = int(0.05 * SR)
    mix_at(out, osc("square", 987.8, m1, duty=0.25) * env_exp(m1, 0.03), 0)          # B5
    m2 = n - int(0.045 * SR)
    mix_at(out, osc("square", 1318.5, m2, duty=0.25) * env_exp(m2, 0.06), int(0.045 * SR))  # E6
    out += 0.25 * osc("sine", 2637.0, n) * env_exp(n, 0.04)                            # 반짝
    return normalize(lowpass(fade_edges(out), 7000))


def coin_pile():
    """동전 무더기를 주울 때: 동전 다섯 개가 짤랑짤랑 쏟아지는 소리 (Coin과 같은 네모파, 음이 조금씩 오르며 0.06초 간격), 0.5초."""
    n = int(0.5 * SR)
    out = np.zeros(n)
    notes = [987.8, 1174.7, 1318.5, 1568.0, 1975.5]   # B5 D6 E6 G6 B6
    for i, f in enumerate(notes):
        m = int(0.12 * SR)
        start = int(i * 0.06 * SR)
        tone = osc("square", f, m, duty=0.25) * env_exp(m, 0.04)
        tone += 0.3 * osc("sine", f * 2, m) * env_exp(m, 0.03)  # 반짝
        mix_at(out, (0.7 + 0.075 * i) * tone, start)
    out += 0.15 * bandpass(noise(n), 4000, 9000) * env_exp(n, 0.12)  # 동전 부딪는 찰랑
    return normalize(lowpass(fade_edges(out), 7500))


def crit():
    """치명타가 들어갈 때: 높고 짧은 금속성 "팅" (세모파 A6 + 배음, 아주 빠른 감쇠) + 아주 짧은 잡음 딱, 0.14초. 적중음(Hit_)에 겹친다."""
    n = int(0.14 * SR)
    out = osc("tri", 1760.0, n) * env_exp(n, 0.035)
    out += 0.45 * osc("sine", 3520.0, n) * env_exp(n, 0.02)
    out += 0.2 * osc("sine", 5274.0, n) * env_exp(n, 0.012)
    m = int(0.012 * SR)
    mix_at(out, 0.5 * highpass(noise(m), 3000) * env_exp(m, 0.004), 0)
    return normalize(lowpass(fade_edges(out), 9000))


def combo():
    """연속 처치(N연속!): 오르는 세 음 아르페지오 "빠라랑" (네모파 E5 G5 C6, 0.05초 간격) + 반짝, 0.32초."""
    n = int(0.32 * SR)
    out = np.zeros(n)
    for i, f in enumerate([659.3, 784.0, 1046.5]):
        m = int(0.16 * SR)
        tone = osc("square", f, m, duty=0.25) * env_exp(m, 0.06)
        tone += 0.3 * osc("sine", f * 2, m) * env_exp(m, 0.04)
        mix_at(out, (0.75 + 0.12 * i) * tone, int(i * 0.05 * SR))
    return normalize(lowpass(fade_edges(out), 7000))


def notice():
    """적이 플레이어를 알아챘을 때(머리 위 "!"): 짧게 올라가는 두 음 "삐빅" (네모파 B5 → E6, 0.045초 간격), 0.13초. 자주 나므로 작게."""
    n = int(0.13 * SR)
    out = np.zeros(n)
    for i, f in enumerate([987.8, 1318.5]):
        m = int(0.07 * SR)
        tone = osc("square", f, m, duty=0.25) * env_exp(m, 0.03)
        tone += 0.25 * osc("sine", f * 2, m) * env_exp(m, 0.02)
        mix_at(out, (0.8 + 0.2 * i) * tone, int(i * 0.045 * SR))
    return normalize(lowpass(fade_edges(out), 7000))


def shield_up():
    """물 기사가 물방울 방패를 들 때: 아래에서 위로 부풀어 오르는 "뽀로롱" (위로 훑는 사인 + 작은 거품 세 알 + 얕은 물결 잡음), 0.32초."""
    n = int(0.32 * SR)
    t = np.arange(n) / SR
    f = 260.0 + 520.0 * (1 - np.exp(-t / 0.08))  # 낮게 시작해 부풀며 올라감
    body = osc("sine", f, n) * np.minimum(1.0, t / 0.03) * np.exp(-t / 0.12)
    wash = bandpass(noise(n), 500.0, 2200.0) * np.minimum(1.0, t / 0.06) * np.exp(-t / 0.08) * 0.35
    out = body + wash
    for start, base, gain in ((0.08, 900.0, 0.45), (0.15, 1200.0, 0.35), (0.21, 1500.0, 0.25)):
        m = int(0.06 * SR)
        k = np.arange(m) / SR
        bub = osc("sine", base * (1 + 0.6 * (1 - np.exp(-k / 0.01))), m) * env_exp(m, 0.015) * gain
        mix_at(out, bub, int(start * SR))
    return normalize(fade_edges(lowpass(out, 6000)))


def shield_block():
    """방패에 막힌 공격: 물 막에 튕기는 둔한 "퉁" (짧게 내려가는 사인 + 유리 같은 고음 한 점 + 아주 짧은 물 튐), 0.18초. 자주 나므로 작게."""
    n = int(0.18 * SR)
    t = np.arange(n) / SR
    thud = osc("sine", 520.0 * np.exp(-t * 9) + 180.0, n) * env_exp(n, 0.05)
    m = int(0.08 * SR)
    ting = osc("sine", 2300.0, m) * env_exp(m, 0.02) * 0.3
    splash = bandpass(noise(n), 1500.0, 5000.0) * env_exp(n, 0.02) * 0.4
    out = thud + splash
    mix_at(out, ting, 0)
    return normalize(fade_edges(lowpass(out, 7000)))


def enemy_windup():
    """적 공격 예고(웅크리기 시작): 짧게 빨아들이는 바람 "슉" (낮→높게 훑는 띠 잡음) + 아주 작은 나무 딸깍, 0.14초. 자주 나므로 작게."""
    n = int(0.14 * SR)
    t = np.arange(n) / SR
    env = np.minimum(1.0, t / 0.09) ** 2 * np.exp(-np.maximum(0.0, t - 0.09) / 0.015)  # 점점 커지다 뚝 끊김 (들이마심)
    hiss = np.zeros(n)
    raw = noise(n)
    for k, (lo, hi) in enumerate(((700.0, 1600.0), (1400.0, 3200.0), (2600.0, 5200.0))):
        seg = bandpass(raw, lo, hi)
        w = np.clip((t / 0.14) * 3 - k, 0.0, 1.0) * np.clip(k + 1 - (t / 0.14) * 3, 0.0, 1.0) + (0.3 if k == 1 else 0.0)
        hiss += seg * w
    out = hiss * env
    m = int(0.025 * SR)
    click = bandpass(noise(m), 1800.0, 3600.0) * env_exp(m, 0.004) * 0.5
    mix_at(out, click, int(0.095 * SR))
    return normalize(fade_edges(lowpass(out, 7000)))


def trap_warn():
    """화살 발사대 예고(붉은 깜빡임 시작): 시위를 감는 듯한 나무 톱니 "끼릭" (짧은 딸깍 3번이 빨라지며 높아짐) + 끝에 팽팽한 줄 울림, 0.3초."""
    n = int(0.3 * SR)
    out = np.zeros(n)
    for k, (at, f) in enumerate(((0.0, 1500.0), (0.06, 1900.0), (0.105, 2400.0))):
        m = int(0.03 * SR)
        click = bandpass(noise(m), f * 0.7, f * 1.6) * env_exp(m, 0.004)
        click += 0.4 * osc("tri", f * 0.5, m) * env_exp(m, 0.006)
        mix_at(out, (0.7 + 0.15 * k) * click, int(at * SR))
    start = int(0.13 * SR)
    m = n - start
    twang = osc("tri", np.linspace(330.0, 360.0, m), m) * env_exp(m, 0.05) * 0.5
    out[start:] += twang
    return normalize(fade_edges(lowpass(out, 8000)))


def flame_hiss():
    """불기둥 예고(연기): 가스가 새는 "쉬이익" — 높아지며 커지는 잡음 + 낮은 웅웅, 0.6초. 불이 솟을 때 Cast_Fire가 이어진다."""
    n = int(0.6 * SR)
    t = np.arange(n) / SR
    rise = np.clip(t / 0.5, 0, 1) ** 1.5
    hiss = lowpass(highpass(noise(n), 1500 + 2500 * rise), 6000 + 3000 * rise) * (0.25 + 0.75 * rise)
    rumble = osc("sine", 70 + 30 * rise, n) * 0.25 * rise
    return normalize(fade_edges(hiss + rumble, 20))


def magnet_whoosh():
    """보상 구슬이 자석으로 끌려오기 시작할 때: 작고 가벼운 "슝" (위로 미끄러지는 사인 + 바람 소리), 0.22초."""
    n = int(0.22 * SR)
    t = np.arange(n) / SR
    f = 500.0 + 900.0 * (t / t[-1]) ** 1.5                     # 500 → 1400Hz로 슝
    env = env_adsr(n, a=0.04, d=0.06, s=0.6, r=0.1)
    out = osc("sine", f, n) * env
    out += 0.5 * bandpass(noise(n), 1500, 5000) * env           # 바람결
    return normalize(fade_edges(out))


def blink():
    """순간이동: 위로 올라가는 반짝 "슝" (빠르게 오르는 사인 + 반짝이는 높은 음 세 개 + 가벼운 바람), 0.26초."""
    n = int(0.26 * SR)
    t = np.arange(n) / SR
    f = 420.0 * 2 ** (2.2 * np.clip(t / 0.14, 0, 1))             # 420 → 약 1930Hz로 휙
    out = osc("sine", f, n) * env_adsr(n, a=0.005, d=0.06, s=0.4, r=0.12)
    out += 0.45 * bandpass(noise(n), 2000, 7000) * env_adsr(n, a=0.02, d=0.05, s=0.3, r=0.1)
    for k, note in enumerate(("E6", "G#6", "B6")):                # 끝에 반짝반짝
        m = int(0.08 * SR)
        mix_at(out, 0.3 * osc("sine", note_hz(note), m) * env_exp(m, 0.025), int((0.07 + 0.035 * k) * SR))
    return normalize(fade_edges(lowpass(out, 9000)))


def fizzle():
    """실패(마나 부족·순간이동 불가·영창 오타): 낮게 꺼지는 "푸슉" (내려가는 네모파 + 바람 빠지는 잡음), 0.2초. 자주 날 수 있어 작게."""
    n = int(0.2 * SR)
    t = np.arange(n) / SR
    f = 330.0 * np.exp(-t * 6)                                    # 330 → 100Hz 남짓으로 쳐짐
    out = lowpass(osc("square", f, n, duty=0.35), 1800) * env_adsr(n, a=0.004, d=0.06, s=0.4, r=0.08) * 0.6
    out += lowpass(highpass(noise(n), 600), 3000) * env_exp(n, 0.05) * 0.5
    return normalize(fade_edges(out))


def step():
    """발소리: 작고 둥근 "톡" (짧은 낮은 사인 + 흙 잡음), 0.07초. 걸을 때마다 나므로 아주 작게, 높낮이는 코드가 번갈아 흔든다."""
    n = int(0.07 * SR)
    t = np.arange(n) / SR
    out = osc("sine", 160 * np.exp(-t * 30) + 90, n) * env_exp(n, 0.02)
    out += lowpass(noise(n), 1200) * env_exp(n, 0.012) * 0.6
    return normalize(fade_edges(out, 2))


def ui_click():
    """메뉴 버튼 누름: 작은 나무 "톡" (짧은 삼각파 두 겹 + 딸깍), 0.06초."""
    n = int(0.06 * SR)
    out = osc("tri", 1046.5, n) * env_exp(n, 0.012)
    out += 0.5 * osc("sine", 1568.0, n) * env_exp(n, 0.008)
    out += 0.3 * highpass(noise(n), 3000) * env_exp(n, 0.003)
    return normalize(fade_edges(out, 2))


def boss_near():
    """보스 방 바로 앞: 낮은 심장 "두-근" 두 번 (낮은 사인 쿵 두 개) + 아래로 깔리는 웅웅, 1.4초."""
    n = int(1.4 * SR)
    t = np.arange(n) / SR
    out = osc("sine", 48.0, n) * env_adsr(n, a=0.3, d=0.3, s=0.5, r=0.6) * 0.35    # 깔리는 웅웅
    for at, amp in ((0.05, 1.0), (0.3, 0.75), (0.75, 0.9), (1.0, 0.65)):
        m = int(0.22 * SR)
        tm = np.arange(m) / SR
        beat = osc("sine", 90 * np.exp(-tm * 14) + 45, m) * env_exp(m, 0.07)
        mix_at(out, amp * beat, int(at * SR))
    return normalize(fade_edges(lowpass(out, 1500), 30))


def summon():
    """전투 구역 적 소환(붉은 연기 속에서 나타남): 작고 둥근 "뿅" (아래에서 위로 튀는 사인) + 짧은 연기 "푸" 잡음, 0.2초. 차례로 여러 번 나므로 작게."""
    n = int(0.2 * SR)
    t = np.arange(n) / SR
    f = 260.0 + 640.0 * (1 - np.exp(-t / 0.03))               # 260 → 900Hz로 튀어 오름
    out = osc("sine", f, n) * env_adsr(n, a=0.004, d=0.06, s=0.25, r=0.1)
    puff = bandpass(noise(n), 500, 2500) * env_exp(n, 0.04) * 0.35
    return normalize(fade_edges(lowpass(out + puff, 4500)))


def room_pass():
    """방을 옮길 때: 부드러운 "휙" (아래로 미끄러지는 사인 + 지나가는 바람, 가운데서 가장 크게), 0.24초. Blink보다 낮고 덜 반짝이게."""
    n = int(0.24 * SR)
    t = np.arange(n) / SR
    f = 700.0 * 2 ** (-1.2 * (t / t[-1]))                       # 700 → 약 300Hz로 휙 지나감
    swell = np.sin(np.pi * np.clip(t / t[-1], 0, 1)) ** 1.5      # 가운데서 부풀었다 사라짐
    out = 0.5 * osc("sine", f, n) * swell
    out += 0.8 * bandpass(noise(n), 600, 3500) * swell           # 바람결
    return normalize(fade_edges(lowpass(out, 5000), 10))


def npc_talk():
    """마을 NPC 말풍선: 동물의 숲풍 짧은 "뽀로롱" 말소리 (네모파 음절 세 개가 오르내림 + 둥근 사인), 0.22초. 코드가 NPC마다 피치를 바꾼다."""
    n = int(0.22 * SR)
    out = np.zeros(n)
    for at, hz in ((0.0, 523.25), (0.07, 659.25), (0.14, 587.33)):
        m = int(0.07 * SR)
        syl = 0.45 * osc("square", hz, m, duty=0.35) + osc("sine", hz, m)
        mix_at(out, syl * env_adsr(m, a=0.005, d=0.03, s=0.5, r=0.025), int(at * SR))
    return normalize(fade_edges(lowpass(out, 3500), 3))


def pause_open():
    """일시정지 창 열기: 작은 종 "또롱" (위로 가는 두 음 G5→C6, 둥근 사인 + 살짝 반짝임), 0.22초. UiClick보다 부드럽게."""
    n = int(0.22 * SR)
    out = np.zeros(n)
    for at, hz in ((0.0, 783.99), (0.07, 1046.5)):
        m = n - int(at * SR)
        tone = osc("sine", hz, m) + 0.25 * osc("sine", hz * 2, m)
        mix_at(out, tone * env_exp(m, 0.06), int(at * SR))
    return normalize(fade_edges(lowpass(out, 6000), 3))


def pause_close():
    """일시정지 창 닫기 (다시 시작): 낮고 둥근 "통" (C5에서 살짝 내려가는 사인 + 나무 딸깍), 0.14초."""
    n = int(0.14 * SR)
    t = np.arange(n) / SR
    out = osc("sine", 523.25 * np.exp(-t * 2.5), n) * env_exp(n, 0.045)
    out += 0.25 * highpass(noise(n), 2500) * env_exp(n, 0.003)
    return normalize(fade_edges(lowpass(out, 5000), 3))


def run_start():
    """시작 화면에서 출발: 밝은 "따-링!" (위로 뛰는 세 음 G4→C5→E5 사각파 + 마지막 G5 종, 끝에 반짝임), 약 0.6초."""
    n = int(0.6 * SR)
    out = np.zeros(n)
    for at, name in ((0.0, "G4"), (0.06, "C5"), (0.12, "E5")):
        m = int(0.12 * SR)
        f = note_hz(name)
        tone_ = 0.3 * osc("square", f, m, duty=0.4) + 0.6 * osc("sine", f, m)
        mix_at(out, tone_ * env_adsr(m, a=0.004, d=0.05, s=0.4, r=0.04), int(at * SR))
    at = int(0.18 * SR)
    m = n - at
    bell = osc("sine", note_hz("G5"), m) + 0.3 * osc("sine", note_hz("G5") * 2, m) + 0.15 * osc("sine", note_hz("G5") * 3, m)
    mix_at(out, bell * env_exp(m, 0.16), at)
    sparkle = 0.12 * highpass(noise(m), 6000) * env_exp(m, 0.05)
    mix_at(out, sparkle, at)
    return normalize(fade_edges(lowpass(out, 7000), 3))


def chest_land():
    """유물 상자 착지: 묵직한 나무 "쿵" (낮은 사인 낙하 + 나무 판 울림) + 안에서 짤랑 (높은 종 두 개, 작게), 약 0.45초."""
    n = int(0.45 * SR)
    t = np.arange(n) / SR
    thud = osc("sine", 120 * np.exp(-t * 14) + 50, n) * env_exp(n, 0.09)
    wood = bandpass(noise(n), 250, 1200) * env_exp(n, 0.035) * 0.5
    knock = osc("tri", 210 * np.exp(-t * 6) + 140, n) * env_exp(n, 0.05) * 0.3
    out = thud + wood + knock
    for at, f in ((0.06, 2350.0), (0.11, 3140.0)):  # 상자 안 동전·보석 짤랑
        s0 = int(at * SR)
        m = n - s0
        out[s0:] += osc("sine", f, m) * env_exp(m, 0.08) * 0.16
    return normalize(fade_edges(lowpass(out, 6000)))


def chest_creak():
    """유물 상자 창 열기: 나무 뚜껑 "끼-익" (위로 휘는 짧은 마찰음, 톱니파 + 띠 잡음) 뒤에 안에서 빛 "샤랑" (높은 사인 두 음), 약 0.4초."""
    n = int(0.4 * SR)
    t = np.arange(n) / SR
    m = int(0.16 * SR)
    tm = np.arange(m) / SR
    creak = osc("saw", 260 + 320 * tm / 0.16, m) * env_adsr(m, a=0.02, d=0.05, s=0.6, r=0.05) * 0.35
    creak = bandpass(creak + 0.4 * bandpass(noise(m), 600, 1800) * env_exp(m, 0.05), 300, 2500)
    out = np.zeros(n)
    mix_at(out, creak, 0)
    for at, name in ((0.14, "E6"), (0.2, "B6")):
        s0 = int(at * SR)
        k = n - s0
        mix_at(out, (osc("sine", note_hz(name), k) + 0.2 * osc("sine", note_hz(name) * 2, k)) * env_exp(k, 0.09) * 0.4, s0)
    mix_at(out, 0.08 * highpass(noise(n - int(0.14 * SR)), 6000) * env_exp(n - int(0.14 * SR), 0.05), int(0.14 * SR))
    return normalize(fade_edges(lowpass(out, 8000), 3))


def relic_slot():
    """고른 유물 아이콘이 HUD 칸에 도착: 작은 "칭" (C7 사인 + 배음, 아주 짧게), 0.18초."""
    n = int(0.18 * SR)
    f = note_hz("C7")
    out = (osc("sine", f, n) + 0.3 * osc("sine", f * 1.5, n)) * env_exp(n, 0.05)
    out += 0.1 * highpass(noise(n), 7000) * env_exp(n, 0.015)
    return normalize(fade_edges(lowpass(out, 9000), 3))


def book_open():
    """특성 창 열기: 책 펼치는 "파라락" (짧은 종이 넘김 잡음 세 번, 점점 높게) + 끝에 작은 반짝 (E6), 약 0.35초."""
    n = int(0.35 * SR)
    out = np.zeros(n)
    for k, (at, lo) in enumerate(((0.0, 1500), (0.05, 2200), (0.1, 3000))):
        m = int(0.06 * SR)
        flap = bandpass(noise(m), lo, lo * 2.5) * env_adsr(m, a=0.005, d=0.02, s=0.4, r=0.03) * (0.6 + 0.2 * k)
        mix_at(out, flap, int(at * SR))
    s0 = int(0.15 * SR)
    k = n - s0
    mix_at(out, (osc("sine", note_hz("E6"), k) + 0.25 * osc("sine", note_hz("E6") * 2, k)) * env_exp(k, 0.07) * 0.35, s0)
    return normalize(fade_edges(lowpass(out, 8000), 3))


def heartbeat():
    """체력 30% 이하 경고: 부드러운 심장 "두-근" (낮은 사인 쿵 두 개, 0.13초 간격), 0.36초. 맥동마다 나므로 작고 둥글게."""
    n = int(0.36 * SR)
    out = np.zeros(n)
    for at, amp in ((0.0, 1.0), (0.13, 0.7)):
        m = int(0.2 * SR)
        tm = np.arange(m) / SR
        beat = osc("sine", 85 * np.exp(-tm * 16) + 48, m) * env_exp(m, 0.06)
        mix_at(out, amp * beat, int(at * SR))
    return normalize(fade_edges(lowpass(out, 1200), 6))


def vampire_heart():
    """흡혈 하트가 플레이어에 닿을 때: 작고 말랑한 "뽁" (아래→위로 튀는 둥근 사인 + 살짝 낮은 몸통), 0.18초."""
    n = int(0.18 * SR)
    t = np.arange(n) / SR
    f = 380.0 + 520.0 * (1 - np.exp(-t / 0.025))               # 380 → 900Hz로 빠르게 튀어 오르는 물방울
    out = osc("sine", f, n) * env_adsr(n, a=0.003, d=0.05, s=0.3, r=0.1)
    out += 0.35 * osc("sine", 190.0, n) * env_exp(n, 0.03)     # 말랑한 몸통
    return normalize(fade_edges(lowpass(out, 5000)))


def crumble():
    """무너지는 발판: 나무가 삐걱(아래로 처지는 톱니 마찰음) 하다가 '툭' 빠지는 소리 + 부스러기 잡음, 0.45초."""
    n = int(0.45 * SR)
    t = t_axis(0.45)
    creak_n = int(0.2 * SR)
    creak_f = np.linspace(330.0, 220.0, creak_n) * (1 + 0.04 * np.sin(2 * np.pi * 38 * t[:creak_n]))
    creak = bandpass(osc("saw", creak_f, creak_n), 300, 2500) * env_adsr(creak_n, a=0.02, d=0.05, s=0.6, r=0.1) * 0.5
    out = np.zeros(n)
    out[:creak_n] += creak
    drop = int(0.17 * SR)
    m = n - drop
    thud = osc("sine", np.linspace(160.0, 70.0, m), m) * env_exp(m, 0.09)
    knock = bandpass(noise(m), 600, 2400) * env_exp(m, 0.03) * 0.6
    debris = bandpass(noise(m), 2000, 6000) * env_exp(m, 0.12) * 0.15
    out[drop:] += thud + knock + debris
    return normalize(fade_edges(lowpass(out, 8000)))



def stone_crack():
    """종유석이 흔들림 (떨어지기 0.4초 전 예고): 낮게 웅웅 울리는 바위 떨림 + 자잘한 돌 부스러기 딱딱 소리, 0.4초."""
    n = int(0.4 * SR)
    t = t_axis(0.4)
    rumble = lowpass(noise(n), 180) * (0.6 + 0.4 * np.sin(2 * np.pi * 22 * t)) * env_adsr(n, a=0.05, d=0.1, s=0.8, r=0.12) * 1.6
    ticks = np.zeros(n)
    for k in range(7):
        start = int((0.03 + 0.05 * k + 0.012 * np.random.rand()) * SR)
        m = int(0.025 * SR)
        if start + m > n:
            break
        ticks[start:start + m] += bandpass(noise(m), 1800, 5200) * env_exp(m, 0.006) * (0.35 + 0.05 * k)
    return normalize(fade_edges(rumble + ticks))


def rock_shatter():
    """종유석이 땅에 부딪혀 부서짐: 묵직한 돌 '쿵' + 깨지는 잡음 + 수정 조각 '찰랑' 두 점 (귀엽게), 0.5초."""
    n = int(0.5 * SR)
    thud = osc("sine", np.linspace(140.0, 55.0, n), n) * env_exp(n, 0.08)
    crack = bandpass(noise(n), 700, 3500) * env_exp(n, 0.04) * 0.7
    grit = bandpass(noise(n), 3000, 8000) * env_exp(n, 0.1) * 0.15
    out = thud + crack + grit
    for delay, note in ((0.05, "E6"), (0.11, "B6")):
        start = int(delay * SR)
        m = n - start
        out[start:] += 0.18 * osc("sine", note_hz(note), m) * env_exp(m, 0.06)
    return normalize(fade_edges(lowpass(out, 9000)))

def jelly_squish():
    """젤리 방석으로 버티는 발판: 말랑하게 눌렸다 펴지는 "뭉-뿅" (아래로 처지는 둥근 사인 + 떨림, 끝에 위로 튀는 작은 뿅), 0.5초."""
    n = int(0.5 * SR)
    t = np.arange(n) / SR
    squash_n = int(0.32 * SR)
    ts = t[:squash_n]
    f = (260.0 - 110.0 * (1 - np.exp(-ts / 0.08))) * (1 + 0.06 * np.sin(2 * np.pi * 9 * ts))  # 260 → 150Hz로 처지며 말랑 떨림
    out = np.zeros(n)
    out[:squash_n] += osc("sine", f, squash_n) * env_adsr(squash_n, a=0.01, d=0.08, s=0.6, r=0.15)
    out[:squash_n] += 0.3 * osc("tri", f * 2, squash_n) * env_exp(squash_n, 0.06)
    pop_start = int(0.36 * SR)
    m = n - pop_start
    pf = 300.0 + 600.0 * (1 - np.exp(-np.arange(m) / SR / 0.02))
    out[pop_start:] += 0.7 * osc("sine", pf, m) * env_exp(m, 0.04)  # 위로 튀는 뿅
    return normalize(fade_edges(lowpass(out, 4500)))


def jelly_drip():
    """젤리 방울이 튈 때 아주 작은 물방울 "똑똑": 위로 휘는 짧은 사인 두 번(두 번째는 조금 높고 작게), 0.26초."""
    n = int(0.26 * SR)
    out = np.zeros(n)
    for start, base, gain in ((0.0, 900.0, 1.0), (0.11, 1150.0, 0.7)):
        m = int(0.09 * SR)
        k = np.arange(m) / SR
        f = base * (1 + 0.8 * (1 - np.exp(-k / 0.012)))  # 위로 톡 휘는 물방울
        drop = osc("sine", f, m) * env_exp(m, 0.022) * gain
        mix_at(out, drop, int(start * SR))
    return normalize(fade_edges(lowpass(out, 6000)))


def crumble_back():
    """무너졌던 발판이 다시 생김: 작고 가벼운 나무 '톡' (짧은 삼각파 + 맑은 고음 한 점), 0.15초."""
    n = int(0.15 * SR)
    tok = osc("tri", note_hz("E5"), n) * env_exp(n, 0.03)
    knock = bandpass(noise(n), 1800, 4500) * env_exp(n, 0.012) * 0.4
    ping = 0.25 * osc("sine", note_hz("B6"), n) * env_exp(n, 0.05)
    return normalize(fade_edges(tok + knock + ping))


def relic_new():
    """도감에 처음 오른 유물: RelicOpen 아르페지오 뒤에 겹쳐 울리는 짧은 반짝 화음 (G장조 G6·B6·D7 동시 + 높은 반짝임), 0.6초."""
    n = int(0.6 * SR)
    out = np.zeros(n)
    for k, (f, gain) in enumerate([(1568.0, 0.5), (1975.5, 0.4), (2349.3, 0.35)]):  # G6 B6 D7
        m = n - int(k * 0.015 * SR)  # 아주 살짝 흩어 쳐서 "촤랑"
        tone_ = (osc("sine", f, m) + 0.25 * osc("tri", f * 2, m)) * env_exp(m, 0.18) * gain
        mix_at(out, tone_, n - m)
    out += bandpass(noise(n), 6000, 10000) * env_exp(n, 0.12) * 0.08
    return normalize(fade_edges(lowpass(out, 10000)))


def trait_up():
    """특성 한 점 찍기: 맑은 "띠링" (E6 → A6 두 음, 사인 + 살짝 세모파), 0.3초. LevelUp보다 짧고 작게."""
    n = int(0.3 * SR)
    out = np.zeros(n)
    for k, f in enumerate([1318.5, 1760.0]):  # E6 A6
        m = n - int(k * 0.06 * SR)
        tone_ = (osc("sine", f, m) + 0.2 * osc("tri", f * 2, m)) * env_exp(m, 0.12) * (0.6 if k == 0 else 0.8)
        mix_at(out, tone_, n - m)
    return normalize(fade_edges(lowpass(out, 9000)))


def trait_milestone():
    """특성 5·10·20 단계 달성: 올라가는 아르페지오(C6 E6 G6) 뒤 C7 화음 + 높은 반짝임, 0.8초. TraitUp보다 크고 화려하게."""
    n = int(0.8 * SR)
    out = np.zeros(n)
    for k, f in enumerate([1046.5, 1318.5, 1568.0]):  # C6 E6 G6
        m = int(0.35 * SR)
        tone_ = (osc("sine", f, m) + 0.3 * osc("square", f, m, duty=0.5) * 0.4) * env_exp(m, 0.15) * 0.5
        mix_at(out, tone_, int(k * 0.06 * SR))
    start = int(0.2 * SR)
    m = n - start
    for f, gain in [(2093.0, 0.5), (2637.0, 0.35), (3136.0, 0.3)]:  # C7 E7 G7 함께
        tone_ = (osc("sine", f, m) + 0.2 * osc("tri", f * 2, m)) * env_exp(m, 0.25) * gain
        mix_at(out, tone_, start)
    out += bandpass(noise(n), 6000, 11000) * env_exp(n, 0.2) * 0.07
    return normalize(fade_edges(lowpass(out, 11000)))


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


def enrage():
    """광폭화: 낮은 톱니 으르렁(빠른 트레몰로)이 한 옥타브 위로 끌려 올라가고 끝에 쿵, 약 0.8초. 무섭지만 짧게."""
    n = int(0.8 * SR)
    t = np.arange(n) / SR
    f = note_hz("E2") * (1 + np.clip(t / 0.6, 0, 1))  # 0.6초 동안 한 옥타브 위로
    growl = osc("saw", f, n) * (0.55 + 0.45 * np.sin(2 * np.pi * 22 * t))
    growl = lowpass(growl, 1200) * env_adsr(n, 0.02, 0.1, 0.8, 0.2) * 0.55
    rasp = bandpass(noise(n), 300, 1500) * env_adsr(n, 0.02, 0.1, 0.5, 0.2) * 0.15
    out = growl + rasp
    m = int(0.3 * SR)
    tb = np.arange(m) / SR
    mix_at(out, osc("sine", 110 * np.exp(-tb * 12) + 40, m) * env_exp(m, 0.12) * 0.8, int(0.5 * SR))
    return normalize(fade_edges(out))


def knight_link():
    """쌍둥이 기사 한쪽이 쓰러질 때: 내려가는 단조 두 음(A5→E5, 유리종) 위로 남은 기사에게 끌려가는 "쉬익" 바람, 약 0.7초."""
    n = int(0.7 * SR)
    t = np.arange(n) / SR
    out = np.zeros(n)
    for k, (name, gain) in enumerate((("A5", 0.55), ("E5", 0.7))):
        m = int((0.7 - k * 0.16) * SR)
        bell = (osc("sine", note_hz(name), m) + 0.35 * osc("sine", note_hz(name) * 2.01, m)) * env_exp(m, 0.18) * gain
        mix_at(out, bell, int(k * 0.16 * SR))
    swish = bandpass(noise(n), 700.0, 3000.0) * np.sin(np.pi * np.clip(t / 0.55, 0, 1)) ** 2 * 0.25
    return normalize(fade_edges(lowpass(out + swish, 7000)))


def enrage_end():
    """광폭화가 풀릴 때: 바람 빠지듯 내려가는 "푸슈우" (내려가는 사인 + 숨소리 잡음), 약 0.6초. 기운이 빠진 느낌."""
    n = int(0.6 * SR)
    t = np.arange(n) / SR
    f = 420.0 * np.exp(-t * 2.2) + 90.0
    body = osc("sine", f, n) * np.minimum(1.0, t / 0.02) * np.exp(-t / 0.3) * 0.6
    breath = bandpass(noise(n), 400.0, 2400.0) * np.minimum(1.0, t / 0.05) * np.exp(-t / 0.22) * 0.4
    return normalize(fade_edges(lowpass(body + breath, 5000)))


def slime_split():
    """슬라임 분열(슬라임 킹 2페이즈, 어미 슬라임 사망): 젤리가 찢어지는 질척한 "뿌직" (낮은 띠 잡음) 뒤에
    둘로 갈라져 튀어 나가는 "쪼옥-뽁" (위로 훑는 사인 두 개, 두 번째가 조금 늦고 높게), 약 0.55초."""
    n = int(0.55 * SR)
    t = np.arange(n) / SR
    out = np.zeros(n)
    rip = bandpass(noise(n), 200.0, 1400.0) * np.minimum(1.0, t / 0.01) * np.exp(-t / 0.09) * 0.8
    out += rip
    for k, (base, gain) in enumerate(((260.0, 0.7), (380.0, 0.6))):
        m = int(0.22 * SR)
        tt = np.arange(m) / SR
        bloop = osc("sine", base + 900.0 * (1 - np.exp(-tt * 18)), m) * np.minimum(1.0, tt / 0.005) * np.exp(-tt / 0.07) * gain
        mix_at(out, bloop, int((0.09 + k * 0.11) * SR))
    return normalize(fade_edges(lowpass(out, 6000)))


def tentacle_lash():
    """슬라임 킹 촉수가 뻗어 나갈 때: 짧고 질척한 "휙" (위로 훑는 띠 잡음 + 낮은 사인), 0.3초."""
    n = int(0.3 * SR)
    t = np.arange(n) / SR
    env = np.minimum(1.0, t / 0.02) * np.exp(-t / 0.08)
    whoosh = bandpass(noise(n), 400.0, 2600.0) * env * 0.7
    body = osc("sine", 160.0 + 260.0 * np.clip(t / 0.12, 0, 1), n) * env * 0.5
    return normalize(fade_edges(lowpass(whoosh + body, 5000)))


def tentacle_slap():
    """촉수 끝이 벽·바닥에 부딪힐 때: 둔탁하고 젖은 "철퍽" (낮은 잡음 터짐 + 아래로 떨어지는 사인), 0.22초."""
    n = int(0.22 * SR)
    t = np.arange(n) / SR
    splat = bandpass(noise(n), 150.0, 1200.0) * np.minimum(1.0, t / 0.003) * np.exp(-t / 0.04) * 0.9
    thud = osc("sine", 90.0 + 160.0 * np.exp(-t * 30), n) * np.exp(-t / 0.06) * 0.7
    return normalize(fade_edges(lowpass(splat + thud, 4000)))


def tentacle_grab():
    """촉수가 플레이어에게 달라붙을 때: 끈적한 "찰싹" (짧은 클릭 + 늘어나는 젤리 사인 흔들림), 0.4초."""
    n = int(0.4 * SR)
    t = np.arange(n) / SR
    smack = highpass(noise(n), 1500.0) * np.exp(-t / 0.012) * 0.6
    f = 240.0 + 60.0 * np.sin(2 * np.pi * 9.0 * t)
    stretch = osc("sine", f, n) * np.minimum(1.0, t / 0.01) * np.exp(-t / 0.15) * 0.6
    goo = bandpass(noise(n), 300.0, 1000.0) * np.exp(-t / 0.08) * 0.3
    return normalize(fade_edges(lowpass(smack + stretch + goo, 6000)))


def tentacle_snap():
    """붙은 촉수를 끊어냈을 때: 팽팽하다 끊기는 "뽁" (오르는 짧은 사인 + 터지는 잡음) 뒤에 밝은 한 음(C6), 0.45초."""
    n = int(0.45 * SR)
    t = np.arange(n) / SR
    out = np.zeros(n)
    m = int(0.12 * SR)
    tt = np.arange(m) / SR
    pop = osc("sine", 300.0 + 1100.0 * (1 - np.exp(-tt * 25)), m) * np.minimum(1.0, tt / 0.003) * np.exp(-tt / 0.04)
    pop += bandpass(noise(m), 800.0, 4000.0) * np.exp(-tt / 0.015) * 0.4
    mix_at(out, pop, 0)
    m = int(0.3 * SR)
    bell = osc("sine", note_hz("C6"), m) * env_exp(m, 0.1) * 0.3
    mix_at(out, bell, int(0.1 * SR))
    return normalize(fade_edges(lowpass(out, 7000)))


def king_fall():
    """슬라임 킹 내려찍기가 떨어지기 시작할 때: 아래로 휘는 짧은 "휘이익" (내려가는 사인 + 바람 띠 잡음), 0.35초."""
    n = int(0.35 * SR)
    t = np.arange(n) / SR
    env = np.minimum(1.0, t / 0.04) * np.exp(-np.maximum(0.0, t - 0.2) / 0.06)
    whistle = osc("sine", 900.0 - 600.0 * (t / t[-1]), n) * env * 0.45
    wind = bandpass(noise(n), 500.0, 2500.0) * env * 0.5
    return normalize(fade_edges(lowpass(whistle + wind, 6000)))


def king_slam():
    """슬라임 킹 내려찍기 착지: 무겁고 젖은 "철퍼덕" (아주 낮은 사인 쿵 + 넓은 젤리 잡음 + 꼬리 출렁임), 0.6초."""
    n = int(0.6 * SR)
    t = np.arange(n) / SR
    thud = osc("sine", 55.0 + 120.0 * np.exp(-t * 18), n) * np.exp(-t / 0.18)
    splat = bandpass(noise(n), 120.0, 1500.0) * np.minimum(1.0, t / 0.004) * np.exp(-t / 0.07) * 0.8
    wobble = osc("sine", 140.0 + 25.0 * np.sin(2 * np.pi * 7.0 * t), n) * np.exp(-np.maximum(0.0, t - 0.05) / 0.12) * 0.3 * (t > 0.05)
    return normalize(fade_edges(lowpass(thud + splat + wobble, 4500)))


def king_land():
    """슬라임 킹 박치기 착지: 둔하고 짧은 "쿵" (낮은 사인 + 작은 질척 잡음), 0.3초."""
    n = int(0.3 * SR)
    t = np.arange(n) / SR
    thud = osc("sine", 70.0 + 90.0 * np.exp(-t * 25), n) * np.exp(-t / 0.09)
    goo = bandpass(noise(n), 200.0, 1000.0) * np.minimum(1.0, t / 0.003) * np.exp(-t / 0.04) * 0.5
    return normalize(fade_edges(lowpass(thud + goo, 4000)))


def bow_twang():
    """고블린 궁수가 시위를 놓을 때: 짧게 울리는 "팅" (빠르게 떨어지는 줄 소리 + 작은 딸깍), 0.25초."""
    n = int(0.25 * SR)
    t = np.arange(n) / SR
    f = 330.0 + 140.0 * np.exp(-t * 40)
    string = (osc("tri", f, n) * 0.6 + osc("sine", f * 2.0, n) * 0.25) * np.minimum(1.0, t / 0.002) * np.exp(-t / 0.06)
    click = highpass(noise(n), 2500.0) * np.exp(-t / 0.006) * 0.4
    return normalize(fade_edges(lowpass(string + click, 7000)))


def arrow_whiz():
    """화살이 플레이어 곁을 스쳐 지나갈 때: 가볍게 내려가는 "슈웅" (좁은 띠 잡음, 앞이 크고 뒤가 낮게), 0.3초."""
    n = int(0.3 * SR)
    t = np.arange(n) / SR
    env = np.minimum(1.0, t / 0.03) * np.exp(-t / 0.09)
    centre = 3200.0 - 1800.0 * (t / t[-1])
    whoosh = bandpass(noise(n), 900.0, 4200.0) * env * 0.7
    tone = osc("sine", centre, n) * env * 0.12
    return normalize(fade_edges(lowpass(whoosh + tone, 6500)))


def arrow_thunk():
    """화살이 벽·바닥에 박힐 때: 마른 나무 "턱" (짧은 클릭 + 짧게 떨리는 낮은 음), 0.2초."""
    n = int(0.2 * SR)
    t = np.arange(n) / SR
    knock = bandpass(noise(n), 600.0, 3000.0) * np.minimum(1.0, t / 0.002) * np.exp(-t / 0.012) * 0.8
    wobble = osc("sine", 210.0 + 20.0 * np.sin(2 * np.pi * 28.0 * t), n) * np.exp(-t / 0.05) * 0.5
    return normalize(fade_edges(lowpass(knock + wobble, 5000)))


def goblin_swing():
    """고블린이 무기를 휘두를 때: 묵직하게 공기를 가르는 "붕" (넓은 띠 잡음이 부풀었다 빠지고, 낮게 내려가는 사인), 0.22초."""
    n = int(0.22 * SR)
    t = np.arange(n) / SR
    env = np.sin(np.pi * np.clip(t / 0.16, 0.0, 1.0)) ** 1.5
    whoosh = bandpass(noise(n), 300.0, 2200.0) * env * 0.8
    hum = osc("sine", 190.0 - 70.0 * (t / t[-1]), n) * env * 0.25
    return normalize(fade_edges(lowpass(whoosh + hum, 4500)))


def spike_stab():
    """가시를 밟았을 때: 날카로운 쇠 "찡" (높은 금속 울림 두 개 + 짧은 긁힘), 0.2초."""
    n = int(0.2 * SR)
    t = np.arange(n) / SR
    env = np.minimum(1.0, t / 0.002) * np.exp(-t / 0.05)
    ring = (osc("sine", 2350.0, n) + osc("sine", 3420.0, n) * 0.6) * env * 0.5
    scrape = bandpass(noise(n), 2500.0, 7000.0) * np.exp(-t / 0.012) * 0.6
    return normalize(fade_edges(lowpass(ring + scrape, 9000)))


def flame_sizzle():
    """불기둥에 닿았을 때: 짧게 타는 "치익" (높은 잡음이 빠르게 옅어지고 탁탁 튀는 소리), 0.25초."""
    n = int(0.25 * SR)
    t = np.arange(n) / SR
    hiss = highpass(noise(n), 2500.0) * np.minimum(1.0, t / 0.005) * np.exp(-t / 0.08) * 0.7
    crackle = (RNG.random(n) < 0.004).astype(float) * np.exp(-t / 0.12)
    crackle = bandpass(crackle, 800.0, 4000.0) * 3.0
    return normalize(fade_edges(lowpass(hiss + crackle, 8000)))


def pit_fall():
    """낭떠러지로 떨어질 때: 만화처럼 내려가는 "휘이이잉" (길게 미끄러져 내려가는 사인 + 옅은 바람), 0.5초."""
    n = int(0.5 * SR)
    t = np.arange(n) / SR
    f = 1400.0 * 2 ** (-2.3 * (t / t[-1]))
    env = np.minimum(1.0, t / 0.03) * np.clip((t[-1] - t) / 0.15, 0, 1)
    tone = osc("sine", f, n) * env * 0.6
    wind = bandpass(noise(n), 500.0, 2500.0) * env * 0.25
    return normalize(fade_edges(lowpass(tone + wind, 5000)))


def pit_return():
    """안전한 땅으로 돌아올 때: 부드럽게 나타나는 "뿅" (오르는 사인 + 반짝이는 높은 음 둘), 0.3초."""
    n = int(0.3 * SR)
    t = np.arange(n) / SR
    f = 330.0 * 2 ** (1.5 * np.clip(t / 0.1, 0, 1))
    out = osc("sine", f, n) * np.minimum(1.0, t / 0.005) * np.exp(-t / 0.09) * 0.7
    for k, freq in enumerate((1568.0, 2093.0)):
        start = int((0.06 + 0.06 * k) * SR)
        m = n - start
        tt = np.arange(m) / SR
        out[start:] += osc("sine", freq, m) * np.exp(-tt / 0.06) * 0.25
    return normalize(fade_edges(lowpass(out, 8000)))


def platform_wake():
    """잠든 스위치 발판이 깨어날 때: 낮게 차오르는 "우웅" + 끝에 맑은 "팅", 0.45초."""
    n = int(0.45 * SR)
    t = np.arange(n) / SR
    f = 196.0 * 2 ** (1.0 * np.clip(t / 0.25, 0, 1))
    hum = (osc("sine", f, n) + osc("tri", f * 2.0, n) * 0.25) * np.minimum(1.0, t / 0.08) * np.exp(-t / 0.22) * 0.6
    start = int(0.2 * SR)
    m = n - start
    tt = np.arange(m) / SR
    hum[start:] += (osc("sine", 1318.5, m) + osc("sine", 1975.5, m) * 0.4) * np.exp(-tt / 0.08) * 0.3
    return normalize(fade_edges(lowpass(hum, 9000)))


def platform_sleep():
    """스위치 발판이 다시 잠들 때: 내려가는 작은 종소리 셋, 0.55초."""
    n = int(0.55 * SR)
    out = np.zeros(n)
    for k, freq in enumerate((1318.5, 1046.5, 784.0)):
        m = int(0.3 * SR)
        tt = np.arange(m) / SR
        bell = (osc("sine", freq, m) + osc("sine", freq * 2.76, m) * 0.12) * np.minimum(1.0, tt / 0.003) * np.exp(-tt / 0.1) * (0.5 - 0.1 * k)
        mix_at(out, bell, int(k * 0.1 * SR))
    return normalize(fade_edges(lowpass(out, 8000)))


def blink_appear():
    """깜빡 발판이 다시 나타날 때: 작고 맑은 "띵" (높은 울림 하나 + 옅은 배음), 0.18초."""
    n = int(0.18 * SR)
    t = np.arange(n) / SR
    ring = (osc("sine", 2093.0, n) + osc("sine", 3139.5, n) * 0.25) * np.minimum(1.0, t / 0.002) * np.exp(-t / 0.05) * 0.6
    return normalize(fade_edges(lowpass(ring, 10000)))


def blink_vanish():
    """서 있던 깜빡 발판이 꺼질 때: 아래로 떨어지는 "퐁" (짧은 음높이 내림 + 공기 빠지는 잡음), 0.2초."""
    n = int(0.2 * SR)
    t = np.arange(n) / SR
    f = 880.0 * 2 ** (-1.2 * np.clip(t / 0.12, 0, 1))
    pop = (osc("sine", f, n) + osc("tri", f * 0.5, n) * 0.3) * np.minimum(1.0, t / 0.003) * np.exp(-t / 0.06) * 0.6
    air = bandpass(noise(n), 1500.0, 5000.0) * np.exp(-t / 0.02) * 0.15
    return normalize(fade_edges(lowpass(pop + air, 8000)))


def platform_stop():
    """움직이는 발판이 끝에 닿아 멈출 때: 나무 판자 같은 작은 "탁" (짧은 낮은 울림 + 딸깍 잡음), 0.12초."""
    n = int(0.12 * SR)
    t = np.arange(n) / SR
    knock = (osc("sine", 330.0, n) + osc("tri", 495.0, n) * 0.4) * np.minimum(1.0, t / 0.002) * np.exp(-t / 0.025) * 0.6
    click = bandpass(noise(n), 1200.0, 4000.0) * np.exp(-t / 0.006) * 0.35
    return normalize(fade_edges(lowpass(knock + click, 7000)))


def mushroom_boing():
    """통통 버섯에 튕겨 오를 때: 말랑한 "보잉" (아래로 눌렸다 위로 휘어 오르는 울림 + 살짝 떨림), 0.35초."""
    n = int(0.35 * SR)
    t = np.arange(n) / SR
    sweep = 220.0 + 520.0 * np.clip((t - 0.03) / 0.22, 0, 1) ** 0.7  # 처음 0.03초 낮게 눌렸다가 위로
    wobble = 1.0 + 0.04 * np.sin(2 * np.pi * 18.0 * t)
    body = (osc("sine", sweep * wobble, n) + osc("tri", sweep * 2.0 * wobble, n) * 0.25) * np.minimum(1.0, t / 0.004) * np.exp(-t / 0.12)
    thump = osc("sine", 110.0, n) * np.exp(-t / 0.02) * 0.5
    return normalize(fade_edges(lowpass(body + thump, 6000)))


def wind_gust():
    """바람 구멍 기둥에 들어갈 때: 위로 솟는 "휘잉" (잡음을 높은 쪽으로 훑는 띠 + 옅은 휘파람), 0.45초."""
    n = int(0.45 * SR)
    t = np.arange(n) / SR
    env = np.minimum(1.0, t / 0.06) * np.exp(-np.maximum(0.0, t - 0.08) / 0.14)
    hiss = noise(n)
    low = bandpass(hiss, 500, 1400)
    high = bandpass(hiss, 1800, 4200)
    rise = np.clip(t / 0.3, 0, 1)
    air = (low * (1.0 - rise) + high * rise) * env
    whistle = osc("sine", 700.0 + 900.0 * rise, n) * env * 0.12
    return normalize(fade_edges(air + whistle))


def gate_deflect():
    """봉인문이 틀린 마법을 튕겨낼 때: 유리 같은 "팅" (높은 울림 둘이 살짝 아래로 휘고 짧은 잡음), 0.25초."""
    n = int(0.25 * SR)
    t = np.arange(n) / SR
    bend = 1.0 - 0.06 * np.clip(t / 0.08, 0, 1)
    ring = (osc("sine", 1760.0 * bend, n) + osc("sine", 2640.0 * bend, n) * 0.5) * np.minimum(1.0, t / 0.002) * np.exp(-t / 0.07) * 0.6
    tick = bandpass(noise(n), 3000.0, 8000.0) * np.exp(-t / 0.008) * 0.4
    return normalize(fade_edges(lowpass(ring + tick, 10000)))


def gate_shatter():
    """봉인문 벽이 조각나 흩어질 때: 유리 조각이 반짝이며 흩어지는 "차르르" (높은 짧은 음 여러 개가 흩뿌려짐), 0.7초."""
    n = int(0.7 * SR)
    out = np.zeros(n)
    for k in range(14):
        start = int(RNG.uniform(0.0, 0.45) * SR)
        m = int(0.12 * SR)
        f = RNG.uniform(2000.0, 4200.0)
        tt = np.arange(m) / SR
        mix_at(out, osc("sine", f, m) * np.exp(-tt / 0.03) * RNG.uniform(0.15, 0.35), start)
    t = np.arange(n) / SR
    out += bandpass(noise(n), 4000.0, 9000.0) * np.exp(-t / 0.12) * 0.15
    return normalize(fade_edges(lowpass(out, 11000)))


def slime_hop():
    """슬라임이 도약할 때: 작고 귀여운 "뾱" (빠르게 오르는 사인 + 젖은 잡음 조금), 0.15초."""
    n = int(0.15 * SR)
    t = np.arange(n) / SR
    env = np.minimum(1.0, t / 0.004) * np.exp(-t / 0.04)
    pop = osc("sine", 260.0 + 520.0 * (1 - np.exp(-t * 35)), n) * env * 0.7
    wet = bandpass(noise(n), 400.0, 1800.0) * np.exp(-t / 0.015) * 0.25
    return normalize(fade_edges(lowpass(pop + wet, 6000)))


def slime_plop():
    """슬라임이 떨어져 착지할 때: 말랑한 "퐁" (내려가는 사인 + 젖은 잡음 + 짧은 출렁임), 0.25초."""
    n = int(0.25 * SR)
    t = np.arange(n) / SR
    body = osc("sine", 140.0 + 220.0 * np.exp(-t * 30), n) * np.minimum(1.0, t / 0.003) * np.exp(-t / 0.07) * 0.8
    wet = bandpass(noise(n), 250.0, 1400.0) * np.exp(-t / 0.025) * 0.4
    wobble = osc("sine", 180.0 + 20.0 * np.sin(2 * np.pi * 12.0 * t), n) * np.exp(-t / 0.08) * 0.2
    return normalize(fade_edges(lowpass(body + wet + wobble, 5000)))


def brood_tick():
    """슬라임 무리 합체 임박(마지막 5초) 초마다: 젤리 같은 짧은 "똑" (아래로 휘는 사인 + 아주 작은 클릭), 0.14초."""
    n = int(0.14 * SR)
    t = np.arange(n) / SR
    f = 700.0 * np.exp(-t * 9.0) + 380.0
    body = osc("sine", f, n) * np.minimum(1.0, t / 0.003) * np.exp(-t / 0.04)
    click = highpass(noise(n), 3000.0) * np.exp(-t / 0.006) * 0.25
    return normalize(fade_edges(lowpass(body + click, 6000)))


def brood_merge():
    """남은 슬라임이 킹에게 빨려 들어갈 때: 길게 위로 끌려가는 "슈우욱" (오르는 사인 + 질척한 띠 잡음), 0.8초."""
    n = int(0.8 * SR)
    t = np.arange(n) / SR
    f = 180.0 + 520.0 * (t / t[-1]) ** 2
    env = np.sin(np.pi * np.clip(t / 0.8, 0, 1)) ** 1.5
    body = osc("sine", f, n) * env * 0.6
    goo = bandpass(noise(n), 300.0, 1800.0) * env * 0.35
    return normalize(fade_edges(lowpass(body + goo, 5000)))


def brood_gulp():
    """킹이 무리를 삼킬 때: 낮고 둥근 "꿀꺽" (내려갔다 오르는 낮은 사인 두 번) + 회복 반짝임 두 음, 약 0.6초."""
    n = int(0.6 * SR)
    out = np.zeros(n)
    for k, (base, gain) in enumerate(((150.0, 1.0), (120.0, 0.8))):
        m = int(0.16 * SR)
        tt = np.arange(m) / SR
        f = base + 140.0 * np.sin(np.pi * tt / tt[-1])
        gulp = osc("sine", f, m) * np.minimum(1.0, tt / 0.01) * np.exp(-tt / 0.07) * gain
        mix_at(out, gulp, int(k * 0.13 * SR))
    for k, name in enumerate(("E6", "A6")):
        m = int(0.25 * SR)
        bell = osc("sine", note_hz(name), m) * env_exp(m, 0.1) * 0.25
        mix_at(out, bell, int((0.3 + k * 0.07) * SR))
    return normalize(fade_edges(lowpass(out, 7000)))


def brood_clear():
    """소환된 슬라임 무리를 제한 시간 안에 모두 처치: 밝게 오르는 세 음(G5 C6 E6) + 반짝임, 0.6초."""
    n = int(0.6 * SR)
    out = np.zeros(n)
    for k, name in enumerate(("G5", "C6", "E6")):
        m = int(0.3 * SR)
        tone_ = (osc("sine", note_hz(name), m) + 0.25 * osc("tri", note_hz(name) * 2, m)) * env_exp(m, 0.12) * 0.55
        mix_at(out, tone_, int(k * 0.07 * SR))
    m = n - int(0.22 * SR)
    sparkle = highpass(noise(m), 5000.0) * env_exp(m, 0.08) * 0.12
    mix_at(out, sparkle, int(0.22 * SR))
    return normalize(fade_edges(out))


def village_found():
    """마을 첫 발견: 오르골 세 음 G5-C6-E6 + 마지막 음 길게, 약 1.4초 (music_box는 배경음악 쪽에 있다)."""
    n = int(1.4 * SR)
    out = np.zeros(n)
    for k, name in enumerate(["G5", "C6", "E6"]):
        mix_at(out, music_box(name, 1.4 - k * 0.14) * (0.6 if k < 2 else 0.8), int(k * 0.14 * SR))
    return out


def shop_bell():
    """가게 문 종: 높은 종 두 번 딸랑 (작게), 약 0.5초."""
    n = int(0.5 * SR)
    out = np.zeros(n)
    for k, f in enumerate((2093.0, 2637.0)):
        m = int(0.42 * SR)
        ring = (osc("sine", f, m) + 0.4 * osc("sine", f * 2.76, m) * env_exp(m, 0.04)) * env_exp(m, 0.18)
        mix_at(out, ring * (0.7 if k == 0 else 0.55), int(k * 0.07 * SR))
    return out


def reroll():
    """수식어 바꾸기: 주사위 데구르르 (짧은 딸깍 5번, 점점 느려짐) + 끝에 톡, 약 0.6초."""
    n = int(0.6 * SR)
    out = np.zeros(n)
    t = 0.0
    for k in range(5):
        m = int(0.03 * SR)
        click = bandpass(noise(m), 1800, 6000) * env_exp(m, 0.008) + osc("tri", 900 - k * 60, m) * env_exp(m, 0.01) * 0.4
        mix_at(out, click, int(t * SR))
        t += 0.06 + k * 0.025
    m = int(0.12 * SR)
    mix_at(out, osc("sine", 660, m) * env_exp(m, 0.05) * 0.6, int(t * SR))
    return out


def boss_defeat():
    """보스·중간보스 격파: 묵직한 "쾅" (낮은 사인 낙하 + 터지는 잡음) 뒤 G-C-E-G-C 반짝 아르페지오가 위로 올라가고 종소리로 끝, 약 1.4초."""
    n = int(1.4 * SR)
    out = np.zeros(n)
    t = np.arange(n) / SR
    boom = osc("sine", 110 * np.exp(-t * 9) + 38, n) * env_exp(n, 0.28)
    burst = lowpass(noise(n), 2200) * env_exp(n, 0.12) * 0.55
    out += boom + burst
    step = 0.075
    for k, name in enumerate(["G4", "C5", "E5", "G5", "C6"]):
        m = int(0.55 * SR)
        f = note_hz(name)
        note = (osc("square", f, m, duty=0.25) * 0.18 + osc("sine", f * 2, m) * 0.22) * env_adsr(m, 0.004, 0.08, 0.35, 0.35)
        mix_at(out, note, int((0.12 + k * step) * SR))
    bell_at = int((0.12 + 5 * step) * SR)
    for f, w in ((note_hz("C7"), 0.16), (note_hz("G6"), 0.12)):
        m = n - bell_at
        mix_at(out, osc("sine", f, m) * env_exp(m, 0.45) * w, bell_at)
    tail = bandpass(noise(n), 6000, 10000) * env_exp(n, 0.6) * 0.03
    return normalize(fade_edges(lowpass(out + tail, 7000)))


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


def music_box(name, seconds):
    """오르골 한 음: 사인 + 높은 배음(빨리 사라짐), 길게 울리며 사라진다."""
    m = int(seconds * SR)
    f = note_hz(name)
    ding = osc("sine", f, m) + 0.35 * osc("sine", f * 4.0, m) * env_exp(m, 0.05) + 0.15 * osc("sine", f * 2.0, m)
    return ding * env_exp(m, 0.6)


def village_theme():
    """NPC 마을 테마: 90 BPM, C장조, 오르골풍 16마디 루프. C-Am-F-G ×2, F-G-Em-Am, F-G-C-G (G로 루프 처음 C에 돌아온다)."""
    bpm = 90
    beat = 60 / bpm
    progression = [["C4", "E4", "G4"], ["A3", "C4", "E4"], ["F3", "A3", "C4"], ["G3", "B3", "D4"]] * 2 + \
                  [["F3", "A3", "C4"], ["G3", "B3", "D4"], ["E3", "G3", "B3"], ["A3", "C4", "E4"]] + \
                  [["F3", "A3", "C4"], ["G3", "B3", "D4"], ["C4", "E4", "G4"], ["G3", "B3", "D4"]]
    n = int(round(len(progression) * 4 * beat * SR))
    music = np.zeros(n)
    for bar, chord in enumerate(progression):
        bar_start = bar * 4 * beat
        mix_at(music, pad_chord(chord, 4 * beat) * 0.1, int(bar_start * SR))
        low = chord[0][:-1] + str(int(chord[0][-1]) - 1)
        for k in (0, 2):  # 베이스: 1·3박에 부드러운 사인
            mix_at(music, tone("sine", low, 1.6 * beat, 0.02, 0.4, 0.4, 0.3) * 0.22, int((bar_start + k * beat) * SR))
        up = [c[:-1] + str(int(c[-1]) + 1) for c in chord]
        # 오르골 선율: 8분음표 6개 + 마지막 두 박은 한 음 길게 (마디마다 모양을 살짝 바꿔 단조롭지 않게)
        shapes = [[up[0], up[1], up[2], up[1], up[0], up[1]], [up[2], up[1], up[0], up[1], up[2], up[1]]]
        pattern = shapes[bar % 2]
        for k, name in enumerate(pattern):
            mix_at(music, music_box(name, 1.4 * beat) * 0.13, int((bar_start + k * beat / 2) * SR))
        mix_at(music, music_box(up[0] if bar % 4 != 3 else up[2], 3 * beat) * 0.15, int((bar_start + 3 * beat) * SR))
    return normalize(lowpass(music, 6000), 0.55)


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


def midboss_theme():
    """중간보스(쌍둥이 슬라임 기사) 테마: 140 BPM, E단조, 8마디 루프. 보스 곡보다 가볍고 짧게, 불 기사(밝은 사각파)와
    물 기사(둥근 삼각파)가 한 마디씩 멜로디를 주고받는다. Em-C-D-B 진행 ×2 (B 화음으로 루프 처음 Em에 돌아온다)."""
    bpm = 140
    beat = 60 / bpm
    progression = [("E", ["E4", "G4", "B4"]), ("C", ["C4", "E4", "G4"]), ("D", ["D4", "F#4", "A4"]), ("B", ["B3", "D#4", "F#4"])] * 2
    bass_root = {"E": "E2", "C": "C2", "D": "D2", "B": "B1"}
    n = int(round(len(progression) * 4 * beat * SR))
    music = np.zeros(n)

    for bar, (root, chord) in enumerate(progression):
        bar_start = bar * 4 * beat
        mix_at(music, lowpass(pad_chord(chord, 4 * beat), 900) * 0.13, int(bar_start * SR))
        # 베이스: 8분음표 통통 (옥타브 왕복)
        for k in range(8):
            name = bass_root[root]
            if k % 2 == 1:
                name = name[:-1] + str(int(name[-1]) + 1)
            b = tone("saw", name, beat / 2 * 0.8, 0.004, 0.05, 0.5, 0.03)
            mix_at(music, lowpass(b, 1000) * 0.2, int((bar_start + k * beat / 2) * SR))
        # 드럼: 킥 1·2.5·3박, 스네어 2·4박, 하이햇 8분 (갑옷 걷는 느낌)
        for b_ in (0, 1.5, 2):
            mix_at(music, kick() * 0.55, int((bar_start + b_ * beat) * SR))
        for b_ in (1, 3):
            mix_at(music, snare() * 0.3, int((bar_start + b_ * beat) * SR))
        if bar == len(progression) - 1:  # 루프 끝 필
            for k in range(4):
                mix_at(music, snare() * (0.12 + 0.05 * k), int((bar_start + (3 + k / 4) * beat) * SR))
        for k in range(8):
            mix_at(music, hat(k % 4 == 2) * (0.07 if k % 2 else 0.045), int((bar_start + k * beat / 2) * SR))

    # 주고받는 멜로디: 짝수 마디 = 불 기사(사각파, 위), 홀수 마디 = 물 기사(삼각파, 아래)
    fire = [[("B5", 0.5), ("E6", 0.5), ("D6", 0.5), ("B5", 0.5), ("A5", 1), ("G5", 1)],
            [("F#5", 0.5), ("A5", 0.5), ("D6", 1), ("C6", 0.5), ("A5", 0.5), ("F#5", 1)],
            [("G5", 0.5), ("B5", 0.5), ("E6", 1), ("F#6", 0.5), ("E6", 0.5), ("B5", 1)],
            [("A5", 0.5), ("F#5", 0.5), ("D6", 1), ("C6", 0.5), ("B5", 0.5), ("A5", 1)]]
    water = [[("E5", 1), ("G5", 0.5), ("E5", 0.5), ("C5", 1.5), (None, 0.5)],
             [("D#5", 1), ("F#5", 0.5), ("A5", 0.5), ("B5", 1.5), (None, 0.5)],
             [("E5", 1), ("C5", 0.5), ("E5", 0.5), ("G5", 1.5), (None, 0.5)],
             [("F#5", 1), ("A5", 0.5), ("F#5", 0.5), ("D#5", 1.5), (None, 0.5)]]
    for bar in range(len(progression)):
        phrase = fire[bar // 2 % 4] if bar % 2 == 0 else water[bar // 2 % 4]
        pos = bar * 4 * beat
        for name, beats in phrase:
            if name:
                if bar % 2 == 0:
                    m = tone("square", name, beats * beat * 0.85, 0.004, 0.06, 0.55, 0.04, duty=0.25)
                    mix_at(music, lowpass(m, 4200) * 0.1, int(pos * SR))
                else:
                    m = tone("tri", name, beats * beat * 0.92, 0.02, 0.1, 0.7, 0.08, vib=0.01)
                    mix_at(music, m * 0.22, int(pos * SR))
            pos += beats * beat
    return normalize(music, 0.7)


def player_down():
    """플레이어가 쓰러짐: 아래로 미끄러지는 "피웅~" (사각파 + 비브라토) 뒤 작고 둔한 "쿵", 약 0.9초. 아프기보다 아쉬운 느낌."""
    n = int(0.9 * SR)
    out = np.zeros(n)
    m = int(0.62 * SR)
    t = np.arange(m) / SR
    glide = 660 * np.exp(-t * 2.6) + 110 + 14 * np.sin(2 * np.pi * 9 * t) * (1 - t / t[-1])
    whine = (osc("square", glide, m, duty=0.3) * 0.22 + osc("sine", glide, m) * 0.5) * env_adsr(m, 0.006, 0.12, 0.65, 0.22)
    mix_at(out, lowpass(whine, 2600), 0)
    k = int(0.3 * SR)
    tk = np.arange(k) / SR
    thud = osc("sine", 120 * np.exp(-tk * 16) + 48, k) * env_exp(k, 0.1)
    dust = lowpass(noise(k), 600) * env_adsr(k, 0.004, 0.05, 0.25, 0.1) * 0.3
    mix_at(out, thud + dust, int(0.58 * SR))
    return normalize(fade_edges(out))


def main():
    sfx = {
        "Cast_Fire": cast_fire, "Cast_Water": cast_water, "Cast_Wind": cast_wind, "Cast_Earth": cast_earth,
        "Cast_Neutral": cast_neutral,
        "Hit_Fire": hit_fire, "Hit_Water": hit_water, "Hit_Wind": hit_wind, "Hit_Earth": hit_earth,
        "Hit_Neutral": hit_neutral,
        "Jump": jump, "Dash": dash, "Hurt": hurt, "EnemyDie": enemy_die, "LevelUp": level_up,
        "RelicOpen": relic_open, "Switch_On": switch_on, "Switch_Tick": switch_tick,
        "GateOpen": gate_open, "Pickup": pickup, "ArenaLock": arena_lock, "StarShard": star_shard, "VampireHeart": vampire_heart,
        "RelicNew": relic_new, "Crumble": crumble, "Crumble_Back": crumble_back, "JellySquish": jelly_squish, "JellyDrip": jelly_drip, "MagnetWhoosh": magnet_whoosh, "Coin": coin, "CoinPile": coin_pile, "Crit": crit, "Combo": combo, "Notice": notice, "EnemyWindup": enemy_windup, "ShieldUp": shield_up, "ShieldBlock": shield_block, "TrapWarn": trap_warn, "FlameHiss": flame_hiss,
        "Blink": blink, "Fizzle": fizzle, "Step": step, "UiClick": ui_click, "BossNear": boss_near, "Heartbeat": heartbeat, "Summon": summon, "ChestLand": chest_land, "ChestCreak": chest_creak, "RelicSlot": relic_slot, "BookOpen": book_open, "RoomPass": room_pass, "TraitUp": trait_up, "TraitMilestone": trait_milestone, "NpcTalk": npc_talk, "PauseOpen": pause_open, "PauseClose": pause_close, "RunStart": run_start, "BossDefeat": boss_defeat, "PlayerDown": player_down,
        "Land": land, "SquishHit": squish_hit, "StoneCrack": stone_crack, "RockShatter": rock_shatter, "EliteWarn": elite_warn, "Enrage": enrage, "KnightLink": knight_link, "EnrageEnd": enrage_end, "SlimeSplit": slime_split, "BroodTick": brood_tick, "BroodMerge": brood_merge, "BroodGulp": brood_gulp, "BroodClear": brood_clear, "TentacleLash": tentacle_lash, "TentacleSlap": tentacle_slap, "TentacleGrab": tentacle_grab, "TentacleSnap": tentacle_snap, "KingFall": king_fall, "KingSlam": king_slam, "KingLand": king_land, "BowTwang": bow_twang, "ArrowWhiz": arrow_whiz, "ArrowThunk": arrow_thunk, "SlimeHop": slime_hop, "SlimePlop": slime_plop, "GoblinSwing": goblin_swing, "SpikeStab": spike_stab, "FlameSizzle": flame_sizzle, "PitFall": pit_fall, "PitReturn": pit_return, "GateDeflect": gate_deflect, "GateShatter": gate_shatter, "PlatformWake": platform_wake, "PlatformSleep": platform_sleep, "BlinkAppear": blink_appear, "BlinkVanish": blink_vanish, "PlatformStop": platform_stop, "MushroomBoing": mushroom_boing, "WindGust": wind_gust,
        "VillageFound": village_found, "ShopBell": shop_bell, "Reroll": reroll,
        "Jingle_Clear": jingle_clear, "Jingle_Defeat": jingle_defeat,
    }
    only = [a for a in sys.argv[1:] if not a.startswith("-")]  # 이름을 주면 그 소리만 만든다 (나머지 파일은 그대로)
    if only:
        sfx = {k: v for k, v in sfx.items() if k in only}
    # 소리마다 난수 씨앗을 따로 둬서 한 소리를 고쳐도 다른 소리가 바뀌지 않게 한다.
    soft = {"Cast_Neutral": 0.13, "Jump": 0.1, "Dash": 0.12, "Switch_Tick": 0.08, "Land": 0.11, "SquishHit": 0.12, "StarShard": 0.1, "VampireHeart": 0.09, "Crumble_Back": 0.08, "StoneCrack": 0.09, "JellySquish": 0.1, "JellyDrip": 0.06, "MagnetWhoosh": 0.07, "Coin": 0.08, "CoinPile": 0.09, "Crit": 0.08, "Combo": 0.09, "Notice": 0.07, "EnemyWindup": 0.06, "ShieldUp": 0.09, "ShieldBlock": 0.07, "TrapWarn": 0.08, "FlameHiss": 0.07, "ShopBell": 0.07, "Reroll": 0.08, "Blink": 0.09, "Fizzle": 0.07, "Step": 0.05, "UiClick": 0.06, "BossNear": 0.12, "Heartbeat": 0.1, "Summon": 0.08, "RoomPass": 0.06, "TraitUp": 0.08, "NpcTalk": 0.07, "ChestCreak": 0.09, "RelicSlot": 0.07, "BookOpen": 0.08, "EnrageEnd": 0.09, "BroodTick": 0.07, "TentacleLash": 0.07, "TentacleSlap": 0.08, "KingFall": 0.07, "BowTwang": 0.07, "ArrowWhiz": 0.06, "ArrowThunk": 0.07, "SlimeHop": 0.06, "SlimePlop": 0.07, "GoblinSwing": 0.07, "SpikeStab": 0.07, "FlameSizzle": 0.07, "PitFall": 0.08, "PitReturn": 0.08, "GateDeflect": 0.07, "PlatformWake": 0.08, "PlatformSleep": 0.07, "BlinkAppear": 0.06, "BlinkVanish": 0.07, "PlatformStop": 0.06, "MushroomBoing": 0.09, "WindGust": 0.07}  # 평타처럼 자주 나는 소리는 작고, tanh 압축 없이 부드럽게
    for name, fn in sfx.items():
        reseed(name)
        sound = fn()
        if name in soft:
            sound = normalize(sound, 1.0)
            sound *= soft[name] / np.sqrt(np.mean(sound ** 2))
        else:
            sound = loudness(sound)
        save_wav(fade_edges(sound), "SFX", f"{name}.wav")
    music = {"Menu": menu_theme, "Dungeon": dungeon_theme, "Dungeon2": dungeon2_theme, "Boss": boss_theme, "MidBoss": midboss_theme, "Village": village_theme}
    if only:
        music = {k: v for k, v in music.items() if k in only}  # 곡 이름도 지정할 수 있다 (예: Boss)
    for name, fn in music.items():
        reseed(name)
        save_wav(fn(), "Music", f"{name}.wav")
    print("done")


if __name__ == "__main__":
    main()
