"""팔·손 해부학 고치기 (16비트 원본 tools/sprites/src16): 팔은 두 개, 손도 두 개.

인형(puppet.py)이 만든 프레임은 원래 몸통 그림(팔이 몸에 붙고 손 끝만 옷자락 모서리에 보이는 그림)을 그대로 쓰고 그 위에 팔다리를 새로 그려,
옷자락 모서리의 작은 손(2~4픽셀)이 남아 '몸통에 박힌 팔'이 하나 더 있는 것처럼 보였다.
- 옆모습(플레이어 17~23, 경쟁 사원 16~27, 사원 0~7, 전화 10·11)과 막기 3(27~29, 33~35): 팔은 새로 그린 팔다리뿐이니 모서리의 작은 손(과 손 외곽선)을 지운다
- 막기 2(24~26, 30~32): 뒤쪽 팔 손이 가슴에 붙어 얼룩처럼 보였다. 몸통을 디자이너가 그린 막기 1(열 3·4: 뻗은 쪽에는 손이 없고 반대쪽 팔이 옆에 내려온 그림)에서 가져온다
여러 번 돌려도 같다. 그다음 upgrade32.py · edge_close.py를 돌린다. 사용: python fix_arms.py <src16 폴더>"""
import sys
import numpy as np
from PIL import Image

D = sys.argv[1]; FW, FH = 32, 48
SIDE = {'player': list(range(17, 24)) + [27, 28, 29, 33, 34, 35], 'rival': list(range(16, 28)), 'staff': list(range(0, 8)) + [10, 11]}


def is_skin(p): r, g, b = int(p[0]), int(p[1]), int(p[2]); return p[3] > 0 and r > 120 and r - b > 45 and r - g > 18 and g > 80 and r - g < 70 and b > 95   # CEO 크림색 옷은 피부가 아니다
def is_hand_line(p): r, g, b = int(p[0]), int(p[1]), int(p[2]); return p[3] > 0 and r > 60 and r > b + 12 and r - g < 30 and g >= b and r + g + b < 360 and not is_skin(p)   # 갈색 손 외곽선 (빨간 바지는 아니다)
def is_dark(p): return p[3] > 0 and int(p[0]) + int(p[1]) + int(p[2]) < 3 * 60


def blobs(f, test):
    h, w = f.shape[:2]; seen = np.zeros((h, w), bool); out = []
    for y in range(h):
        for x in range(w):
            if seen[y, x] or not test(f[y, x]): continue
            st = [(y, x)]; seen[y, x] = True; pts = []
            while st:
                cy, cx = st.pop(); pts.append((cy, cx))
                for dy in (-1, 0, 1):
                    for dx in (-1, 0, 1):
                        yy, xx = cy + dy, cx + dx
                        if 0 <= yy < h and 0 <= xx < w and not seen[yy, xx] and test(f[yy, xx]): seen[yy, xx] = True; st.append((yy, xx))
            out.append(pts)
    return out


def remove_stubs(f):
    """옷자락 모서리(24행 아래)의 작은 손(4픽셀 이하)과 그 손 외곽선을 지운다. 옷 안쪽이면 바로 위 옷 색, 몸 밖이면 투명"""
    n = 0
    for pts in blobs(f, is_skin):
        ys = [p[0] for p in pts]
        if len(pts) > 4 or min(ys) < 24: continue
        area = set(pts)
        for y, x in pts:                                   # 손 외곽선(갈색)·바로 붙은 어두운 점
            for dy in (-1, 0, 1):
                for dx in (-1, 0, 1):
                    yy, xx = y + dy, x + dx
                    if 0 <= yy < FH and 0 <= xx < FW and is_hand_line(f[yy, xx]): area.add((yy, xx))
        for y, x in sorted(area):
            up = next((f[y - k] [x] for k in (1, 2, 3) if y - k >= 0 and f[y - k, x, 3] and not is_skin(f[y - k, x]) and not is_dark(f[y - k, x]) and not is_hand_line(f[y - k, x])), None)
            down = next((f[y + k][x] for k in (1, 2) if y + k < FH and f[y + k, x, 3] and not is_skin(f[y + k, x]) and not is_hand_line(f[y + k, x]) and (y + k, x) not in area), None)
            side = any(f[y, xx, 3] and not is_skin(f[y, xx]) and not is_hand_line(f[y, xx]) and not is_dark(f[y, xx]) for xx in (x - 1, x + 1) if 0 <= xx < FW)
            if up is not None and (side or down is not None): f[y, x] = up       # 옷 안쪽 (아래에 다리가 붙어 있으면 끊기지 않게 옷으로 채운다)
            elif down is not None and side: f[y, x] = down
            else: f[y, x] = 0
            n += 1
    # 지워서 생긴 바깥 가장자리에 외곽선: 투명과 맞닿은 옷 픽셀이 외곽선이 아니면 외곽선 색으로
    return n


def outline_fix(f, before):
    op = f[..., 3] > 0; was = before[..., 3] > 0; out = np.array([0x14, 0x15, 0x18, 255], np.uint8)
    for y in range(FH):
        for x in range(FW):
            if not op[y, x] or is_dark(f[y, x]): continue
            for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
                yy, xx = y + dy, x + dx
                if 0 <= yy < FH and 0 <= xx < FW and not op[yy, xx] and was[yy, xx]: f[y, x] = out; break


def crumbs(f, before):
    """손을 지운 자리에 남은 외곽선 부스러기: 바뀐 곳 둘레(22행 아래)에서 몸(어둡지 않은 픽셀)에 닿지 않거나 혼자 튀어나온 어두운 점을 지운다"""
    changed = np.any(f != before, axis=-1)
    near = np.zeros_like(changed)
    for dy in (-2, -1, 0, 1, 2):
        for dx in (-2, -1, 0, 1, 2): near |= np.roll(np.roll(changed, dy, 0), dx, 1)
    for _ in range(3):
        op = f[..., 3] > 0
        for y in range(22, FH):
            for x in range(FW):
                if not (near[y, x] and op[y, x] and is_dark(f[y, x])): continue
                n8 = [(y + dy, x + dx) for dy in (-1, 0, 1) for dx in (-1, 0, 1) if (dy or dx) and 0 <= y + dy < FH and 0 <= x + dx < FW]
                body = sum(1 for yy, xx in n8 if op[yy, xx] and not is_dark(f[yy, xx]))
                n4 = sum(1 for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)) if 0 <= yy < FH and 0 <= xx < FW and op[yy, xx])
                if body == 0 or n4 <= 1: f[y, x] = 0


def align(base, f):
    return min(range(-4, 5), key=lambda dx: np.abs(np.roll(base[30:], dx, 1).astype(int) - f[30:].astype(int)).sum())


for sid, cols in SIDE.items():
    p = f'{D}/{sid}.png'; a = np.array(Image.open(p).convert('RGBA')); rows = a.shape[0] // FH; total = 0
    for r in range(rows):
        if sid == 'player':                                 # 막기 2: 막기 1 몸통
            for c, src_c, side in ((24, 3, -1), (25, 3, -1), (26, 3, -1), (30, 4, 1), (31, 4, 1), (32, 4, 1)):
                f = a[r * FH:(r + 1) * FH, c * FW:(c + 1) * FW]; base = a[r * FH:(r + 1) * FH, src_c * FW:(src_c + 1) * FW]
                b = np.roll(base, align(base, f), 1)
                cols_t = np.nonzero((b[20:30, :, 3] > 0).any(0))[0]; x0, x1 = cols_t.min(), cols_t.max()
                # 막기 1에서 뻗은 팔이 있는 쪽(몸통 밖)은 빼고, 몸통 칸만 가져온다. 뻗은 팔 어깨 3칸은 막기 2의 팔을 남긴다
                for y in range(16, 32):
                    for x in (range(x0 + 3, x1 + 1) if side < 0 else range(x0, x1 - 2)):
                        f[y, x] = b[y, x]
        for c in cols:
            if (c + 1) * FW > a.shape[1]: continue
            f = a[r * FH:(r + 1) * FH, c * FW:(c + 1) * FW]; before = f.copy()
            total += remove_stubs(f); outline_fix(f, before); crumbs(f, before)
    Image.fromarray(a).save(p); print('arms', sid, total, 'px')
