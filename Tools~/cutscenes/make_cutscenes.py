"""컷신 그림 만들기 (인트로 · 새 판 시작 · 100층 엔딩). 2배 밀도(논리 1px = 2px) 픽셀 아트.

그림체는 사용자가 준 참고 영상을 따른다:
- 채도가 낮은 청록 밤하늘, 크고 부드러운 구름 덩어리(위는 밝고 아래는 어둡고 가장자리는 디더링), 배경에는 검은 외곽선 없음
- 슬레이트 청회색 빌딩에 작고 따뜻한 노란 창이 촘촘히, 멀수록 안개 색, 층층이 좁아지는 큰 빌딩
- 사람은 거의 검은 양복 실루엣에 창빛을 받은 하얀 테두리 빛만 또렷하게
- 책상 위 통보는 원근으로 누운 종이, 계단처럼 깎인 가장자리, 붉은 굵은 글자, 스탠드의 따뜻한 빛은 디더링으로 부드럽게
움직임(키·이징)은 game.js 대본이 맡는다 (피자 타워식 예비 동작·스미어·충격 프레임). 소리 효과는 글자로 쓰지 않는다.

배경  cs_office_night, cs_office_fg, cs_desk_close, cs_desk_morning, cs_rage_bg, cs_window_tall, cs_ceo_room, cs_ceo_fg, cs_button_close,
      cs_tower_night, cs_office_morning, cs_office_morning_fg
시트  cs_hero(128x192, 20), cs_paper(400x240, 2), cs_fx(64x64, 9), cs_boom(192x192, 8), cs_button(96x80, 2), cs_hand(240x240, 5)
사용: python make_cutscenes.py <assets 폴더> <굵은 글꼴 .ttf>"""
import sys, json, math, random
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont

OUT = Path(sys.argv[1]); FONT = sys.argv[2]
BAYER4 = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0

SKY = [(28, 42, 42), (34, 52, 50), (42, 62, 59), (50, 72, 67), (58, 82, 75)]
CLOUD_D, CLOUD, CLOUD_L = (36, 54, 52), (52, 76, 70), (70, 96, 88)
HAZE = (46, 62, 66); BLD_MID = (36, 46, 56); TOWER_L, TOWER_R = (44, 54, 66), (30, 37, 47)
WIN = [(232, 202, 128), (206, 176, 104), (150, 134, 92)]
SUIT, SUIT_M, RIM = (15, 17, 21), (25, 28, 34), (236, 240, 234)
HAIR, HAIR_L = (30, 24, 21), (58, 44, 36)
SKIN, SKIN_D = (198, 154, 124), (160, 118, 94)
SHIRT, TIE = (214, 206, 200), (150, 44, 42)
WOOD = [(46, 30, 20), (66, 42, 26), (88, 58, 34), (112, 76, 44), (138, 98, 58)]


def mix(a, b, k): return tuple(int(a[i] + (b[i] - a[i]) * k) for i in range(3))


def blur(a, r):
    a = a.astype(float)
    for _ in range(3):
        p = np.pad(a, r, mode='edge'); c = np.cumsum(np.cumsum(p, 0), 1); c = np.pad(c, ((1, 0), (1, 0)))
        k = 2 * r + 1; h, w = a.shape
        a = (c[k:k + h, k:k + w] - c[:h, k:k + w] - c[k:k + h, :w] + c[:h, :w]) / (k * k)
    return a


def bayer(h, w): return np.tile(BAYER4, (h // 4 + 1, w // 4 + 1))[:h, :w]
def to_img(arr): return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))


def sky(w, h, cols=SKY, top=0, bottom=None):
    bottom = h if bottom is None else bottom
    a = np.zeros((h, w, 4)); a[..., 3] = 255
    t = np.clip((np.arange(h) - top) / max(1, bottom - top), 0, 1)[:, None] * (len(cols) - 1) + bayer(h, w) * 0.999
    a[..., :3] = np.array(cols, float)[np.clip(t.astype(int), 0, len(cols) - 1)]
    return a


def clouds(a, seed, y0, y1, n=6, scale=1.0, cols=(CLOUD_D, CLOUD, CLOUD_L)):
    """크고 부드러운 구름: 흐린 덩어리를 디더링으로 자르고, 위는 밝게 아래는 어둡게"""
    h, w = a.shape[:2]; rnd = random.Random(seed); m = np.zeros((h, w)); yy, xx = np.mgrid[0:h, 0:w]
    for _ in range(n):
        cx = rnd.uniform(-60, w + 60); cy = rnd.uniform(y0, y1); L = rnd.uniform(120, 260) * scale
        for _k in range(rnd.randint(5, 9)):
            ex = cx + rnd.uniform(-L / 2, L / 2); ey = cy + rnd.uniform(-10, 10) * scale; rx = rnd.uniform(30, 70) * scale; ry = rnd.uniform(10, 22) * scale
            m = np.maximum(m, 1 - ((xx - ex) / rx) ** 2 - ((yy - ey) / ry) ** 2)
    m = blur(np.clip(m, 0, 1), 4)
    cut = m > 0.25 + (bayer(h, w) - 0.5) * 0.25
    up = np.roll(m, 6, 0); dn = np.roll(m, -6, 0)
    top = cut & (up < m - 0.04); bot = cut & (dn < m - 0.06)
    a[cut, :3] = cols[1]; a[bot, :3] = cols[0]; a[top, :3] = cols[2]
    return a


def win_grid(a, x0, y0, x1, y1, rnd, lit, ww=2, wh=3, gx=3, gy=4, wall=None, dim=1.0):
    """작은 창이 촘촘히. 층마다 불 켜진 칸이 몇 개씩 이어진다"""
    h, w = a.shape[:2]
    for y in range(int(y0) + 3, int(y1) - wh, wh + gy):
        on = False; run = 0
        for x in range(int(x0) + 2, int(x1) - ww - 1, ww + gx):
            if run <= 0: on = rnd.random() < lit; run = rnd.randint(1, 5)
            run -= 1
            if not (0 <= x < w - ww and 0 <= y < h - wh): continue
            if on: a[y:y + wh, x:x + ww, :3] = [ci * dim for ci in (WIN[0] if rnd.random() < 0.6 else WIN[1])]
            elif wall is not None: a[y:y + wh, x:x + ww, :3] = [ci * 0.8 for ci in wall]


def skyline(a, base, seed, layers):
    rnd = random.Random(seed); h, w = a.shape[:2]
    for hk, col, lit, dim in layers:
        x = -20
        while x < w:
            bw = rnd.randint(30, 80); bh = int(rnd.randint(30, 120) * hk + 16)
            a[max(0, base - bh):base, max(0, x):min(w, x + bw), :3] = col
            if rnd.random() < 0.35:
                tx = x + rnd.randint(4, max(5, bw - 12)); a[max(0, base - bh - 10):max(0, base - bh), max(0, tx):min(w, tx + 8), :3] = col
            win_grid(a, x, base - bh, x + bw, base, rnd, lit, wall=col, dim=dim)
            x += bw + rnd.randint(-4, 6)


def tower(a, cx, base, top, seed, lit=0.5, beacon=True, W=160):
    """층층이 좁아지는 큰 빌딩: 왼쪽 면은 밝게 오른쪽 면은 어둡게, 안테나와 붉은 등"""
    rnd = random.Random(seed); H = base - top
    tiers = [(1.0, 0.36), (0.86, 0.2), (0.7, 0.09), (0.54, 0.03), (0.36, 0.0)]
    y_low = base
    for wk, hk in tiers:
        tw = int(W * wk); y_hi = int(top + H * hk); x0, x1 = cx - tw // 2, cx + tw // 2; xm = x1 - tw // 4
        a[y_hi:y_low, x0:xm, :3] = TOWER_L; a[y_hi:y_low, xm:x1, :3] = TOWER_R
        a[y_hi:y_hi + 2, x0:x1, :3] = mix(TOWER_L, (90, 110, 116), 0.4)
        win_grid(a, x0, y_hi, xm, y_low, rnd, lit, 3, 4, 4, 5, wall=TOWER_L)
        win_grid(a, xm, y_hi, x1, y_low, rnd, lit * 0.6, 3, 4, 4, 5, wall=TOWER_R, dim=0.8)
        y_low = y_hi + 2
    y_top = int(top)
    a[max(0, y_top - 30):y_top, cx - 2:cx + 2, :3] = TOWER_R
    if beacon: a[max(0, y_top - 34):max(0, y_top - 28), cx - 3:cx + 3, :3] = (230, 70, 60)


def glow(a, cx, cy, r, col, amt=0.5, cone=None, mask=None):
    """디더링된 부드러운 빛: 세기를 5단계로 나누고 단계 사이는 바이어 디더링 (둥근 띠가 보이지 않게)"""
    h, w = a.shape[:2]; yy, xx = np.mgrid[0:h, 0:w]; dx, dy = xx - cx, yy - cy
    k = np.clip(1 - np.hypot(dx, dy) / r, 0, 1) ** 1.5
    if cone:
        ang = np.arctan2(dy, dx); da = np.abs((ang - math.atan2(cone[1], cone[0]) + np.pi) % (2 * np.pi) - np.pi)
        k = k * np.clip((cone[2] - da) / 0.25, 0, 1)
    k = np.floor(k * amt * 5 + bayer(h, w)) / 5
    if mask is not None: k = k * mask
    for i in range(3): a[..., i] = a[..., i] + (col[i] - a[..., i]) * np.clip(k, 0, 1)
    return a


def font(sz): return ImageFont.truetype(FONT, sz)
def text_mask(size, s, sz, xy, anchor='mm'):
    m = Image.new('L', size); ImageDraw.Draw(m).text(xy, s, font=font(sz), fill=255, anchor=anchor); return np.array(m) > 110


def rim_light(img, col=RIM, sides=(-1, 1), w=2, top=True):
    """참고 영상의 하얀 테두리 빛: 실루엣 가장자리 안쪽 w px (아래쪽 가장자리는 빼고). 피부는 반만"""
    a = np.array(img).astype(float); op = a[..., 3] > 0; edge = np.zeros_like(op)
    for k in range(1, w + 1):
        for s in sides:
            sh = np.zeros_like(op)
            if s > 0: sh[:, :-k] = ~op[:, k:]; sh[:, -k:] = True
            else: sh[:, k:] = ~op[:, :-k]; sh[:, :k] = True
            edge |= sh
        if top:
            sh = np.zeros_like(op); sh[k:, :] = ~op[:-k, :]; sh[:k, :] = True; edge |= sh
    skin = (a[..., 0] > 140) & (a[..., 0] > a[..., 2] + 30)
    m = op & edge
    for i in range(3): a[..., i] = np.where(m, np.where(skin, a[..., i] * 0.5 + col[i] * 0.5, col[i]), a[..., i])
    return to_img(a)


# ------------------------------------------------------------------ 사람 (2배 밀도 128x192, 발 = y 188). 머리는 작게(약 1/7), 거의 검은 양복
FW, FH, FOOT = 128, 192, 188


def P(o, ang, L): return (o[0] + math.cos(math.radians(ang)) * L, o[1] + math.sin(math.radians(ang)) * L)
def limb(d, a, b, w, col):
    d.line([a, b], fill=col + (255,), width=w); r = w // 2
    d.ellipse([a[0] - r, a[1] - r, a[0] + r, a[1] + r], fill=col + (255,)); d.ellipse([b[0] - r, b[1] - r, b[0] + r, b[1] + r], fill=col + (255,))


def figure(view, pose):
    """팔다리는 두 개씩만. view: back | front | side (오른쪽을 봄)"""
    img = Image.new('RGBA', (FW, FH)); d = ImageDraw.Draw(img)
    ox, oy, sq = pose.get('ox', 0), pose.get('oy', 0), pose.get('sq', 1.0)
    hipY = FOOT - 66 * sq + oy; shY = hipY - 54 * sq; cx = 64 + ox; lean = pose.get('lean', 0)
    head = (cx + lean * 1.4 + pose.get('hx', 0), shY - 17 * sq + pose.get('hy', 0))
    sw = 18 if view != 'side' else 7
    shL, shR = (cx + lean - sw, shY + 3), (cx + lean + sw, shY + 3)
    hipL, hipR = ((cx - 9, hipY), (cx + 9, hipY)) if view != 'side' else ((cx - 3, hipY), (cx + 3, hipY))
    ak = pose.get('armk', 1.0); ua, fa, th, shn = 27 * sq * ak, 25 * sq * ak, 32 * sq, 32 * sq
    arms = pose['arms']; legs = pose.get('legs', [(95, 90), (85, 90)]); sit = pose.get('sit', False); hide = pose.get('hide', ())
    hv = pose.get('hv', view)

    def leg(hip, a1, a2, col):
        k = P(hip, a1, th); f = P(k, a2, shn); limb(d, hip, k, 15, col); limb(d, k, f, 13, col)
        fx = 1 if view == 'side' else (-1 if hip[0] < cx else 1)
        d.ellipse([f[0] - 8 + fx * 3, f[1] - 4, f[0] + 8 + fx * 3, f[1] + 4], fill=(10, 10, 12, 255))

    def arm(sh0, a1, a2, col, which=None):
        e = P(sh0, a1, ua); h = P(e, a2, fa); limb(d, sh0, e, 12, col)
        if which in hide: return e
        limb(d, e, h, 11, col)
        d.ellipse([h[0] - 5, h[1] - 5, h[0] + 5, h[1] + 5], fill=(SKIN if view == 'front' else SKIN_D) + (255,))
        return h

    if view == 'side':
        if not sit: leg(hipL, *legs[1], SUIT)
        arm(shL, *arms[1], SUIT)
        if not sit: leg(hipR, *legs[0], SUIT_M)
    elif not sit:
        leg(hipL, *legs[0], SUIT); leg(hipR, *legs[1], SUIT)
    if view == 'side':
        d.polygon([(cx + lean - 10, shY - 2), (cx + lean + 10, shY - 2), (cx + 12, hipY + 8), (cx - 12, hipY + 8)], fill=SUIT_M + (255,))
        d.polygon([(cx + lean + 6, shY - 2), (cx + lean + 10, shY - 2), (cx + 11, hipY - 12), (cx + 8, hipY - 12)], fill=SHIRT + (255,))
        d.line([(cx + lean + 9, shY), (cx + 10, hipY - 16)], fill=TIE + (255,), width=3)
    else:
        d.polygon([(shL[0] - 4, shY - 1), (shR[0] + 4, shY - 1), (cx + 16, hipY + 10), (cx - 16, hipY + 10)], fill=SUIT + (255,))
        if view == 'front':
            d.polygon([(cx + lean - 7, shY - 2), (cx + lean + 7, shY - 2), (cx + lean * 0.5, shY + 22)], fill=SHIRT + (255,))
            d.polygon([(cx + lean - 2, shY), (cx + lean + 2, shY), (cx + lean * 0.5 + 3, shY + 28), (cx + lean * 0.5, shY + 32), (cx + lean * 0.5 - 3, shY + 28)], fill=TIE + (255,))
            for sgn in (-1, 1): d.line([(cx + lean + sgn * 8, shY - 1), (cx + lean * 0.5 + sgn, shY + 26)], fill=SUIT_M + (255,), width=2)
        else:
            d.line([(cx + lean, shY + 6), (cx, hipY + 8)], fill=SUIT_M + (255,), width=2)
            d.rectangle([cx + lean - 6, shY - 4, cx + lean + 6, shY - 1], fill=SHIRT + (255,))   # 뒷목 셔츠 깃
    hx, hy = head
    d.rectangle([cx + lean - 5, shY - 9, cx + lean + 5, shY - 1], fill=SKIN_D + (255,))
    if hv == 'back':
        d.ellipse([hx - 10, hy - 12, hx + 10, hy + 12], fill=HAIR + (255,))
        d.ellipse([hx - 12, hy - 1, hx - 8, hy + 6], fill=SKIN_D + (255,)); d.ellipse([hx + 8, hy - 1, hx + 12, hy + 6], fill=SKIN_D + (255,))
        d.ellipse([hx - 10, hy - 12, hx + 10, hy + 10], fill=HAIR + (255,)); d.arc([hx - 7, hy - 10, hx + 5, hy - 2], 200, 320, fill=HAIR_L + (255,), width=2)
    elif hv == 'front':
        d.ellipse([hx - 9, hy - 10, hx + 9, hy + 12], fill=SKIN + (255,))
        d.ellipse([hx + 2, hy - 4, hx + 9, hy + 11], fill=SKIN_D + (255,)); d.ellipse([hx - 9, hy - 10, hx + 6, hy + 12], fill=SKIN + (255,))
        d.chord([hx - 11, hy - 14, hx + 11, hy + 6], 180, 360, fill=HAIR + (255,))
        d.rectangle([hx - 11, hy - 4, hx - 8, hy + 3], fill=HAIR + (255,)); d.rectangle([hx + 8, hy - 4, hx + 11, hy + 3], fill=HAIR + (255,))
    else:
        d.ellipse([hx - 9, hy - 11, hx + 10, hy + 12], fill=SKIN + (255,))
        d.chord([hx - 11, hy - 14, hx + 10, hy + 4], 180, 360, fill=HAIR + (255,)); d.ellipse([hx - 11, hy - 11, hx - 1, hy + 8], fill=HAIR + (255,))
        d.ellipse([hx - 3, hy - 1, hx + 2, hy + 5], fill=SKIN_D + (255,)); d.rectangle([hx + 9, hy + 1, hx + 12, hy + 4], fill=SKIN + (255,))
    hL = hR = (cx, shY)
    if view == 'side': arm(shR, *arms[0], SUIT_M)
    else: hL = arm(shL, *arms[0], SUIT_M, 'L'); hR = arm(shR, *arms[1], SUIT_M, 'R')
    for p in pose.get('paper', []):   # 두 손 사이에 쥔 통보 (구겨지는 중)
        s_ = p[2]; x, y = (hL[0] + hR[0]) / 2, (hL[1] + hR[1]) / 2
        d.polygon([(x - s_, y - s_ * 0.6), (x + s_, y - s_ * 0.8), (x + s_ * 0.8, y + s_ * 0.7), (x - s_ * 0.9, y + s_ * 0.6)], fill=(226, 218, 200, 255))
        d.line([(x - s_ * 0.6, y - s_ * 0.2), (x + s_ * 0.6, y + s_ * 0.1)], fill=(170, 160, 140, 255), width=2)
    return img


HERO = [
    ('back', dict(sit=True, arms=[(120, 60), (60, 120)], oy=6, lean=-1, hide=('L', 'R'))),     # 0·1 야근 (어깨가 번갈아 들썩)
    ('back', dict(sit=True, arms=[(112, 60), (64, 120)], oy=8, lean=1, hide=('L', 'R'))),
    ('back', dict(sit=True, arms=[(100, 80), (80, 100)], oy=14, sq=0.92, hy=8, hide=('L', 'R'))),   # 2 한숨 (축 처짐)
    ('back', dict(arms=[(98, 92), (82, 88)])),                                                  # 3 창가에 선 뒷모습
    ('back', dict(arms=[(98, 92), (40, -110)], sq=0.98)),                                       # 4·5 주먹 쥐고 부들부들
    ('back', dict(arms=[(98, 92), (38, -112)], ox=1, sq=0.97)),
    ('back', dict(arms=[(98, 92), (-60, -75)], sq=1.04)),                                       # 6 꼭대기를 가리킴
    ('front', dict(arms=[(115, -15), (65, -165)], sq=0.86, oy=4, paper=[(0, 0, 13)])),          # 7 화남 예비 (움츠림)
    ('front', dict(arms=[(-120, -70), (-60, -110)], sq=1.12, oy=4, hy=-3, armk=0.86, paper=[(0, 0, 12)])),   # 8·9 쭉 늘어나 부들부들 (손이 프레임 위에 닿지 않게)
    ('front', dict(arms=[(-118, -68), (-62, -112)], sq=1.1, ox=2, oy=4, hy=-2, armk=0.86, paper=[(0, 0, 10)])),
    ('front', dict(sit=True, arms=[(170, -10), (10, 190)], oy=30, sq=0.9, hy=16, hv='back')),   # 10 엎드려 잠
    ('front', dict(sit=True, arms=[(-140, -40), (-40, -140)], oy=4, sq=1.14, hy=-6)),           # 11 화들짝
    ('front', dict(sit=True, arms=[(120, 80), (-40, -150)], oy=8, hx=2)),                       # 12 머리 긁적
    ('side', dict(arms=[(70, 100), (110, 80)], legs=[(65, 100), (115, 95)])),                   # 13~16 걷기
    ('side', dict(arms=[(85, 95), (95, 85)], legs=[(80, 95), (100, 92)], oy=-2)),
    ('side', dict(arms=[(110, 80), (70, 100)], legs=[(115, 95), (65, 100)])),
    ('side', dict(arms=[(95, 85), (85, 95)], legs=[(100, 92), (80, 95)], oy=-2)),
    ('side', dict(arms=[(60, 70), (75, 80)], legs=[(85, 95), (95, 92)])),                       # 17 책상 앞에서 내려다봄
    ('side', dict(arms=[(40, -40), (70, 60)], legs=[(85, 95), (95, 92)], lean=-3)),             # 18 손을 치켜듦 (예비)
    ('side', dict(arms=[(55, 40), (70, 70)], legs=[(85, 95), (95, 92)], lean=4, sq=0.97)),      # 19 내려 누름
]
RIMS = {i: dict(col=RIM, sides=(-1, 1)) for i in range(7)}
RIMS.update({i: dict(col=(255, 196, 150), sides=(-1, 1)) for i in (7, 8, 9)})
RIMS.update({i: dict(col=(255, 236, 206), sides=(1,), w=2) for i in (10, 11, 12)})
RIMS.update({i: dict(col=(220, 200, 210), sides=(1,), w=2) for i in range(13, 20)})


def hero_sheet():
    sh = Image.new('RGBA', (FW * len(HERO), FH))
    for i, (v, p) in enumerate(HERO): sh.alpha_composite(rim_light(figure(v, p), **RIMS[i]), (i * FW, 0))
    return sh, len(HERO)


# ------------------------------------------------------------------ 배경
def office_night():
    W, H = 640, 360; a = sky(W, H, top=0, bottom=300); clouds(a, 3, 20, 160, 7)
    skyline(a, 300, 5, ((0.6, HAZE, 0.18, 0.75), (0.85, BLD_MID, 0.3, 0.9))); tower(a, 300, 300, 40, 7, lit=0.42)
    a[292:, :, :3] = (13, 14, 16)
    for x in (0, 212, 430, 632): a[:300, x:x + 8, :3] = (12, 13, 15)
    return to_img(a)


def office_fg(morning=False):
    W, H = 640, 360; a = np.zeros((H, W, 4)); top = 286
    a[top:top + 14, 60:600] = WOOD[3] + (255,); a[top:top + 3, 60:600] = WOOD[4] + (255,); a[top + 14:, 60:600] = WOOD[1] + (255,)
    for x in range(84, 600, 66): a[top + 16:, x:x + 2] = WOOD[0] + (255,)
    for i in range(5): a[top - 10 - i * 4:top - 7 - i * 4, 110 + i:190 + i] = (214 - i * 7, 208 - i * 7, 192 - i * 7, 255)
    a[top - 6:top, 420:540] = (28, 30, 34, 255); a[top - 5:top - 3, 424:536] = (60, 64, 72, 255)
    img = to_img(a); d = ImageDraw.Draw(img); lx, ly = 330, top
    d.rectangle([lx - 20, ly - 6, lx + 20, ly], fill=(22, 24, 28, 255))
    d.line([(lx, ly - 6), (lx - 14, ly - 56)], fill=(30, 32, 38, 255), width=5); d.line([(lx - 14, ly - 56), (lx + 20, ly - 84)], fill=(30, 32, 38, 255), width=5)
    d.polygon([(lx + 8, ly - 96), (lx + 40, ly - 82), (lx + 30, ly - 66), (lx + 2, ly - 78)], fill=(40, 42, 48, 255))
    if not morning: d.ellipse([lx + 14, ly - 74, lx + 26, ly - 66], fill=(255, 240, 190, 255))
    a = np.array(img).astype(float)
    if not morning: glow(a, lx + 20, ly - 70, 230, (255, 222, 150), 0.6, cone=(-0.1, 1, 0.8), mask=(a[..., 3] > 0))
    return to_img(a)


def desk_close(morning=False):
    W, H = 640, 360
    a = sky(W, H, [(110, 130, 150), (170, 160, 150), (220, 186, 150), (240, 206, 166)] if morning else SKY, 0, 150)
    clouds(a, 11, 10, 70, 5, 0.7, ((170, 150, 140), (210, 184, 160), (236, 210, 180)) if morning else (CLOUD_D, CLOUD, CLOUD_L))
    skyline(a, 140, 13, ((0.45, (150, 140, 140) if morning else HAZE, 0.0 if morning else 0.2, 0.7), (0.65, (118, 110, 114) if morning else BLD_MID, 0.0 if morning else 0.35, 0.85)))
    a[132:150, :, :3] = (40, 30, 26) if morning else (20, 16, 14)
    a[:150, 520:532, :3] = (12, 13, 15); a[:8, :, :3] = (12, 13, 15)
    rnd = random.Random(2)
    for y in range(150, H):
        k = (y - 150) / (H - 150); c = WOOD[2] if int((y - 150) / (6 + k * 26)) % 2 else WOOD[3]
        a[y, :, :3] = mix(c, (255, 220, 170), 0.25) if morning else c
    col = mix(WOOD[1], (255, 220, 170), 0.2) if morning else WOOD[1]
    for i in range(40):   # 나뭇결: 소실점으로 모이는 가는 선
        x0 = rnd.uniform(-300, W + 300)
        for y in range(150, H):
            x = int(320 + (x0 - 320) * (y - 140) / (H - 140))
            if 0 <= x < W and rnd.random() < 0.85: a[y, x, :3] = col
    img = to_img(a); d = ImageDraw.Draw(img)
    d.line([(470, 306), (610, 252)], fill=(14, 14, 16, 255), width=12); d.line([(474, 300), (606, 249)], fill=(70, 70, 80, 255), width=2)
    d.line([(568, 270), (584, 264)], fill=(206, 168, 76, 255), width=12); d.polygon([(470, 300), (458, 312), (476, 310)], fill=(206, 168, 76, 255))
    a = np.array(img).astype(float)
    if not morning: glow(a, 40, -10, 520, (255, 214, 140), 0.55)
    else: glow(a, 420, -20, 600, (255, 236, 200), 0.3)
    return to_img(a)


def rage_bg():
    W, H = 640, 360; a = np.zeros((H, W, 4)); a[..., 3] = 255; yy, xx = np.mgrid[0:H, 0:W]
    band = ((np.degrees(np.arctan2(yy - 170, xx - 320)) % 360) // 8).astype(int) % 2
    a[..., :3] = np.where(band[..., None] == 1, np.array([132, 28, 28]), np.array([104, 20, 22]))
    k = np.floor(np.clip(1 - np.hypot((xx - 320) / 1.4, yy - 170) / 260, 0, 1) * 5 + bayer(H, W)) / 5
    for i, c in enumerate((230, 120, 70)): a[..., i] = a[..., i] + (c - a[..., i]) * k * 0.6
    return to_img(a)


def window_tall():
    W, H = 640, 720; a = sky(W, H, [(24, 36, 36)] + SKY, 0, 660); clouds(a, 21, 60, 460, 11, 1.2)
    skyline(a, 660, 23, ((0.7, HAZE, 0.18, 0.75), (1.0, BLD_MID, 0.3, 0.9))); tower(a, 300, 660, 120, 25, lit=0.5, W=190)
    a[650:, :, :3] = (12, 13, 15)
    for x in (0, 90, 548, 632): a[:660, x:x + 8, :3] = (11, 12, 14)
    img = to_img(a); d = ImageDraw.Draw(img)
    d.line([(40, 700), (56, 664)], fill=(30, 32, 38, 255), width=5); d.polygon([(48, 656), (80, 666), (72, 682), (42, 670)], fill=(40, 42, 48, 255))
    a = np.array(img).astype(float); glow(a, 74, 676, 260, (255, 226, 150), 0.5, cone=(1, 0.15, 0.45))
    return to_img(a)


def ceo_room():
    """100층 CEO실: 큰 창 아래로 아득한 도시, 붉은 카펫 (책상은 cs_ceo_fg)"""
    W, H = 640, 360; a = sky(W, H, [(20, 26, 34), (26, 34, 44), (34, 42, 54), (42, 50, 62)], 0, 240)
    clouds(a, 31, 20, 120, 6, 0.9, ((30, 38, 48), (40, 50, 62), (54, 64, 78)))
    skyline(a, 238, 33, ((0.18, (40, 48, 58), 0.4, 0.7), (0.28, (32, 40, 50), 0.55, 0.85)))
    for x in range(0, W, 128): a[:240, x:x + 6, :3] = (26, 16, 16)
    a[236:, :, :3] = (60, 16, 20)
    for y in range(240, H, 12): a[y:y + 2, :, :3] = (72, 22, 26)
    a[234:240, :, :3] = (130, 104, 56)
    return to_img(a)


def ceo_fg():
    """CEO 책상 (사람 앞): 짙은 마호가니, 금빛 이름판, 가운데에 손바닥만 한 폭파 버튼 (논리 약 7x6px — 손 크기)"""
    W, H = 640, 360; a = np.zeros((H, W, 4)); top = 262
    a[top:top + 10, 120:520] = (74, 34, 26, 255); a[top:top + 2, 120:520] = (110, 56, 40, 255); a[top + 10:, 132:508] = (52, 24, 18, 255)
    for x in range(150, 508, 60): a[top + 12:, x:x + 2] = (40, 18, 14, 255)
    a[top - 8:top, 170:226] = (150, 120, 60, 255); a[top - 6:top - 2, 176:220] = (60, 44, 24, 255)
    a[top - 6:top, 316:330] = (24, 24, 28, 255); a[top - 10:top - 6, 319:327] = (190, 30, 32, 255); a[top - 10:top - 9, 320:323] = (255, 140, 130, 255)
    return to_img(a)


def button_close():
    """누르는 장면: CEO 책상 위 가까이 (마호가니 원근, 스탠드 빛, 서류, 만년필). 버튼과 손은 따로"""
    W, H = 640, 360; a = np.zeros((H, W, 4)); a[..., 3] = 255
    for y in range(H):
        a[y, :, :3] = (84, 38, 28) if int(y / (8 + y / H * 30)) % 2 else (96, 46, 34)
    rnd = random.Random(5)
    for i in range(36):
        x0 = rnd.uniform(-400, W + 400)
        for y in range(H):
            x = int(320 + (x0 - 320) * (y + 60) / (H + 60))
            if 0 <= x < W and rnd.random() < 0.8: a[y, x, :3] = (64, 28, 20)
    img = to_img(a); d = ImageDraw.Draw(img)
    d.polygon([(30, 40), (210, 20), (230, 150), (40, 170)], fill=(214, 206, 190, 255))
    for i in range(5): d.line([(60, 60 + i * 18), (190, 46 + i * 18)], fill=(170, 160, 146, 255), width=4)
    d.line([(420, 330), (630, 250)], fill=(14, 14, 16, 255), width=14); d.line([(540, 285), (560, 277)], fill=(206, 168, 76, 255), width=14)
    a = np.array(img).astype(float); glow(a, 120, -40, 560, (255, 210, 150), 0.55)
    return to_img(a)


def tower_night():
    W, H = 640, 360; a = sky(W, H); clouds(a, 41, 10, 140, 8)
    skyline(a, 360, 43, ((0.6, HAZE, 0.2, 0.75), (0.85, BLD_MID, 0.32, 0.9))); tower(a, 320, 360, 40, 45, lit=0.55, W=180)
    return to_img(a)


def office_morning():
    W, H = 640, 360; a = sky(W, H, [(120, 146, 166), (176, 170, 162), (226, 196, 160), (246, 216, 178)], 0, 300)
    clouds(a, 51, 20, 150, 6, 1.0, ((186, 168, 158), (222, 198, 176), (246, 224, 200)))
    skyline(a, 300, 53, ((0.6, (162, 156, 160), 0.0, 0.7), (0.85, (134, 128, 136), 0.0, 0.8))); tower(a, 300, 300, 40, 57, lit=0.0, beacon=False)
    a[292:, :, :3] = (66, 56, 52)
    for x in (0, 212, 430, 632): a[:300, x:x + 8, :3] = (48, 42, 40)
    yy, xx = np.mgrid[0:H, 0:W]
    ray = (((xx + yy * 0.34) // 34) % 3 == 0) & (yy < 292) & (bayer(H, W) < 0.35)   # 아침 햇살 (디더링 반투명)
    a[ray, :3] = a[ray, :3] * 0.6 + np.array([255, 244, 220]) * 0.4
    return to_img(a)


# ------------------------------------------------------------------ 소품·효과
def _persp(dst, src):
    A = []; B = []
    for (x, y), (u, v) in zip(dst, src):
        A.append([x, y, 1, 0, 0, 0, -u * x, -u * y]); B.append(u); A.append([0, 0, 0, x, y, 1, -v * x, -v * y]); B.append(v)
    return np.linalg.solve(np.array(A, float), np.array(B, float)).tolist()


def paper_sheet():
    """원근으로 누운 통보 (참고 영상): 평평하게 그린 뒤 사다리꼴로 눕힌다 (가장자리가 계단처럼). 1은 도장이 찍힌 것"""
    W, H = 400, 240; sh = Image.new('RGBA', (W * 2, H))
    for k in range(2):
        flat = Image.new('RGBA', (440, 300), (230, 224, 210, 255)); d = ImageDraw.Draw(flat)
        for i, x in enumerate((40, 56, 72)): d.rectangle([x, 34 - i * 4, x + 10, 52], fill=(150, 144, 132, 255))
        d.rectangle([100, 40, 240, 46], fill=(176, 170, 158, 255))
        fa = np.array(flat); fa[text_mask((440, 300), '구조조정까지', 38, (220, 104)) | text_mask((440, 300), 'D-30', 64, (220, 162))] = (186, 44, 40, 255)
        flat = Image.fromarray(fa); d = ImageDraw.Draw(flat)
        for i, ww in enumerate((260, 300, 220, 180)): d.rectangle([60, 210 + i * 16, 60 + ww, 218 + i * 16], fill=(184, 178, 166, 255))
        if k == 1:
            d.ellipse([320, 200, 410, 290], outline=(196, 40, 40, 255), width=7)
            fa = np.array(flat); fa[text_mask((440, 300), '인사', 22, (365, 245))] = (196, 40, 40, 255); flat = Image.fromarray(fa)
        img = flat.transform((W, H), Image.PERSPECTIVE, _persp([(70, 14), (360, 4), (396, 236), (6, 232)], [(0, 0), (440, 0), (440, 300), (0, 300)]), Image.NEAREST)
        a = np.array(img).astype(float); op = a[..., 3] > 0; shd = np.linspace(0.84, 1.04, H)[:, None]
        for i in range(3): a[..., i] = np.where(op, np.clip(a[..., i] * shd, 0, 255), 0)
        sh.alpha_composite(to_img(a), (k * W, 0))
    return sh


def fx_sheet():
    """만화 기호 (소리 글자는 없다): 0·1 핏대, 2~4 김, 5 !, 6 !?, 7 땀, 8 충격 별"""
    S = 64; n = 9; sh = Image.new('RGBA', (S * n, S)); red = (220, 50, 48, 255); yel = (255, 220, 90, 255)
    for i in range(n):
        img = Image.new('RGBA', (S, S)); d = ImageDraw.Draw(img); c = S // 2
        if i in (0, 1):
            s = 14 if i == 0 else 18
            for a_ in (45, 135, 225, 315):
                x = c + math.cos(math.radians(a_)) * s * 0.55; y = c + math.sin(math.radians(a_)) * s * 0.55
                d.line([(x, y), (x + math.cos(math.radians(a_ + 90)) * s * 0.6, y + math.sin(math.radians(a_ + 90)) * s * 0.6)], fill=red, width=5)
        elif i in (2, 3, 4):   # 김: 반투명 대신 디더링으로 옅어진다 (픽셀 아트는 알파 0/255)
            k = i - 2; r = 9 + k * 4; cy = 40 - k * 6
            d.ellipse([c - r, cy - r, c + r, cy + r], fill=(226, 226, 222, 255)); d.ellipse([c - r // 2 + 6, cy - r - 4, c + r // 2 + 6, cy + 2], fill=(244, 244, 240, 255))
            a_ = np.array(img); keep = bayer(S, S) < (1.0 - k * 0.3); a_[~keep] = 0; img = Image.fromarray(a_); d = ImageDraw.Draw(img)
        elif i == 5:
            d.rectangle([c - 5, 8, c + 5, 40], fill=yel); d.rectangle([c - 5, 46, c + 5, 56], fill=yel)
        elif i == 6:
            d.rectangle([14, 8, 22, 40], fill=yel); d.rectangle([14, 46, 22, 56], fill=yel)
            d.arc([30, 6, 56, 32], 180, 90, fill=yel, width=7); d.rectangle([40, 30, 47, 42], fill=yel); d.rectangle([40, 46, 47, 56], fill=yel)
        elif i == 7:
            d.polygon([(c, 10), (c + 12, 36), (c, 48), (c - 12, 36)], fill=(150, 200, 236, 255)); d.ellipse([c - 12, 26, c + 12, 50], fill=(150, 200, 236, 255)); d.ellipse([c - 6, 30, c, 38], fill=(230, 245, 255, 255))
        else:
            pts = [(c + math.cos(math.radians(a_)) * (30 if j % 2 == 0 else 12), c + math.sin(math.radians(a_)) * (30 if j % 2 == 0 else 12)) for j, a_ in enumerate(range(0, 360, 30))]
            d.polygon(pts, fill=(255, 244, 170, 255)); d.polygon([(x * 0.5 + c * 0.5, y * 0.5 + c * 0.5) for x, y in pts], fill=(255, 255, 255, 255))
        sh.alpha_composite(img, (i * S, 0))
    return sh, n


def boom_sheet():
    """폭발 8프레임: 하얀 섬광 → 불덩이 → 연기 (가장자리는 디더링)"""
    S = 192; n = 8; sh = Image.new('RGBA', (S * n, S)); rnd = random.Random(7)
    cols = [(255, 252, 226), (255, 222, 130), (246, 156, 70), (196, 80, 46), (96, 66, 62), (62, 54, 58)]
    blobs = [(rnd.uniform(0, 2 * math.pi), rnd.uniform(0.3, 1.0), rnd.uniform(0.5, 1.0)) for _ in range(16)]
    yy, xx = np.mgrid[0:S, 0:S]; B = bayer(S, S)
    for i in range(n):
        m = np.zeros((S, S)); ci = np.zeros((S, S)); c = S / 2; t = (i + 1) / n; R = 16 + 44 * (1 - (1 - t) ** 2)   # 프레임(192) 안에 들게
        for a_, dist, sz in blobs:
            x = c + math.cos(a_) * R * dist; y = c + 8 + math.sin(a_) * R * dist * 0.85 - t * 14; r = (10 + 16 * sz) * (0.6 + t * 0.8)
            v = 1 - ((xx - x) ** 2 + (yy - y) ** 2) / r ** 2; upd = v > m; m = np.where(upd, v, m); ci = np.where(upd, min(5, t * 4 + dist * 2), ci)
        if i < 4:
            v = 1 - ((xx - c) ** 2 + (yy - c) ** 2) / (R * (0.7 - i * 0.12)) ** 2; ci = np.where(v > 0, 0, ci); m = np.maximum(m, v)
        a = np.zeros((S, S, 4)); a[..., :3] = np.array(cols, float)[np.clip((ci + B * 0.9).astype(int), 0, 5)]; a[..., 3] = (m > 0.05 + (B - 0.5) * 0.1) * 255
        sh.alpha_composite(to_img(a), (i * S, 0))
    return sh, n


def button_sheet():
    """손바닥만 한 폭파 버튼 (가까이 본 모습, 2배 밀도 96x80): 검은 상자에 붉은 돔. 1은 눌림 (글자 없음)"""
    S, T = 96, 80; sh = Image.new('RGBA', (S * 2, T))
    for k in range(2):
        img = Image.new('RGBA', (S, T)); d = ImageDraw.Draw(img); c = S // 2
        d.polygon([(10, 58), (86, 58), (80, 76), (16, 76)], fill=(22, 22, 26, 255)); d.polygon([(14, 50), (82, 50), (86, 58), (10, 58)], fill=(40, 40, 46, 255))
        h = 18 if k == 0 else 7
        d.rectangle([c - 22, 52 - h, c + 22, 54], fill=(150, 18, 22, 255)); d.ellipse([c - 22, 44 - h, c + 22, 60 - h], fill=(210, 40, 40, 255))
        d.ellipse([c - 14, 46 - h, c - 2, 52 - h], fill=(255, 150, 140, 255))
        d.rectangle([24, 64, 72, 69], fill=(196, 160, 70, 255))
        sh.alpha_composite(img, (k * S, 0))
    return sh


def hand_sheet():
    """누르는 손 가까이 (2배 밀도 240x240, 위에서 내려온다): 0 떠 있음, 1 치켜듦(예비), 2 스미어(쭉 늘어남), 3 누름(눌림), 4 누른 채.
    검지 끝은 프레임 아래 가운데 근처에 온다 (게임에서는 버튼 돔에 맞춰 놓는다)"""
    S = 240; n = 5; sh = Image.new('RGBA', (S * n, S))
    for i in range(n):
        big = Image.new('RGBA', (S * 2, S * 2)); d = ImageDraw.Draw(big); cx = S
        stretch = {0: 1.0, 1: 0.92, 2: 1.35, 3: 0.86, 4: 0.9}[i]
        py = 150; ph = int(64 * stretch); il = int(58 * stretch)
        d.rectangle([cx - 42, 0, cx + 42, py - 10], fill=SUIT + (255,)); d.rectangle([cx - 40, py - 24, cx + 40, py], fill=SHIRT + (255,))
        d.rectangle([cx + 14, py - 18, cx + 24, py - 8], fill=(210, 180, 90, 255))
        d.rounded_rectangle([cx - 34, py, cx + 34, py + ph], 16, fill=SKIN + (255,))
        for fx in (-22, -6, 10): d.rounded_rectangle([cx + fx - 8, py + ph - 14, cx + fx + 8, py + ph + 8], 7, fill=SKIN_D + (255,))
        d.polygon([(cx - 34, py + 18), (cx - 54, py + 46), (cx - 48, py + 56), (cx - 26, py + 36)], fill=SKIN_D + (255,))
        if i == 2:   # 스미어 잔상: 반투명 대신 디더링 (뒤로 갈수록 성기게)
            ghost = Image.new('RGBA', big.size); gd = ImageDraw.Draw(ghost)
            for k_ in range(1, 4):
                g1 = Image.new('RGBA', big.size); ImageDraw.Draw(g1).rounded_rectangle([cx + 16, py + ph - 10 - 34 * k_, cx + 34, py + ph + 20 - 34 * k_], 9, fill=SKIN + (255,))
                ga = np.array(g1); ga[bayer(*ga.shape[:2]) >= 0.6 - 0.15 * k_] = 0; ghost.alpha_composite(Image.fromarray(ga))
            big.alpha_composite(ghost); d = ImageDraw.Draw(big)
        d.rounded_rectangle([cx + 16, py + ph - 20, cx + 34, py + ph + il], 9, fill=SKIN + (255,))
        d.rounded_rectangle([cx + 18, py + ph + il - 14, cx + 32, py + ph + il - 2], 5, fill=(228, 196, 176, 255))
        tip = (cx + 25, py + ph + il)
        img = big.rotate({0: -14, 1: -24, 2: -10, 3: -8, 4: -8}[i], resample=Image.NEAREST, center=tip, translate=(S - tip[0], int(S * 1.6) - tip[1]))
        img = img.resize((S, S), Image.NEAREST)   # 검지 끝 = (120, 192)
        sh.alpha_composite(rim_light(img, (255, 214, 160), sides=(-1,), w=2, top=False), (i * S, 0))
    return sh, n


if __name__ == '__main__':
    imgs = {
        'cs_office_night': office_night(), 'cs_office_fg': office_fg(), 'cs_desk_close': desk_close(), 'cs_desk_morning': desk_close(True),
        'cs_rage_bg': rage_bg(), 'cs_window_tall': window_tall(), 'cs_ceo_room': ceo_room(), 'cs_ceo_fg': ceo_fg(), 'cs_button_close': button_close(),
        'cs_tower_night': tower_night(), 'cs_office_morning': office_morning(), 'cs_office_morning_fg': office_fg(True),
    }
    hero, nh = hero_sheet(); fx, nfx = fx_sheet(); boom, nb = boom_sheet(); hand, nhd = hand_sheet()
    sheets = {'cs_hero': (hero, FW, FH, nh, None), 'cs_paper': (paper_sheet(), 400, 240, 2, None), 'cs_fx': (fx, 64, 64, nfx, None),
              'cs_boom': (boom, 192, 192, nb, 'boom'), 'cs_button': (button_sheet(), 96, 80, 2, None), 'cs_hand': (hand, 240, 240, nhd, None)}
    for k, im in imgs.items(): im.save(OUT / f'{k}.png')
    for k, v in sheets.items(): v[0].save(OUT / f'{k}.png')
    jp = OUT / 'sprite_definition.json'; d = json.loads(jp.read_text(encoding='utf-8'))
    for k in [k for k in d['images'] if k.startswith('cs_') and k not in imgs]: del d['images'][k]
    for k in [k for k in d['sprites'] if k.startswith('cs_') and k not in sheets]: del d['sprites'][k]
    for k, im in imgs.items(): d['images'][k] = {'file': f'{k}.png', 'w': im.width, 'h': im.height, 'density': 2, 'note': '컷신 배경 (2배 밀도: 논리 크기 = w/2 x h/2)'}
    for k, (im, fw, fh, n, kind) in sheets.items():
        anims = {'f%d' % i: {'frames': [i], 'fps': 1} for i in range(n)}
        if kind == 'boom': anims['boom'] = {'frames': list(range(n)), 'fps': 14}
        d['sprites'][k] = {'file': f'{k}.png', 'frame_w': fw, 'frame_h': fh, 'variants': {'default': 0}, 'animations': anims, 'density': 2,
                           'note': '컷신 (2배 밀도: 그릴 때 scale 2 → 논리 크기 = 프레임/2)'}
    js = json.dumps(d, ensure_ascii=False, indent=2)
    jp.write_text(js, encoding='utf-8'); (OUT / 'sprite_definition.js').write_text('window.SPRITE_DEF = ' + js + ';\n', encoding='utf-8')
    print('cutscene art:', ', '.join(list(imgs) + list(sheets)))
