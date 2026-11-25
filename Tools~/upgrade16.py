"""16비트 느낌으로 다듬기: 스프라이트 시트의 프레임마다 (빛은 왼쪽 위에서)
- 밝은 쪽 가장자리 하이라이트, 어두운 쪽 가장자리 그림자 (두께 2픽셀 이상인 곳만, 눈·넥타이 같은 작은 것은 그대로)
- 셀아웃: 밝은 쪽 외곽선을 검정 대신 옆 색의 어두운 색으로
- 머리카락 윤기
사용: python upgrade16.py <원본 assets> <출력 assets> [sprite id ...]  (원본은 고치지 않는다 — 두 번 걸리지 않게)"""
import sys, json
import numpy as np
from PIL import Image
src, dst = sys.argv[1], sys.argv[2]
ids = sys.argv[3:] or ['player', 'rival', 'staff', 'commuter', 'projectiles', 'sticky', 'props', 'stairs']
d = json.load(open(f"{src}/sprite_definition.json", encoding='utf-8'))

def cell16(f):
    a = f.astype(int); op = a[..., 3] > 0; lum = a[..., :3].sum(-1)
    dark = op & (lum < 90); fill = op & ~dark
    H, W = op.shape; out = f.copy()
    def nb(m, dy, dx):
        r = np.zeros_like(m)
        ys, ye = max(0, -dy), H - max(0, dy); xs, xe = max(0, -dx), W - max(0, dx)
        r[ys:ye, xs:xe] = m[ys + dy:ye + dy, xs + dx:xe + dx]; return r
    up, lf, dn, rt = nb(fill, -1, 0), nb(fill, 0, -1), nb(fill, 1, 0), nb(fill, 0, 1)
    lit = fill & (~up | ~lf) & dn & rt                     # 왼쪽 위 가장자리 (안쪽은 두께가 있다)
    shd = fill & (~dn | ~rt) & up & lf                     # 오른쪽 아래 가장자리
    rgb = a[..., :3].astype(float)
    rgb[lit] = rgb[lit] + (255 - rgb[lit]) * 0.22
    rgb[shd] = rgb[shd] * 0.78
    shd2 = fill & ~lit & ~shd & (nb(shd, 0, -1) | nb(shd, -1, 0)) & up & lf   # 그림자 한 칸 안쪽: 중간 톤 (두 단계 명암)
    rgb[shd2] = rgb[shd2] * 0.9
    # 셀아웃: 왼쪽 위가 비어 있고 오른쪽/아래가 칠해진 외곽선은 그 색의 어두운 색으로
    empty = ~op; eu, el = nb(empty, -1, 0), nb(empty, 0, -1)
    sel = dark & (eu | el) & (dn | rt)
    nbcol = np.where(dn[..., None], np.roll(a[..., :3], -1, axis=0), np.roll(a[..., :3], -1, axis=1)).astype(float)
    hair = dark & (lum < 90)
    rgb[sel] = np.maximum(nbcol[sel] * 0.42, rgb[sel])
    # 머리카락 윤기: 어두운 덩어리 안쪽에서 바로 위 두 칸 안에 빈칸이 있는 곳
    du, dd, dl, dr = nb(dark, -1, 0), nb(dark, 1, 0), nb(dark, 0, -1), nb(dark, 0, 1)
    inner = dark & du & dd & dl & dr & nb(empty, -2, 0)
    ys, xs = np.nonzero(inner)
    if len(ys):   # 머리 전체에 줄이 생기지 않게: 맨 윗줄의 왼쪽 세 칸만 (짧은 윤기)
        top = ys.min(); row = sorted(xs[ys == top])[:3]
        for x in row: rgb[top, x] = [0x3b, 0x41, 0x50]
    out[..., :3] = np.clip(rgb, 0, 255).astype(np.uint8)
    return out

for sid in ids:
    sp = d['sprites'].get(sid)
    if not sp: print('skip', sid); continue
    img = np.array(Image.open(f"{src}/{sp['file']}").convert('RGBA')); fw, fh = sp['frame_w'], sp['frame_h']
    for y in range(0, img.shape[0] - fh + 1, fh):
        for x in range(0, img.shape[1] - fw + 1, fw):
            img[y:y + fh, x:x + fw] = cell16(img[y:y + fh, x:x + fw])
    Image.fromarray(img).save(f"{dst}/{sp['file']}"); print('16bit', sid, img.shape[1] // fw, 'x', img.shape[0] // fh)
