"""사람 스프라이트 해부학 QA: 팔은 두 개, 손도 두 개, 손은 어깨에서 팔 길이 안에.

프레임마다 피부색 덩어리를 찾아 가장 큰 것(얼굴)과 목(얼굴 바로 아래 가운데의 작은 덩어리)을 빼면 손이다.
- 손이 셋 이상: 몸통에 박힌 팔(옛 몸통 그림의 옷자락 모서리 손)이 남았다
- 손이 어깨에서 너무 멀다: 팔이 늘어났거나 떨어진 손
- 옷자락 모서리의 아주 작은 손(밀도 2배 기준 2픽셀 이하, 4배는 8픽셀 이하)이 다른 손과 함께 있다: 몸통에 붙은 팔 조각
밀도(pixel_scale)에 맞춰 픽셀 한계를 늘린다. 사용: python anatomy_qa.py <assets 폴더>"""
import sys, json
import numpy as np
from PIL import Image

A = sys.argv[1]
d = json.load(open(f"{A}/sprite_definition.json", encoding='utf-8'))
K = d.get('pixel_scale', 2) / 2
PEOPLE = ('player', 'rival', 'staff')   # commuter: 손을 그리지 않는 작은 실루엣
issues, report = [], []


def skin_mask(f):
    r, g, b = (f[..., i].astype(int) for i in range(3))
    return (f[..., 3] > 0) & (r > 120) & (r - b > 45) & (r - g > 18) & (g > 80) & (r - g < 70) & (b > 95)   # 상자(골판지 b 낮음)·CEO 크림색 옷(r-b 작음)은 빼고


def blobs(m):
    h, w = m.shape; seen = np.zeros_like(m); out = []
    for y in range(h):
        for x in range(w):
            if m[y, x] and not seen[y, x]:
                st = [(y, x)]; seen[y, x] = True; pts = []
                while st:
                    cy, cx = st.pop(); pts.append((cy, cx))
                    for dy in (-1, 0, 1):
                        for dx in (-1, 0, 1):
                            yy, xx = cy + dy, cx + dx
                            if 0 <= yy < h and 0 <= xx < w and m[yy, xx] and not seen[yy, xx]: seen[yy, xx] = True; st.append((yy, xx))
                ys, xs = zip(*pts); out.append({'n': len(pts), 'x': float(np.mean(xs)), 'y': float(np.mean(ys)), 'y0': min(ys), 'y1': max(ys)})
    return out


for sid in PEOPLE:
    sp = d['sprites'].get(sid)
    if not sp: continue
    img = np.array(Image.open(f"{A}/{sp['file']}").convert('RGBA')); fw, fh = sp['frame_w'], sp['frame_h']
    used = sorted({c for a in sp['animations'].values() for c in a['frames']})
    names = {c: '/'.join(a for a, v in sp['animations'].items() if c in v['frames']) for c in used}
    rows = img.shape[0] // fh
    for r in range(rows):
        for c in used:
            f = img[r * fh:(r + 1) * fh, c * fw:(c + 1) * fw]
            bs = [b for b in blobs(skin_mask(f)) if b['n'] >= 2 * K * K]
            if not bs: continue
            head = max(bs, key=lambda b: b['n'])
            neck = [b for b in bs if b is not head and b['y0'] <= head['y1'] + 6 * K and abs(b['x'] - head['x']) < 6 * K and b['n'] <= 4 * K * K]
            hands = [b for b in bs if b is not head and b not in neck]
            # 어깨: 얼굴 아래 옷이 시작되는 줄에서 몸 양 끝
            op = f[..., 3] > 0
            sy = int(min(fh - 1, head['y1'] + 3 * K))
            xs = np.nonzero(op[sy])[0]
            if len(xs) == 0: continue
            shoulders = [(xs.min() + 2 * K, sy), (xs.max() - 2 * K, sy)]
            tag = f"{sid} 행{r} 칸{c} ({names[c]})"
            if len(hands) > 2:
                issues.append(f"{tag}: 손이 {len(hands)}개 " + str([(round(h['x'] / K), round(h['y'] / K), h['n']) for h in hands]) + " (팔이 몸통에 박혀 하나 더 있음)")
            for h in hands:
                reach = min(np.hypot(h['x'] - sx, h['y'] - sy_) for sx, sy_ in shoulders)
                if reach > 17 * K: issues.append(f"{tag}: 손이 어깨에서 {reach / K:.1f}px (팔 길이 17 넘음)")
            tiny = [h for h in hands if h['n'] <= 2 * K * K and h['y0'] >= 24 * K]
            if tiny and len(hands) >= 2 and any(h['n'] > 4 * K * K for h in hands):
                issues.append(f"{tag}: 옷자락 모서리에 몸통에 붙은 손 조각 {len(tiny)}개")
    report.append(f"{sid}: {len(used)}프레임 × {rows}행")

print('\n'.join(report))
print(f"\n문제 {len(issues)}개")
print('\n'.join(issues[:60]) if issues else '없음')
sys.exit(1 if issues else 0)
