"""애니메이션 QA: 모든 스프라이트 동작의 프레임을 검사한다.
- 빈 프레임, 시트 밖 프레임
- 조각남: 불투명 픽셀이 떨어진 덩어리로 나뉘었는지 (8방향 연결, 2픽셀 이하 점은 무시)
- 발 기준선: 서 있기·걷기·달리기 동작에서 프레임마다 맨 아래 줄이 얼마나 흔들리는지
- 흔들림: 이어지는 프레임 사이 몸 가운데(x)가 얼마나 튀는지 (되풀이 동작은 마지막 → 첫 프레임 이음매도)
- 멈춘 동작: 여러 프레임 동작인데 이웃 프레임이 같은 그림이면 움직임이 끊겨 보인다
- 좌우 짝: *_left / *_right 동작은 서로 거울에 비친 모양이어야 한다 (실루엣 겹침)
- 속도: fps가 동작 성격에 맞는 범위 안인지
사용: python anim_qa.py <assets 폴더> [미리보기 png]"""
import sys, json
import numpy as np
from PIL import Image
A = sys.argv[1]
d = json.load(open(f"{A}/sprite_definition.json", encoding='utf-8'))
issues, report = [], []
ROWS_FOR = {'obstacles': {'roll': [0], 'fly': [1], 'snap': [2], 'flap': [3]}}
OBJECTS = {'obstacles'}   # 물건은 부품 사이 외곽선(스테이플러 팔과 받침)이 정상이라 이음매 검사는 사람만
SEAMS = {}
MASKS = {}
SIDE = {'climb', 'climb_r', 'climb_l', 'down', 'walk', 'leave'}   # 옆모습: 몸 앞을 지나는 팔의 외곽선은 정상
LOOPS = {'idle', 'walk', 'climb', 'type', 'phone', 'roll', 'fly', 'flap'}
FPS = {'idle': (1, 4), 'walk': (4, 14), 'climb': (6, 16), 'climb_r': (6, 18), 'climb_l': (6, 18), 'down': (4, 12), 'roll': (6, 16),
       'fly': (6, 14), 'flap': (8, 18), 'snap': (6, 14), 'type': (3, 10)}

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
        for r in (ROWS_FOR.get(sid, {}).get(an) or range(rows)):   # 물건 시트는 동작마다 제 행만
            bottoms, cxs, masks = [], [], []
            for c in a['frames']:
                if c >= cols: issues.append(f"{sid}.{an}: 프레임 {c}가 시트 밖 (열 {cols})"); continue
                f = img[r * fh:(r + 1) * fh, c * fw:(c + 1) * fw]; m = f[..., 3] > 0
                if not m.any(): issues.append(f"{sid}.{an} 행{r} 프레임{c}: 비어 있음"); continue
                big = [s for s in comps(m) if s > 2]
                if len(big) > 1: issues.append(f"{sid}.{an} 행{r} 프레임{c}: 조각 {len(big)}개 {sorted(big, reverse=True)}")
                ys, xs = np.nonzero(m); bottoms.append(ys.max()); cxs.append(xs.mean()); masks.append((c, f))
                # 이음매: 외곽선 색 픽셀인데 좌우(또는 위아래) 모두 몸 색(외곽선 아님)인 것 — 몸을 가르는 검은 줄
                o = m & (np.abs(f[..., :3].astype(int) - [0x14, 0x15, 0x18]).sum(-1) < 12)
                bodyc = m & ~o
                lr = o[:, 1:-1] & bodyc[:, :-2] & bodyc[:, 2:]; ud = o[1:-1, :] & bodyc[:-2, :] & bodyc[2:, :]
                seam = int(lr.sum() + ud.sum())
                SEAMS.setdefault(f"{sid}.{an}", []).append(seam)
                if seam > (18 if an in SIDE else 14) and sid not in OBJECTS: issues.append(f"{sid}.{an} 행{r} 프레임{c}: 몸을 가르는 외곽선 {seam}픽셀")
            if len(bottoms) > 1 and an in GROUNDED and an != 'type':
                if max(bottoms) - min(bottoms) > 3: issues.append(f"{sid}.{an} 행{r}: 발 기준선이 {min(bottoms)}~{max(bottoms)}로 흔들림")
                jumps = [abs(cxs[i + 1] - cxs[i]) for i in range(len(cxs) - 1)]
                if jumps and max(jumps) > 4.5: issues.append(f"{sid}.{an} 행{r}: 몸 가운데가 프레임 사이 {max(jumps):.1f}px 튐")
            if r == (ROWS_FOR.get(sid, {}).get(an) or [0])[0]:
                MASKS[(sid, an)] = masks
                uniq = [i for i in range(len(masks)) if masks[i][0] != masks[(i + 1) % len(masks)][0] and np.array_equal(masks[i][1], masks[(i + 1) % len(masks)][1])]
                if len(masks) > 1 and uniq: issues.append(f"{sid}.{an}: 프레임 {[masks[i][0] for i in uniq]}가 다음 프레임과 같은 그림 (멈춘 듯)")
                if len(cxs) > 2 and an in LOOPS and abs(cxs[-1] - cxs[0]) > 4.5: issues.append(f"{sid}.{an}: 되풀이 이음매(마지막→첫 프레임)에서 {abs(cxs[-1] - cxs[0]):.1f}px 튐")
        fps = a.get('fps', 1); lo, hi = FPS.get(an, (1, 24))
        if len(a['frames']) > 1 and not (lo <= fps <= hi): issues.append(f"{sid}.{an}: fps {fps}가 {lo}~{hi} 밖")
        report.append(f"{sid}.{an}: {len(a['frames'])}프레임 × {rows}행 {fps}fps")
# 좌우 짝: 왼쪽 동작의 각 프레임을 뒤집으면 오른쪽 동작의 같은 순서 프레임과 겹쳐야 한다 (몸 가운데 맞춰 IoU)
def iou_flip(fa, fb):
    ma, mb = fa[..., 3] > 0, (fb[..., 3] > 0)[:, ::-1]
    xa, xb = np.nonzero(ma)[1].mean(), np.nonzero(mb)[1].mean(); mb = np.roll(mb, int(round(xa - xb)), axis=1)
    return (ma & mb).sum() / max(1, (ma | mb).sum())
for (sid, an), ms in list(MASKS.items()):
    if '_left' not in an: continue
    other = MASKS.get((sid, an.replace('_left', '_right')))
    if not other: issues.append(f"{sid}.{an}: 짝인 {an.replace('_left', '_right')} 동작이 없음"); continue
    if len(other) != len(ms): issues.append(f"{sid}.{an}: 짝과 프레임 수가 다름 ({len(ms)} vs {len(other)})"); continue
    for (ca, fa), (cb, fb) in zip(ms, other):
        v = iou_flip(fa, fb)
        if v < 0.8: issues.append(f"{sid}.{an} 프레임{ca} ↔ {cb}: 거울 모양 겹침 {v:.2f} (0.8 미만)")
    report.append(f"짝 {sid}.{an} ↔ _right: 겹침 " + ', '.join(f'{iou_flip(fa, fb):.2f}' for (_, fa), (_, fb) in zip(ms, other)))
print('\n'.join(report))
print(f"\n문제 {len(issues)}개")
print('\n'.join(issues) if issues else '없음')
