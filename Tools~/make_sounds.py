"""새 장애물 소리: 장애물마다 다른 패링 타이밍 소리와 상사의 눈·사라지는 칸·에스컬레이터 소리 (8비트 느낌, 22050Hz 모노).
박(막을 순간)의 위치를 적어 둔다: CONFIG.cues의 앞당김 = 박 + 0.1초."""
import sys, wave, subprocess, numpy as np
SR = 22050
out = sys.argv[1]
def t(d): return np.arange(int(SR * d)) / SR
def sq(f, d, duty=0.5): ph = (np.cumsum(np.broadcast_to(f, t(d).shape) / SR)) % 1; return np.where(ph < duty, 1.0, -1.0)
def tri(f, d): ph = (np.cumsum(np.broadcast_to(f, t(d).shape) / SR)) % 1; return 4 * np.abs(ph - 0.5) - 1
def noise(d, seed=1): return np.random.default_rng(seed).uniform(-1, 1, len(t(d)))
def env(n, a=0.003, r=None):
    e = np.ones(n); na = max(1, int(a * SR)); e[:na] = np.linspace(0, 1, na)
    if r: nr = min(n, int(r * SR)); e[-nr:] *= np.linspace(1, 0, nr) ** 2
    return e
def place(buf, x, at): i = int(at * SR); buf[i:i + len(x)] += x[:max(0, len(buf) - i)]
def save(name, buf, peak=0.6):
    buf = buf / max(1e-9, np.abs(buf).max()) * peak
    p = f"{out}/{name}.wav"
    with wave.open(p, 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes((buf * 32767).astype('<i2').tobytes())
    for ext, args in (('ogg', ['-c:a', 'libvorbis', '-q:a', '4']), ('mp3', ['-c:a', 'libmp3lame', '-b:a', '64k'])):
        subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', p, '-ar', str(SR), '-ac', '1', *args, f"{out}/{name}.{ext}"], check=True)
    print(name, round(len(buf) / SR, 2), 's')

# 스테이플러: 철컥(0.0) … 철컥!(박 0.30) 무거운 쇠 소리 — 앞당김 0.40
b = np.zeros(int(SR * 0.45))
def clack(heavy):
    d = 0.09 if heavy else 0.05
    n = noise(d, 3) * env(len(t(d)), r=d) ; s = sq(np.linspace(1400 if heavy else 1800, 600, len(t(d))), d, 0.3) * env(len(t(d)), r=d)
    lo = sq(np.linspace(180, 90, len(t(d))), d) * env(len(t(d)), r=d) * (0.8 if heavy else 0)
    return 0.5 * n + 0.45 * s + lo
place(b, clack(False) * 0.6, 0.0); place(b, clack(True), 0.30); save('sfx_cue_stapler', b, 0.85)

# 포스트잇: 팔랑(0.0) 팔랑(0.15) 짝!(박 0.30) 가볍고 높은 종이 튕김 — 앞당김 0.40
b = np.zeros(int(SR * 0.42))
def flap(f, d=0.04): return (noise(d, 7) * 0.5 + tri(np.linspace(f, f * 1.5, len(t(d))), d) * 0.6) * env(len(t(d)), r=d)
place(b, flap(900) * 0.45, 0.0); place(b, flap(1100) * 0.55, 0.15); place(b, flap(1500, 0.07) * 1.0, 0.30); save('sfx_cue_sticky', b, 0.85)

# 종이비행기 편대: 휘이잉 올라가는 바람 소리, 끝의 휙(박 0.25) — 앞당김 0.35
d = 0.36; tt = t(d); f = np.where(tt < 0.25, 500 + 1500 * (tt / 0.25) ** 2, 2000 - 900 * (tt - 0.25) / 0.11)
w = noise(d, 11) * 0.35 + tri(f, d) * 0.4 * (0.5 + 0.5 * np.sin(2 * np.pi * 28 * tt))
w *= np.where(tt < 0.25, (tt / 0.25) ** 1.5, np.maximum(0, 1 - (tt - 0.25) / 0.11)); save('sfx_cue_airplane', w, 0.55)

# 상사의 눈 예고: 낮게 웅웅 차오르는 소리 1.2초 (레이저 충전)
d = 1.2; tt = t(d); f = 70 + 110 * (tt / d) ** 2
w = (sq(f, d, 0.3) * 0.5 + sq(f * 1.5, d, 0.5) * 0.25) * (0.5 + 0.5 * np.sin(2 * np.pi * 6 * tt)) * np.minimum(1, tt / 0.2) * env(len(tt), r=0.12)
save('sfx_gaze_warn', w, 0.45)

# 상사의 눈 레이저: 0.6초 만에 위에서 아래로 쓸어내리는 날카로운 빔 (높은 음이 빠르게 떨어지고 끝에 쾅)
d = 0.75; tt = t(d); f = 2400 * np.exp(-tt * 3.2) + 120
w = sq(f, d, 0.25) * 0.4 + sq(f * 0.503, d) * 0.3 + noise(d, 5) * 0.2 * (np.sin(2 * np.pi * 60 * tt) > 0)
w *= np.minimum(1, tt / 0.01) * env(len(tt), r=0.25)
th = noise(0.15, 6) * env(len(t(0.15)), r=0.15) * 0.8 + sq(np.linspace(90, 50, len(t(0.15))), 0.15) * env(len(t(0.15)), r=0.15) * 0.7
place(w, th, 0.58); save('sfx_gaze_laser', w, 0.55)

# 사라지는 칸: 뾰로롱 내려가는 블립 (칸이 꺼진다)
d = 0.28; tt = t(d); w = sq(np.where(tt < 0.14, 1200, 600) * (1 - 0.6 * tt / d), d, 0.25) * env(len(tt), r=0.1); save('sfx_vanish', w, 0.32)

# 에스컬레이터 밀림: 덜컹 + 낮게 미끄러지는 모터 소리
d = 0.3; tt = t(d); w = sq(np.linspace(110, 70, len(tt)), d) * 0.6 * env(len(tt), r=0.15) + noise(d, 9) * 0.4 * env(len(tt), r=0.25) * (tt < 0.06)
save('sfx_escalator', w, 0.5)

# 완벽한 막기(판정 창 한가운데): 날카로운 '창!' 쇳소리 + 밝은 울림(두 음이 살짝 어긋나 반짝임) + 낮은 쿵 + 짧은 울림 꼬리. 더 짜릿하게 (2026-11-25)
d = 0.42; tt = t(d)
crack = noise(d, 21) * np.exp(-tt * 90) * 0.9                                   # 맞부딪는 순간
ring = (sq(1760, d, 0.25) * 0.35 + sq(1760 * 1.5 * 1.004, d, 0.3) * 0.25 + tri(3520 * 1.01, d) * 0.2) * np.exp(-tt * 9)
thump = sq(np.maximum(55, 160 - 900 * tt), d) * np.exp(-tt * 28) * 0.7          # 낮게 쿵
sweep = tri(2600 + 2200 * np.exp(-tt * 20), d) * np.exp(-tt * 25) * 0.3          # 위로 튀는 반짝
w = crack + ring + thump + sweep
echo = np.zeros_like(w); k = int(0.07 * SR); echo[k:] = w[:-k] * 0.35; w = w + echo
w = np.tanh(w * 3.0) * np.exp(-tt * 2.5); w *= np.minimum(1, tt / 0.001); save('sfx_parry_perfect', w, 0.72)   # 부드럽게 눌러 펀치감

# 굴러오는 의자: 바퀴가 덜그럭 구르는 소리, 끝의 '쾅'(박 0.30) — 앞당김 0.40
d = 0.45; tt = t(d); b = noise(d, 31) * 0.25 * (np.sin(2 * np.pi * 22 * tt) > 0.3) * np.minimum(1, tt / 0.05)
b += sq(np.full(len(tt), 140.0), d, 0.4) * 0.12 * (np.sin(2 * np.pi * 11 * tt) > 0)
hit = noise(0.12, 32) * env(len(t(0.12)), r=0.12) * 0.8 + sq(np.linspace(220, 90, len(t(0.12))), 0.12) * env(len(t(0.12)), r=0.12) * 0.8
b[int(0.30 * SR):] *= 0.3; place(b, hit, 0.30); save('sfx_cue_chair', b, 0.7)
# 튀어 오르는 의자: 뾰옹 (위로 튀는 소리, 막지 말라는 신호)
d = 0.35; tt = t(d); w = tri(250 + 900 * (tt / d) ** 0.6, d) * env(len(tt), r=0.15) * 0.8 + sq(250 + 900 * (tt / d) ** 0.6, d, 0.2) * env(len(tt), r=0.15) * 0.2
save('sfx_chair_hop', w, 0.55)
# 편지 한 장: 종이가 휙 스치는 소리 끝의 '착'(박 0.25) — 앞당김 0.35
d = 0.33; tt = t(d); w = noise(d, 41) * 0.35 * np.minimum(1, tt / 0.2) * (tt < 0.25)
snap = noise(0.06, 42) * env(len(t(0.06)), r=0.06) * 0.9 + tri(np.full(len(t(0.06)), 2600.0), 0.06) * env(len(t(0.06)), r=0.06) * 0.5
place(w, snap, 0.25); save('sfx_cue_envelope', w, 0.6)
