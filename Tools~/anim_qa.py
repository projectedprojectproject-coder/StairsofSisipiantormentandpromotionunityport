"""애니메이션 QA: 모든 스프라이트 동작의 프레임을 검사한다.
- 빈 프레임, 시트 밖 프레임
- 조각남: 불투명 픽셀이 떨어진 덩어리로 나뉘었는지 (8방향 연결, 2픽셀 이하 점은 무시)
- 발 기준선: 서 있기·걷기·달리기 동작에서 프레임마다 맨 아래 줄이 얼마나 흔들리는지
- 흔들림: 이어지는 프레임 사이 몸 가운데(x)가 얼마나 튀는지
사용: python anim_qa.py <assets 폴더> [미리보기 png]"""
import sys, json
import numpy as np
from PIL import Image
A = sys.argv[1]
d = json.load(open(f"{A}/sprite_definition.json", encoding='utf-8'))
issues, report = [], []
SEAMS = {}
def comps(m):
    h, w = m.shape; seen = np.zeros_like(m, bool); sizes = []
    for y in range(h):
        for x in range(w):
            if m[y, x] and not seen[y, x]:
                st = [(y, x)]; seen[y, x] = True; n = 0
                while st:
                    cy, cx = st.pop(); n += 1
                    for dy in (-1, 0, 1):
                        for dx in (-1, 0, 1):
                            yy, xx = cy + dy, cx + dx
                            if 0 <= yy < h and 0 <= xx < w and m[yy, xx] and not seen[yy, xx]: seen[yy, xx] = True; st.append((yy, xx))
                sizes.append(n)
    return sizes
GROUNDED = {'idle', 'walk', 'climb', 'climb_r', 'climb_l', 'step', 'proud', 'parry_left', 'parry_right', 'parry_left2', 'parry_right2', 'parry_left3', 'parry_right3',
            'parry_hit_left', 'parry_hit_right', 'parry_hit_left2', 'parry_hit_right2', 'parry_hit_left3', 'parry_hit_right3', 'coffee', 'phone', 'attack', 'leave', 'hit', 'roll'}
for sid in ('player', 'rival', 'staff', 'commuter', 'obstacles'):
    sp = d['sprites'].get(sid)
    if not sp: continue
    img = np.array(Image.open(f"{A}/{sp['file']}").convert('RGBA')); fw, fh = sp['frame_w'], sp['frame_h']
    cols, rows = img.shape[1] // fw, img.shape[0] // fh
    for an, a in sp.get('animations', {}).items():
        for r in range(rows):
            bottoms, cxs = [], []
            for c in a['frames']:
                if c >= cols: issues.append(f"{sid}.{an}: 프레임 {c}가 시트 밖 (열 {cols})"); continue
                f = img[r * fh:(r + 1) * fh, c * fw:(c + 1) * fw]; m = f[..., 3] > 0
                if not m.any(): issues.append(f"{sid}.{an} 행{r} 프레임{c}: 비어 있음"); continue
                big = [s for s in comps(m) if s > 2]
                if len(big) > 1: issues.append(f"{sid}.{an} 행{r} 프레임{c}: 조각 {len(big)}개 {sorted(big, reverse=True)}")
                ys, xs = np.nonzero(m); bottoms.append(ys.max()); cxs.append(xs.mean())
                # 이음매: 외곽선 색 픽셀인데 좌우(또는 위아래) 모두 몸 색(외곽선 아님)인 것 — 몸을 가르는 검은 줄
                o = m & (np.abs(f[..., :3].astype(int) - [0x14, 0x15, 0x18]).sum(-1) < 12)
                bodyc = m & ~o
                lr = o[:, 1:-1] & bodyc[:, :-2] & bodyc[:, 2:]; ud = o[1:-1, :] & bodyc[:-2, :] & bodyc[2:, :]
                seam = int(lr.sum() + ud.sum())
                SEAMS.setdefault(f"{sid}.{an}", []).append(seam)
                if seam > 14: issues.append(f"{sid}.{an} 행{r} 프레임{c}: 몸을 가르는 외곽선 {seam}픽셀")
            if len(bottoms) > 1 and an in GROUNDED and an != 'type':
                if max(bottoms) - min(bottoms) > 3: issues.append(f"{sid}.{an} 행{r}: 발 기준선이 {min(bottoms)}~{max(bottoms)}로 흔들림")
                jumps = [abs(cxs[i + 1] - cxs[i]) for i in range(len(cxs) - 1)]
                if jumps and max(jumps) > 4.5: issues.append(f"{sid}.{an} 행{r}: 몸 가운데가 프레임 사이 {max(jumps):.1f}px 튐")
        report.append(f"{sid}.{an}: {len(a['frames'])}프레임 × {rows}행")
print('\n'.join(report))
print(f"\n문제 {len(issues)}개")
print('\n'.join(issues) if issues else '없음')
