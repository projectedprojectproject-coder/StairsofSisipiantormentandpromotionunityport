"""플레이어·경쟁 사원 스프라이트에 계단 오르기·내려가기 프레임을 더한다 (기존 프레임의 픽셀을 옮겨 만든다).
player.png: 9 → 17열 (9 지나가기: 몸 1px 위, 10 왼무릎 들기: 2번의 다리를 좌우로 뒤집음, 11 웅크려 내려가기, 12 내려 딛기,
            13·14 왼쪽 막기 성공(손이 하얗게 번쩍 → 몸이 1px 밀림), 15·16 오른쪽 막기 성공)
rival.png: 10 → 16열 (10~13 계단 달려 오르기: 발 들기/지나가기 번갈아, 14~15 밀려 내려가기)
사용: python make_frames.py <assets 폴더>  (이미 늘어난 시트면 그대로 둔다)"""
import sys, json
import numpy as np
from PIL import Image
A = sys.argv[1]
FW, FH = 32, 48

def body_shift(f, dy, waist):
    """허리(waist) 위 몸통을 dy만큼 옮긴다. 위로(-)면 다리가 늘고, 아래로(+)면 허벅지 줄을 빼서 다리를 줄인다(웅크림)."""
    g = np.zeros_like(f)
    if dy < 0:
        g[:waist + dy] = f[-dy:waist]; g[waist + dy:] = f[waist + dy:]
        g[waist + dy:waist] = f[waist - 1]          # 허리 줄을 늘려 채운다
    else:
        g[dy:waist + dy] = f[:waist]
        legs = f[waist:]; keep = [i for i in range(len(legs)) if i not in set(range(2, 2 + dy * 2, 2))]
        legs = legs[keep]; g[FH - len(legs):] = legs
    return g

def mirror_legs(f, waist):
    g = f.copy(); g[waist:] = f[waist:, ::-1]; return g

def raise_foot(f, side, dy, from_row):
    """한쪽(side<0 왼쪽, >0 오른쪽) 다리의 from_row 아래를 dy만큼 들어 올린다 (무릎을 굽혀 디딜 칸에 발을 올리는 모양)."""
    g = f.copy(); xs = slice(16, FW) if side > 0 else slice(0, 16)
    part = f[from_row:, xs].copy(); g[from_row:, xs] = 0
    g[from_row - dy:FH - dy, xs] = np.where(part[..., 3:4] > 0, part, g[from_row - dy:FH - dy, xs])
    return g

def sheet(path):
    return np.array(Image.open(path).convert('RGBA'))

# ---- 플레이어
pp = f"{A}/player.png"; P = sheet(pp)
if P.shape[1] // FW == 9:
    rows = P.shape[0] // FH; out = np.zeros((P.shape[0], FW * 13, 4), np.uint8); out[:, :FW * 9] = P
    for r in range(rows):
        fr = lambda c: P[r * FH:(r + 1) * FH, c * FW:(c + 1) * FW]
        new = [body_shift(fr(0), -1, 31), mirror_legs(fr(2), 31), body_shift(fr(0), 2, 31), body_shift(mirror_legs(fr(2), 31), 2, 31)]
        for k, f in enumerate(new): out[r * FH:(r + 1) * FH, (9 + k) * FW:(10 + k) * FW] = f
    Image.fromarray(out).save(pp); print('player.png ->', out.shape[1] // FW, 'cols')
def flash_hand(f, side):
    """막는 손(그림 끝 6칸)의 밝은 픽셀을 하얗게, 팔은 조금 밝게 (막기 성공 순간)."""
    g = f.copy(); xs = slice(0, 6) if side < 0 else slice(26, 32); arm = slice(0, 10) if side < 0 else slice(22, 32)
    rgb = g[10:24, arm, :3].astype(int); lum = rgb.sum(-1); m = (g[10:24, arm, 3] > 0) & (lum > 120)
    rgb[m] = np.minimum(255, rgb[m] + 70); g[10:24, arm, :3] = rgb
    h = g[10:24, xs]; m2 = (h[..., 3] > 0) & (h[..., :3].astype(int).sum(-1) > 150); h[m2, :3] = 255
    return g
def recoil(f, side):
    g = np.zeros_like(f)
    if side < 0: g[:, 1:] = f[:, :-1]
    else: g[:, :-1] = f[:, 1:]
    return g
pp = f"{A}/player.png"; P = sheet(pp)
if P.shape[1] // FW == 13:
    rows = P.shape[0] // FH; out = np.zeros((P.shape[0], FW * 17, 4), np.uint8); out[:, :FW * 13] = P
    for r in range(rows):
        fr = lambda c: P[r * FH:(r + 1) * FH, c * FW:(c + 1) * FW]
        new = [flash_hand(fr(3), -1), recoil(fr(3), -1), flash_hand(fr(4), +1), recoil(fr(4), +1)]
        for k, f in enumerate(new): out[r * FH:(r + 1) * FH, (13 + k) * FW:(14 + k) * FW] = f
    Image.fromarray(out).save(pp); print('player.png ->', out.shape[1] // FW, 'cols (막기 성공)')
# ---- 경쟁 사원
rp = f"{A}/rival.png"; R = sheet(rp)
if R.shape[1] // FW == 10:
    out = np.zeros((FH, FW * 16, 4), np.uint8); out[:, :FW * 10] = R
    fr = lambda c: R[:, c * FW:(c + 1) * FW]
    waist = 34
    new = [raise_foot(fr(0), +1, 4, 38), body_shift(fr(1), -1, waist), raise_foot(fr(2), -1, 4, 38), body_shift(fr(3), -1, waist),
           body_shift(fr(1), 2, waist), body_shift(fr(0), 2, waist)]
    for k, f in enumerate(new): out[:, (10 + k) * FW:(11 + k) * FW] = f
    Image.fromarray(out).save(rp); print('rival.png ->', out.shape[1] // FW, 'cols')

# ---- 정의 (json·js)
def upd(d):
    pa = d['sprites']['player']['animations']
    pa['climb_r'] = {'frames': [2, 9], 'fps': 14}      # 오른무릎 들고 → 지나가기 (한 칸 0.14초)
    pa['climb_l'] = {'frames': [10, 9], 'fps': 14}     # 왼무릎 들고 → 지나가기 (칸마다 번갈아)
    pa['down'] = {'frames': [11, 12], 'fps': 8}        # 밀려 내려가기·떨어지기
    pa['parry_hit_left'] = {'frames': [13, 14, 3], 'fps': 18}    # 막기 성공: 번쩍 → 밀림 → 막는 자세
    pa['parry_hit_right'] = {'frames': [15, 16, 4], 'fps': 18}
    ra = d['sprites']['rival']['animations']
    ra['climb'] = {'frames': [10, 11, 12, 13], 'fps': 10}
    ra['down'] = {'frames': [14, 15], 'fps': 8}
jp = f"{A}/sprite_definition.json"; d = json.load(open(jp, encoding='utf-8')); upd(d)
open(jp, 'w', encoding='utf-8').write(json.dumps(d, ensure_ascii=False, indent=2))
js = f"{A}/sprite_definition.js"; t = open(js, encoding='utf-8').read(); pre = t[:t.index('{')]; d2 = json.loads(t[t.index('{'):].rstrip().rstrip(';')); upd(d2)
open(js, 'w', encoding='utf-8').write(pre + json.dumps(d2, ensure_ascii=False) + ';\n')
print('definitions updated')
