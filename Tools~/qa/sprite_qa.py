"""스프라이트 QA: sprite_definition.json의 모든 시트와 배경을 픽셀 단위로 검사한다.
- 프레임 가장자리 잘림: 프레임 테두리에 닿은 불투명 픽셀이 외곽선(어두운 색)으로 닫혀 있지 않으면 잘려 보인다 (edge_close.py로 닫는다)
- 반투명 픽셀: 픽셀 아트는 알파가 0 또는 255 (반투명 점은 확대하면 얼룩)
- 떨어진 점: 주변 8칸이 모두 투명한 외톨이 불투명 픽셀
- 색 수: 시트의 한 행(옷 색 바꿈 하나)의 색이 너무 많으면 (그라데이션·안티에일리어싱) 16비트 팔레트 느낌이 깨진다
- 키 일관성: 같은 인물의 서 있기·걷기·오르기 프레임 키(위~발)가 크게 다르면 동작이 바뀔 때 몸이 줄었다 늘어난다
- 쓰지 않는 칸에 그림이 있거나, 쓰는 칸이 비어 있는 것
- 배경: 정해진 크기와 같은지, 투명 픽셀이 없는지
사용: python sprite_qa.py <assets 폴더>"""
import sys, json
import numpy as np
from PIL import Image
import os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from edge_close import is_closed

A = sys.argv[1]
d = json.load(open(f"{A}/sprite_definition.json", encoding='utf-8'))
issues, report = [], []
K = d.get('pixel_scale', 2) / 2   # 밀도 배율 (4배 밀도면 2): 픽셀 기준 한계를 늘린다
MAX_COLORS = 128                       # 시트 한 행의 색 상한 (16비트). 32비트(4배 밀도)는 4배까지 (부드러운 명암 단계)
PEOPLE = {'player', 'rival', 'staff', 'commuter'}
HEIGHT_ANIMS = ('idle', 'walk', 'climb', 'climb_r', 'climb_l')
ROWS_FOR = {'obstacles': {'roll': [0], 'fly': [1], 'snap': [2], 'flap': [3]}}
GROUND_OBJ = {'props', 'cs_hero', 'cs_hand'}   # 바닥에 놓인 소품, 컷신 사람(발)·손(소매는 화면 밖에서 들어온다)
EDGE_OK = {'stairs', 'hud_icons'}      # 계단 타일은 이웃 타일과 이어지도록 가장자리까지 그린다


def strays(m):
    p = np.pad(m, 1)
    n = sum(np.roll(np.roll(p, dy, 0), dx, 1) for dy in (-1, 0, 1) for dx in (-1, 0, 1) if dy or dx)[1:-1, 1:-1]
    return int((m & (n == 0)).sum())


for sid, sp in d['sprites'].items():
    img = np.array(Image.open(f"{A}/{sp['file']}").convert('RGBA'))
    fw, fh = sp['frame_w'], sp['frame_h']
    cols, rows = img.shape[1] // fw, img.shape[0] // fh
    if img.shape[1] % fw or img.shape[0] % fh: issues.append(f"{sid}: 시트 크기 {img.shape[1]}x{img.shape[0]}가 프레임 {fw}x{fh}로 나누어떨어지지 않음")
    a = img[..., 3]
    semi = int(((a > 0) & (a < 255)).sum())
    if semi: issues.append(f"{sid}: 반투명 픽셀 {semi}개")
    ncol = 0
    for r in range(rows):   # 색 수는 행(옷 색 바꿈)마다 센다
        band = img[r * fh:(r + 1) * fh]; op = band[band[..., 3] > 0][:, :3]
        nr = len(np.unique(op.view([('', op.dtype)] * 3))) if len(op) else 0; ncol = max(ncol, nr)
        if nr > MAX_COLORS * K * K: issues.append(f"{sid} 행{r}: 색 {nr}개 (상한 {int(MAX_COLORS * K * K)})")
    used = {}
    for an, anim in sp.get('animations', {}).items():
        for r in (ROWS_FOR.get(sid, {}).get(an) or range(rows)):
            for c in anim['frames']: used.setdefault((r, c), []).append(an)
    heights = {}
    for r in range(rows):
        for c in range(cols):
            f = img[r * fh:(r + 1) * fh, c * fw:(c + 1) * fw]; m = f[..., 3] > 0
            if (r, c) not in used:
                continue
            if not m.any(): issues.append(f"{sid} 행{r} 칸{c} ({'/'.join(used[(r, c)])}): 쓰는 칸인데 비어 있음"); continue
            if sid not in EDGE_OK:   # 테두리에 닿은 픽셀은 외곽선(어두운 색)으로 닫혀 있어야 한다. 아니면 잘려 보인다
                h_, w_ = m.shape; open_ = {'위': 0, '왼쪽': 0, '오른쪽': 0, '아래': 0}
                for side, pts in (('위', [(0, x, 1, x) for x in range(w_)]), ('왼쪽', [(y, 0, y, 1) for y in range(h_)]),
                                  ('오른쪽', [(y, w_ - 1, y, w_ - 2) for y in range(h_)]), ('아래', [(h_ - 1, x, h_ - 2, x) for x in range(w_)])):
                    if side == '아래' and (sid in PEOPLE or sid in GROUND_OBJ): continue   # 발·밑면은 바닥 기준선
                    open_[side] = sum(1 for y, x, iy, ix in pts if f[y, x, 3] and not is_closed(f[y, x], f[iy, ix]))
                bad = {k: v for k, v in open_.items() if v}
                if bad: issues.append(f"{sid} 행{r} 칸{c} ({'/'.join(used[(r, c)])}): 테두리에서 잘림(외곽선 없음) " + ', '.join(f'{k} {v}' for k, v in bad.items()))
            st = strays(m) if (sid in PEOPLE or sid == 'obstacles') else 0   # 김·반짝이 같은 떨어진 점은 물건에서는 정상
            if st > 1 * K: issues.append(f"{sid} 행{r} 칸{c}: 떨어진 점 {st}개")
            if sid in PEOPLE:
                ys = np.nonzero(m)[0]
                for an in used[(r, c)]:
                    if an in HEIGHT_ANIMS: heights.setdefault(r, []).append((an, c, int(ys.max() - ys.min() + 1)))
    for r, hs in heights.items():
        lo, hi = min(h for _, _, h in hs), max(h for _, _, h in hs)
        if hi - lo > 5 * K: issues.append(f"{sid} 행{r}: 서 있기·걷기·오르기 키가 {lo}~{hi}px로 다름 " + str(sorted(hs, key=lambda t: t[2])[:2] + sorted(hs, key=lambda t: t[2])[-1:]))
    report.append(f"{sid}: {cols}x{rows}칸, 쓰는 칸 {len(used)}, 색 {ncol}")

for iid, im in (d.get('images') or {}).items():
    try:
        img = Image.open(f"{A}/{im['file']}").convert('RGBA')
    except FileNotFoundError:
        issues.append(f"{iid}: 파일 없음 {im['file']}"); continue
    if 'w' in im and img.size != (im['w'], im['h']): issues.append(f"{iid}: 크기 {img.size} ≠ 정의 {(im['w'], im['h'])}")
    if iid.startswith('bg') and (np.array(img)[..., 3] < 255).any(): issues.append(f"{iid}: 배경에 투명 픽셀")
    report.append(f"{iid}: {img.size[0]}x{img.size[1]}")

print('\n'.join(report))
print(f"\n문제 {len(issues)}개")
print('\n'.join(issues) if issues else '없음')
sys.exit(1 if issues else 0)
