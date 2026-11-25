"""16비트 느낌으로 다듬기: 스프라이트 시트의 프레임마다 (빛은 왼쪽 위에서)
- 덩어리 명암: 같은 색 가로줄마다 밝은 띠·기본·그림자·깊은 그림자 (원통처럼), 세로로 긴 덩어리는 아래쪽 그늘. 작은 것(3픽셀 미만)은 그대로
- 셀아웃: 밝은 쪽 외곽선을 검정 대신 옆 색의 어두운 색으로
- 머리카락 윤기
사용: python upgrade16.py <원본 assets> <출력 assets> [sprite id ...]  (원본은 고치지 않는다 — 두 번 걸리지 않게)"""
import sys, json
import numpy as np
from PIL import Image
src, dst = sys.argv[1], sys.argv[2]
ids = sys.argv[3:] or ['player', 'rival', 'staff', 'commuter', 'projectiles', 'sticky', 'props', 'stairs', 'obstacles']
d = json.load(open(f"{src}/sprite_definition.json", encoding='utf-8'))

SKINS = {(0xd9, 0xb5, 0x96), (0xb8, 0x93, 0x7a)}
def tone(c, k):   # k > 1: 흰색 쪽으로 밝게, k < 1: 어둡게
    c = c.astype(float)
    return c + (255 - c) * (k - 1) if k > 1 else c * k
def cell16(f, per_color=True, amp=1.0):
    """덩어리 명암 (빛은 왼쪽 위): 같은 색이 가로로 이어진 줄(소매·다리·몸통·얼굴)마다 왼쪽→오른쪽으로
    밝은 띠 · 기본 · 그림자 · 깊은 그림자, 세로로 긴 덩어리는 아래쪽을 조금 어둡게. 외곽선을 더 긋지 않는다."""
    a = f.astype(int); op = a[..., 3] > 0; lum = a[..., :3].sum(-1)
    dark = op & (lum < 90); fill = op & ~dark
    H, W = op.shape; out = f.copy(); rgb = a[..., :3].astype(float)
    # 사람은 같은 색 덩어리(소매·다리·몸통)마다, 물건은 전체 모양 하나로 (서류 글줄 같은 작은 무늬에 줄무늬가 생기지 않게)
    key = [[(tuple(a[y, x, :3]) if per_color else 1) if fill[y, x] else None for x in range(W)] for y in range(H)]
    kx = np.ones((H, W)); ky = np.ones((H, W))
    horiz = amp > 0.5                                    # 계단 칸은 가로 그러데이션 없이 (이어 붙인 칸 사이가 이음매처럼 보이지 않게)
    for y in range(H if horiz else 0):                   # 가로 줄: 원통처럼 (넓은 덩어리 3단, 다리 같은 중간 덩어리 2단, 6픽셀 미만은 그대로)
        x = 0
        while x < W:
            if key[y][x] is None: x += 1; continue
            x0 = x
            while x < W and key[y][x] == key[y][x0]: x += 1
            n = x - x0; c = key[y][x0]
            if c in SKINS:                               # 얼굴·손: 왼쪽 한 칸만 밝게 (원래 얼굴 그늘은 그대로)
                kx[y, x0] = 1.08; continue
            if n < 6: continue
            for xx in range(x0, x):
                t = (xx - x0 + 0.5) / n
                if not per_color or n < 10: kx[y, xx] = 1.0 if t < 0.6 else 0.86
                else: kx[y, xx] = 1.12 if t < 0.25 else 1.0 if t < 0.7 else 0.82
    for x in range(W):                                   # 세로 줄: 위는 조금 밝게, 아래쪽 그늘
        y = 0
        while y < H:
            if key[y][x] is None: y += 1; continue
            y0 = y
            while y < H and key[y][x] == key[y0][x]: y += 1
            n = y - y0
            if n < 5 or key[y0][x] in SKINS: continue
            for yy in range(y0, y):
                tv = (yy - y0 + 0.5) / n
                ky[yy, x] = 1.06 if tv < 0.15 else 1.0 if tv < 0.72 else 0.9
    for y in range(H):
        for x in range(W):
            if not fill[y, x]: continue
            k = kx[y, x] * ky[y, x]
            l = rgb[y, x].mean()
            if l > 200 or l < 70: k = min(max(k, 0.9), 1.06)   # 아주 밝은 색(크림 셔츠)·아주 어두운 색(남색)은 얕게
            rgb[y, x] = tone(rgb[y, x], k)
    # 셀아웃: 왼쪽 위(빛 쪽) 외곽선만 옆 색의 어두운 색으로, 그늘 쪽 외곽선은 그대로
    def nb(m, dy, dx):
        r = np.zeros_like(m)
        ys, ye = max(0, -dy), H - max(0, dy); xs, xe = max(0, -dx), W - max(0, dx)
        r[ys:ye, xs:xe] = m[ys + dy:ye + dy, xs + dx:xe + dx]; return r
    empty = ~op; dn, rt = nb(fill, 1, 0), nb(fill, 0, 1)
    sel = dark & (nb(empty, -1, 0) | nb(empty, 0, -1)) & (dn | rt)
    nbcol = np.where(dn[..., None], np.roll(rgb, -1, axis=0), np.roll(rgb, -1, axis=1))
    keep = nbcol.mean(-1) > 200                         # 아주 밝은 것(종이·종이비행기)은 검은 외곽선을 그대로 두어 또렷하게
    sel = sel & ~keep
    rgb[sel] = np.maximum(nbcol[sel] * 0.42, rgb[sel])
    # 머리카락 윤기: 맨 윗줄 왼쪽 세 칸만
    du, dd, dl, dr = nb(dark, -1, 0), nb(dark, 1, 0), nb(dark, 0, -1), nb(dark, 0, 1)
    inner = dark & du & dd & dl & dr & nb(empty, -2, 0)
    ys, xs = np.nonzero(inner)
    if len(ys):
        top = ys.min()
        for x in sorted(xs[ys == top])[:3]: rgb[top, x] = [0x3b, 0x41, 0x50]
    out[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    return out

PEOPLE = {'player', 'rival', 'staff', 'commuter'}
for sid in ids:
    sp = d['sprites'].get(sid)
    if not sp: print('skip', sid); continue
    img = np.array(Image.open(f"{src}/{sp['file']}").convert('RGBA')); fw, fh = sp['frame_w'], sp['frame_h']
    for y in range(0, img.shape[0] - fh + 1, fh):
        for x in range(0, img.shape[1] - fw + 1, fw):
            img[y:y + fh, x:x + fw] = cell16(img[y:y + fh, x:x + fw], sid in PEOPLE, 0.4 if sid == 'stairs' else 1.0)   # 계단 칸: 가로 그러데이션 없이 위·아래만
    Image.fromarray(img).save(f"{dst}/{sp['file']}"); print('16bit', sid, img.shape[1] // fw, 'x', img.shape[0] // fh)
