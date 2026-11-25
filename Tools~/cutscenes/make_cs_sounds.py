"""컷신 소리 (8비트 느낌, 22050Hz 모노, ogg+mp3): 타자, 종이 쾅, 도장, 부들부들, 휙, 버튼, 폭발, 자명종, 제목 쾅.
사용: python make_cs_sounds.py <assets 폴더>   (ffmpeg 필요, sprite_definition.json/js 의 sounds 에도 넣는다)"""
import sys, wave, subprocess, json
from pathlib import Path
import numpy as np

SR = 22050; OUT = Path(sys.argv[1])
def t(d): return np.arange(int(SR * d)) / SR
def sq(f, d, duty=0.5): ph = (np.cumsum(np.broadcast_to(f, t(d).shape) / SR)) % 1; return np.where(ph < duty, 1.0, -1.0)
def tri(f, d): ph = (np.cumsum(np.broadcast_to(f, t(d).shape) / SR)) % 1; return 4 * np.abs(ph - 0.5) - 1
def noise(d, seed=1): return np.random.default_rng(seed).uniform(-1, 1, len(t(d)))
def env(n, a=0.003, r=None):
    e = np.ones(n); na = max(1, int(a * SR)); e[:na] = np.linspace(0, 1, na)
    if r: nr = min(n, int(r * SR)); e[-nr:] *= np.linspace(1, 0, nr) ** 2
    return e
def place(buf, x, at): i = int(at * SR); buf[i:i + len(x)] += x[:max(0, len(buf) - i)]
def lowpass(x, k): return np.convolve(x, np.ones(k) / k, mode='same')
NAMES = []
def save(name, buf, peak=0.7):
    buf = buf / max(1e-9, np.abs(buf).max()) * peak
    p = OUT / f'{name}.wav'
    with wave.open(str(p), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes((buf * 32767).astype('<i2').tobytes())
    for ext, args in (('ogg', ['-c:a', 'libvorbis', '-q:a', '4']), ('mp3', ['-c:a', 'libmp3lame', '-b:a', '64k'])):
        subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', str(p), '-ar', str(SR), '-ac', '1', *args, str(OUT / f'{name}.{ext}')], check=True)
    p.unlink(); NAMES.append(name); print(name, round(len(buf) / SR, 2), 's')

# 타자: 딸깍 여섯 번 (불규칙)
b = np.zeros(int(SR * 0.7))
for i, at in enumerate((0.0, 0.09, 0.2, 0.27, 0.4, 0.52)):
    d = 0.025; place(b, (noise(d, 10 + i) * 0.6 + sq(2400 - i * 90, d, 0.3) * 0.4) * env(len(t(d)), r=d), at)
save('sfx_cs_type', b, 0.5)
# 종이 쾅: 펄럭 + 낮은 쿵
d = 0.5; b = np.zeros(int(SR * d))
place(b, noise(0.08, 3) * env(len(t(0.08)), r=0.08) * 0.8, 0.0)
place(b, sq(np.linspace(130, 50, len(t(0.4))), 0.4) * env(len(t(0.4)), r=0.4) * 0.9 + lowpass(noise(0.4, 4), 6) * env(len(t(0.4)), r=0.3) * 0.6, 0.05)
save('sfx_cs_slam', b, 0.85)
# 도장: 묵직한 텅
d = 0.35; b = (sq(np.linspace(220, 70, len(t(d))), d, 0.4) * 0.7 + lowpass(noise(d, 5), 4) * 0.5) * env(len(t(d)), r=d); save('sfx_cs_stamp', b, 0.85)
# 부들부들: 낮게 떨리며 올라가는 소리
d = 1.1; tt = t(d); f = 70 + 90 * tt / d + 12 * np.sin(2 * np.pi * 18 * tt)
b = (sq(f, d, 0.3) * 0.6 + tri(f * 2.01, d) * 0.3) * env(len(tt), 0.05, 0.25) * (0.4 + 0.6 * tt / d); save('sfx_cs_rage', b, 0.6)
# 휙: 바람
d = 0.3; tt = t(d); b = lowpass(noise(d, 8), 3) * np.sin(np.pi * tt / d) ** 2 * 0.8 + tri(np.linspace(300, 1400, len(tt)), d) * 0.15 * np.sin(np.pi * tt / d); save('sfx_cs_whoosh', b, 0.6)
# 버튼: 딸깍(높은) + 띠링
b = np.zeros(int(SR * 0.4)); place(b, sq(1800, 0.03) * env(len(t(0.03)), r=0.03), 0.0); place(b, (sq(880, 0.3, 0.25) * 0.5 + sq(1320, 0.3, 0.25) * 0.3) * env(len(t(0.3)), r=0.25), 0.05)
save('sfx_cs_button', b, 0.7)
# 폭발: 쾅 + 우르르 (길게)
d = 2.4; tt = t(d); n = noise(d, 9)
b = lowpass(n, 3) * np.exp(-tt * 1.6) * 0.9 + lowpass(n, 20) * np.exp(-tt * 0.9) * 0.9 + sq(np.linspace(90, 30, len(tt)), d) * np.exp(-tt * 3) * 0.6
save('sfx_cs_boom', b * env(len(tt), 0.002, 0.6), 0.95)
# 자명종: 삐비빅 ×3
b = np.zeros(int(SR * 1.2))
for k in range(3):
    for j in range(3): place(b, sq(2093, 0.05, 0.5) * env(len(t(0.05)), r=0.01) * 0.6, k * 0.38 + j * 0.08)
save('sfx_cs_alarm', b, 0.6)
# 제목 쾅: 화음 + 쿵
d = 1.2; tt = t(d); b = (sq(110, d) * 0.4 + sq(165, d, 0.3) * 0.3 + tri(220, d) * 0.3) * np.exp(-tt * 2.5) + lowpass(noise(d, 12), 8) * np.exp(-tt * 8) * 0.6
save('sfx_cs_title', b, 0.8)

jp = OUT / 'sprite_definition.json'; dd = json.loads(jp.read_text(encoding='utf-8'))
for n_ in NAMES: dd['sounds'][n_] = {'loop': False, 'files': {'mp3': n_ + '.mp3', 'ogg': n_ + '.ogg'}}
js = json.dumps(dd, ensure_ascii=False, indent=2); jp.write_text(js, encoding='utf-8'); (OUT / 'sprite_definition.js').write_text('window.SPRITE_DEF = ' + js + ';\n', encoding='utf-8')
print('sounds', len(dd['sounds']))
