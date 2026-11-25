"""컨셉 아트처럼 옆모습으로 달리고 계단을 오르는 프레임, 경쟁 사원 서 있기, 막기 동작 3가지(왼·오른)를 만든다.
머리·몸통은 기존 그림, 팔다리는 관절 각도로 그린다. 외곽선은 한 번에(몸 위에는 칠하지 않고) 그린 뒤 색을 칠해 마디가 끊겨 보이지 않게 한다.
player.png = 원래 17열 + 17~23 옆모습(서 있기 2, 오르기 3, 내려가기 2) + 24~35 막기 동작 2·3 (왼: 자세·번쩍·밀림 ×2, 오른 ×2)
rival.png  = 원래 16열 + 16~19 달리기, 20~23 계단 달려 오르기, 24·25 내려가기, 26·27 서 있기
staff.png  = 로비의 사원들 (4가지 옷: 플레이어 2~5행 색) 0~3 걷기, 4·5 서 있기, 6·7 커피 마시기, 8·9 책상에서 타자(위몸만), 10·11 전화
사용: python puppet.py <assets 폴더>   (몇 번 돌려도 같은 결과: 원래 열만 남기고 새로 만든다)"""
import sys, json, math
from collections import Counter
import numpy as np
from PIL import Image
A = sys.argv[1]; FW, FH = 32, 48
OUT = np.array([0x14, 0x15, 0x18, 255], np.uint8)
SKIN = np.array([0xd9, 0xb5, 0x96, 255], np.uint8); SKIN_D = np.array([0xb8, 0x93, 0x7a, 255], np.uint8)

def shade(c, k): c = c.copy(); c[:3] = np.clip(c[:3].astype(int) * k, 0, 255); return c
def ang(p, deg, L): r = math.radians(deg); return (p[0] + math.sin(r) * L, p[1] + math.cos(r) * L)

class Canvas:
    def __init__(self): self.a = np.zeros((FH, FW, 4), np.uint8)
    def dot(self, x, y, c, r, under=False):
        for yy in range(int(math.floor(y - r)), int(math.ceil(y + r)) + 1):
            for xx in range(int(math.floor(x - r)), int(math.ceil(x + r)) + 1):
                if 0 <= xx < FW and 0 <= yy < FH and (xx - x) ** 2 + (yy - y) ** 2 <= r * r + 0.25:
                    if not under or not self.a[yy, xx, 3]: self.a[yy, xx] = c
    def seg(self, p, q, c, r, under=False):
        n = int(max(abs(q[0] - p[0]), abs(q[1] - p[1])) * 2) + 1
        for i in range(n + 1):
            t = i / n; self.dot(p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t, c, r, under)

class Limb:
    """관절 두 마디. 끝은 신발(shoe) 또는 손(hand)."""
    def __init__(self, p, a1, a2, L1, L2, col, r, end, endcol, endlen=3):
        self.p = p; self.k = ang(p, a1, L1); self.f = ang(self.k, a2, L2); self.col = col; self.r = r; self.end = end; self.endcol = endcol; self.endlen = endlen
    def outline(self, cv, under):
        cv.seg(self.p, self.k, OUT, self.r + 1, under); cv.seg(self.k, self.f, OUT, self.r + 1, under)
        if self.end == 'shoe': cv.seg(self.f, (self.f[0] + self.endlen, self.f[1]), OUT, self.r + 0.6, under)
        else: cv.dot(self.f[0], self.f[1], OUT, self.r + 1.2, under)
    def fill(self, cv):
        cv.seg(self.p, self.k, self.col, self.r); cv.seg(self.k, self.f, self.col, self.r)
        if self.end == 'shoe': cv.seg(self.f, (self.f[0] + self.endlen, self.f[1]), self.endcol, self.r - 0.4)
        else: cv.dot(self.f[0], self.f[1], self.endcol, self.r + 0.2)

def palette(src):
    def common(y0, y1, n=0):
        skip = {(0x14, 0x15, 0x18), (0xe8, 0xe6, 0xdf), (0xd9, 0xb5, 0x96), (0xb8, 0x93, 0x7a), (0x1e, 0x1f, 0x22), (0xa1, 0x3d, 0x3d)}
        c = Counter(tuple(src[y, x]) for y in range(y0, y1) for x in range(FW) if src[y, x, 3] and tuple(src[y, x, :3]) not in skip)
        m = c.most_common(); return np.array(m[min(n, len(m) - 1)][0], np.uint8)
    return dict(suit=common(17, 27), suit_d=common(17, 27, 1), pants=common(33, 41), shoe=common(45, 47))

def bbox_cols(src, y0, y1):
    xs = [x for x in range(FW) if src[y0:y1, x, 3].any()]
    return min(xs), max(xs) + 1
SHIRT = {(0xe8, 0xe6, 0xdf), (0xa1, 0x3d, 0x3d), (0x1e, 0x1f, 0x22)}   # 셔츠·넥타이 (경쟁 사원은 검은 넥타이)
def side_torso(torso, suit):
    """옆모습: 가운데 셔츠·넥타이를 양복 색으로 덮고, 앞(오른쪽) 가장자리 안쪽에 좁은 깃·넥타이를 다시 그린다 (넥타이가 삐져나오지 않게)."""
    t = torso.copy(); tie = None
    from collections import Counter
    keep = {c for c, _ in Counter(tuple(t[y, x, :3]) for y in range(t.shape[0]) for x in range(t.shape[1]) if t[y, x, 3]).most_common(4)} | SHIRT | {tuple(OUT[:3]), tuple(SKIN[:3]), tuple(SKIN_D[:3])}
    for y in range(t.shape[0]):   # 배지 같은 작은 장식 색은 양복 색으로 (옆모습에서는 가려진다)
        for x in range(t.shape[1]):
            if t[y, x, 3] and tuple(t[y, x, :3]) not in keep: t[y, x] = suit
    for y in range(t.shape[0]):
        for x in range(t.shape[1]):
            c = tuple(t[y, x, :3])
            if t[y, x, 3] and c in SHIRT:
                if c != (0xe8, 0xe6, 0xdf) and tie is None: tie = t[y, x].copy()
                t[y, x] = suit
    if tie is None: tie = np.array([0xa1, 0x3d, 0x3d, 255], np.uint8)
    shirt = np.array([0xe8, 0xe6, 0xdf, 255], np.uint8)
    for y in range(t.shape[0]):
        xs = [x for x in range(t.shape[1]) if t[y, x, 3] and not (t[y, x, :3] == OUT[:3]).all()]
        if not xs: continue
        f = max(xs)
        if y <= 2: t[y, f - 1] = shirt; t[y, f - 2] = shirt          # 깃
        elif y <= 10: t[y, f - 1] = tie                               # 넥타이 (앞 가장자리 안쪽 1칸)
    return t
def body(src, cv, dx, dy, lean, x0=None, x1=None, side=False):
    """기존 정면 그림의 머리·몸통(팔 자리 빼고)을 기울여 붙이고 가장자리에 외곽선. 머리·몸통 폭은 그림마다 실제 범위로 자른다."""
    bx0, bx1 = bbox_cols(src, 16, 29)
    if x0 is None: x0, x1 = bx0 + 2, bx1 - 2                         # 몸통: 팔 두 칸씩 뺀다
    hx0, hx1 = bbox_cols(src, 3, 15)
    torso = src[15:31, x0:x1]; head = src[3:15, hx0:hx1]
    if side: torso = side_torso(torso, palette(src)['suit'])
    for y in range(torso.shape[0]):
        off = round(lean * (15 - y) / 15); row = torso[y]
        xs = [x for x in range(row.shape[0]) if row[x, 3] and not (row[x, :3] == OUT[:3]).all()]
        for x in range(row.shape[0]):
            if row[x, 3]: cv.a[15 + y + dy, x0 + x + off + dx] = row[x]
        if xs:
            cv.a[15 + y + dy, max(0, x0 + min(xs) + off + dx - 1)] = OUT; cv.a[15 + y + dy, min(FW - 1, x0 + max(xs) + off + dx + 1)] = OUT
    for y in range(head.shape[0]):
        for x in range(head.shape[1]):
            if head[y, x, 3] and 0 <= hx0 + x + lean + dx < FW: cv.a[3 + y + dy, hx0 + x + lean + dx] = head[y, x]

def side_figure(src, pose):
    pal = palette(src); cv = Canvas(); bob = pose.get('bob', 0); lean = pose.get('lean', 2)
    hip = (16, 31 + bob); sh = (16 + lean + 1, 18 + bob)
    legB = Limb(hip, *pose['legB'], 8.5, 8.5, shade(pal['pants'], 0.55), 2.0, 'shoe', shade(pal['shoe'], 0.7))   # 먼 다리는 확실히 어둡게
    armB = Limb((sh[0] - 1, sh[1]), *pose['armB'], 6, 6, shade(pal['suit_d'], 0.65), 1.5, 'hand', shade(SKIN, 0.8))
    legF = Limb(hip, *pose['legF'], 8.5, 8.5, pal['pants'], 2.0, 'shoe', pal['shoe'])
    armF = Limb(sh, *pose['armF'], 6, 6, pal['suit'], 1.5, 'hand', SKIN)
    for l in (legB, armB): l.outline(cv, False)
    for l in (legB, armB): l.fill(cv)
    body(src, cv, 0, bob, lean, side=True)
    for l in (legF, armF): l.outline(cv, True)          # 몸 위에는 외곽선을 칠하지 않는다 (끊겨 보이지 않게)
    for l in (legF, armF): l.fill(cv)
    return cv.a

def front_parry(src, arms, lean, L1=4.0, L2=3.0):
    """정면 막기 자세 (왼쪽으로). 다리는 기존 그림, 팔 두 개를 각도로."""
    pal = palette(src); cv = Canvas()
    legs = src[30:48]; cv.a[30:48] = legs
    body(src, cv, -lean, 0, 0)
    ls, rs = (10 - lean, 17), (21 - lean, 17)
    L = [Limb(ls, a1, a2, L1, L2, pal['suit'], 1.5, 'hand', SKIN) if who == 'L' else Limb(rs, a1, a2, L1, L2, pal['suit_d'], 1.5, 'hand', SKIN) for who, a1, a2 in arms]
    for l in L: l.outline(cv, True)
    for l in L: l.fill(cv)
    return cv.a

def side_prop(src, pose, prop=None):
    f = side_figure(src, pose)
    if prop:   # 손에 든 것: 커피잔(흰색) — 앞 손 위치는 pose로 다시 계산
        lean = pose.get('lean', 2); bob = pose.get('bob', 0); sh = (16 + lean + 1, 18 + bob)
        k = ang(sh, pose['armF'][0], 6); h = ang(k, pose['armF'][1], 6); x, y = int(round(h[0])), int(round(h[1]))
        for yy in range(y - 3, y + 1):
            for xx in range(x, x + 3):
                if 0 <= xx < FW and 0 <= yy < FH: f[yy, xx] = OUT if (yy in (y - 3, y) or xx in (x, x + 2)) else np.array([0xe8, 0xe6, 0xdf, 255], np.uint8)
    return f

def front_pose(src, arms, head_dy=0, cut=None, phone=False):
    """정면 자세 (다리는 기존 그림). arms: [('L'|'R', 위팔, 아래팔)], cut: 이 행부터 아래는 지운다(책상 뒤에 앉은 위몸)."""
    pal = palette(src); cv = Canvas(); cv.a[30:48] = src[30:48]
    body(src, cv, 0, 0, 0)
    if head_dy:
        hx0, hx1 = bbox_cols(src, 3, 15); head = src[3:15, hx0:hx1]; cv.a[3:15 + 1, hx0:hx1] = 0
        for y in range(head.shape[0]):
            for x in range(head.shape[1]):
                if head[y, x, 3]: cv.a[3 + y + head_dy, hx0 + x] = head[y, x]
    ls, rs = (10, 17), (21, 17)
    L = [Limb(ls, a1, a2, 6, 6, pal['suit'], 1.5, 'hand', SKIN) if who == 'L' else Limb(rs, a1, a2, 6, 6, pal['suit_d'], 1.5, 'hand', SKIN) for who, a1, a2 in arms]
    for l in L: l.outline(cv, True)
    for l in L: l.fill(cv)
    if phone:   # 오른손에 밝은 회색 전화기 (머리카락과 구분되게)
        h = L[-1].f; x, y = int(round(h[0])), int(round(h[1]))
        x += 2   # 손 옆 (머리카락과 겹치지 않게)
        for yy in range(y - 3, y + 4):
            for xx in range(x - 2, x + 1):
                if 0 <= xx < FW and 0 <= yy < FH: cv.a[yy, xx] = OUT if xx == x - 2 or yy in (y - 3, y + 3) else np.array([0x9a, 0xa0, 0xa8, 255], np.uint8)
    if cut is not None: cv.a[cut:] = 0
    return cv.a

def flash(f, side):
    """막기 성공 순간: 막는 쪽 손(살색)을 하얗게, 그쪽 팔을 밝게."""
    g = f.copy(); xs = range(0, 13) if side < 0 else range(19, 32)
    for y in range(FH):
        for x in xs:
            c = g[y, x]
            if not c[3]: continue
            out_body = x < 9 if side < 0 else x > 22
            if out_body and ((c[:3] == SKIN[:3]).all() or (c[:3] == SKIN_D[:3]).all()): g[y, x, :3] = 255   # 뻗은 손만 (얼굴은 그대로)
            elif (x < 10 if side < 0 else x > 21):   # 몸통 밖으로 뻗은 팔만 밝게 (얼굴은 10~21열이라 건드리지 않는다)
                if y < 30 and not (c[:3] == OUT[:3]).all(): g[y, x, :3] = np.minimum(255, c[:3].astype(int) + 70)
    return g
def recoil(f, side):
    g = np.zeros_like(f)
    if side < 0: g[:, 1:] = f[:, :-1]
    else: g[:, :-1] = f[:, 1:]
    return g
def mirror(f): return f[:, ::-1].copy()

P_IDLE = [dict(legF=(4, 0), legB=(-4, 0), armF=(6, 10), armB=(-6, -2), lean=0, bob=0), dict(legF=(4, 0), legB=(-4, 0), armF=(6, 12), armB=(-6, 0), lean=0, bob=1)]
P_CLIMB_N = dict(legF=(78, -8), legB=(-22, -10), armF=(-40, 10), armB=(55, 110), lean=3, bob=0)
P_PASS = dict(legF=(18, -25), legB=(-8, -2), armF=(8, 30), armB=(-8, 20), lean=3, bob=-1)
P_CLIMB_F = dict(legF=(-22, -10), legB=(78, -8), armF=(55, 110), armB=(-40, 10), lean=3, bob=0)
P_CROUCH = dict(legF=(50, -40), legB=(30, -50), armF=(40, 80), armB=(-30, 30), lean=1, bob=3)
P_DSTEP = dict(legF=(25, 10), legB=(55, -60), armF=(-50, -20), armB=(40, 70), lean=-1, bob=2)
R_RUN = [dict(legF=(48, 8), legB=(-38, -95), armF=(-55, -20), armB=(50, 150), lean=3, bob=1),
         dict(legF=(75, -30), legB=(-12, -30), armF=(-10, 40), armB=(20, 80), lean=3, bob=-1),
         dict(legF=(-38, -95), legB=(48, 8), armF=(50, 150), armB=(-55, -20), lean=3, bob=1),
         dict(legF=(-12, -30), legB=(75, -30), armF=(20, 80), armB=(-10, 40), lean=3, bob=-1)]
R_CLIMB = [dict(legF=(85, -5), legB=(-30, -40), armF=(-55, -20), armB=(50, 150), lean=3, bob=0),
           dict(legF=(30, -40), legB=(-10, -20), armF=(-10, 40), armB=(20, 80), lean=3, bob=-1),
           dict(legF=(-30, -40), legB=(85, -5), armF=(50, 150), armB=(-55, -20), lean=3, bob=0),
           dict(legF=(-10, -20), legB=(30, -40), armF=(20, 80), armB=(-10, 40), lean=3, bob=-1)]
R_IDLE = [dict(legF=(6, 0), legB=(-5, 0), armF=(10, 25), armB=(-8, 10), lean=1, bob=0), dict(legF=(6, 0), legB=(-5, 0), armF=(10, 28), armB=(-8, 12), lean=1, bob=1)]
# 막기 동작 2: 두 손으로 밀어내기 (두 팔 모두 왼쪽으로, 오른팔은 몸 앞을 가로질러), 3: 위로 쳐내기 (왼팔을 비스듬히 위로, 오른팔은 굽혀 옆구리에)
PARRY2 = [('R', -70, -95), ('L', -100, -105)]   # 손이 x 2~ 안에 들어오게 짧은 팔
PARRY3 = [('R', 15, 70), ('L', -135, -160)]

S_WALK = [dict(legF=(25, -5), legB=(-22, -25), armF=(-20, -10), armB=(20, 10), lean=1, bob=0),
          dict(legF=(6, -12), legB=(-3, -4), armF=(-8, 0), armB=(10, 15), lean=1, bob=-1),
          dict(legF=(-22, -25), legB=(25, -5), armF=(20, 10), armB=(-20, -10), lean=1, bob=0),
          dict(legF=(-3, -4), legB=(6, -12), armF=(10, 15), armB=(-8, 0), lean=1, bob=-1)]
S_COFFEE = [dict(legF=(4, 0), legB=(-4, 0), armF=(120, 200), armB=(-6, 0), lean=0, bob=0), dict(legF=(4, 0), legB=(-4, 0), armF=(45, 110), armB=(-6, 0), lean=0, bob=0)]
S_TYPE = [[('L', -40, 80), ('R', 40, -80)], [('L', -32, 70), ('R', 44, -88)]]   # 팔꿈치는 바깥, 아래팔은 안쪽 자판으로
S_PHONE = [[('L', 8, 10), ('R', 160, 230)], [('L', -25, -45), ('R', 160, 230)]]
def sheet(p): return np.array(Image.open(p).convert('RGBA'))
pp = f"{A}/player.png"; P = sheet(pp)[:, :FW * 17]
rows = P.shape[0] // FH; out = np.zeros((P.shape[0], FW * 36, 4), np.uint8); out[:, :FW * 17] = P
for r in range(rows):
    src = P[r * FH:(r + 1) * FH, 0:FW]
    frames = [side_figure(src, q) for q in (P_IDLE[0], P_IDLE[1], P_CLIMB_N, P_PASS, P_CLIMB_F, P_CROUCH, P_DSTEP)]
    p2 = front_parry(src, PARRY2, 0); p3 = front_parry(src, PARRY3, 1, 5.5, 5.0)
    lefts = [p2, flash(p2, -1), recoil(p2, -1), p3, flash(p3, -1), recoil(p3, -1)]
    rights = [mirror(f) for f in (p2, p3)]
    rights = [rights[0], flash(rights[0], +1), recoil(rights[0], +1), rights[1], flash(rights[1], +1), recoil(rights[1], +1)]
    for k, f in enumerate(frames + lefts + rights): out[r * FH:(r + 1) * FH, (17 + k) * FW:(18 + k) * FW] = f
Image.fromarray(out).save(pp); print('player.png ->', out.shape[1] // FW)
rp = f"{A}/rival.png"; R = sheet(rp)[:, :FW * 16]
out = np.zeros((FH, FW * 28, 4), np.uint8); out[:, :FW * 16] = R
src = R[:, FW:2 * FW]
frames = [side_figure(src, q) for q in R_RUN + R_CLIMB + [P_CROUCH, P_DSTEP] + R_IDLE]
for k, f in enumerate(frames): out[:, (16 + k) * FW:(17 + k) * FW] = f
Image.fromarray(out).save(rp); print('rival.png ->', out.shape[1] // FW)

# 로비 사원들
st = np.zeros((FH * 4, FW * 12, 4), np.uint8)
for r in range(4):
    src = P[(r + 1) * FH:(r + 2) * FH, 0:FW]
    frames = [side_figure(src, q) for q in S_WALK] + [side_figure(src, q) for q in P_IDLE] + [side_prop(src, q, 'cup') for q in S_COFFEE]
    frames += [front_pose(src, S_TYPE[0], 0, 27), front_pose(src, S_TYPE[1], 1, 27), front_pose(src, S_PHONE[0], 0, None, True), front_pose(src, S_PHONE[1], 1, None, True)]
    for k, f in enumerate(frames): st[r * FH:(r + 1) * FH, k * FW:(k + 1) * FW] = f
Image.fromarray(st).save(f"{A}/staff.png"); print('staff.png ->', st.shape[1] // FW, 'x', st.shape[0] // FH)

def upd(d):
    pa = d['sprites']['player']['animations']
    pa['idle'] = {'frames': [17, 18], 'fps': 2}
    pa['climb_r'] = {'frames': [19, 20], 'fps': 14}; pa['climb_l'] = {'frames': [21, 20], 'fps': 14}
    pa['down'] = {'frames': [22, 23], 'fps': 8}
    pa['parry_left2'] = {'frames': [24], 'fps': 1}; pa['parry_hit_left2'] = {'frames': [25, 26, 24], 'fps': 18}
    pa['parry_left3'] = {'frames': [27], 'fps': 1}; pa['parry_hit_left3'] = {'frames': [28, 29, 27], 'fps': 18}
    pa['parry_right2'] = {'frames': [30], 'fps': 1}; pa['parry_hit_right2'] = {'frames': [31, 32, 30], 'fps': 18}
    pa['parry_right3'] = {'frames': [33], 'fps': 1}; pa['parry_hit_right3'] = {'frames': [34, 35, 33], 'fps': 18}
    ra = d['sprites']['rival']['animations']
    ra['walk'] = {'frames': [16, 17, 18, 19], 'fps': 10}; ra['climb'] = {'frames': [20, 21, 22, 23], 'fps': 10}
    ra['down'] = {'frames': [24, 25], 'fps': 8}; ra['idle'] = {'frames': [26, 27], 'fps': 2}
    d['sprites']['staff'] = {'file': 'staff.png', 'frame_w': 32, 'frame_h': 48, 'variants': {'a': 0, 'b': 1, 'c': 2, 'd': 3},
                             'animations': {'walk': {'frames': [0, 1, 2, 3], 'fps': 6}, 'idle': {'frames': [4, 5], 'fps': 2}, 'coffee': {'frames': [6, 7], 'fps': 1},
                                            'type': {'frames': [8, 9], 'fps': 6}, 'phone': {'frames': [10, 11], 'fps': 2}}}
jp = f"{A}/sprite_definition.json"; d = json.load(open(jp, encoding='utf-8')); upd(d); open(jp, 'w', encoding='utf-8').write(json.dumps(d, ensure_ascii=False, indent=2))
js = f"{A}/sprite_definition.js"; t = open(js, encoding='utf-8').read(); pre = t[:t.index('{')]; d2 = json.loads(t[t.index('{'):].rstrip().rstrip(';')); upd(d2)
open(js, 'w', encoding='utf-8').write(pre + json.dumps(d2, ensure_ascii=False) + ';\n')
print('done')
