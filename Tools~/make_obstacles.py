"""굴러오는 의자·편지 스프라이트 (obstacles.png, 32x32 프레임, 2배 밀도 = 화면 16x16).
0행 의자 roll 0~3 (바퀴살이 돈다, 몸통이 1px 들썩), 1행 편지 fly 0~3 (돌며 날개가 펄럭인다).
그린 뒤 외곽선을 한 번에 두르고, upgrade16.py가 명암을 넣는다. 사용: python make_obstacles.py <assets 폴더>"""
import sys, json, math
import numpy as np
from PIL import Image
A = sys.argv[1]; F = 32
OUT = (0x14, 0x15, 0x18)
def C(h): return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))
NAVY, NAVY_L, NAVY_D, RIM = C('34466E'), C('4E6290'), C('24304C'), C('7088BC')
STEEL, STEEL_L, STEEL_D = C('9AA0A8'), C('C9CCD1'), C('5A5F66')
BLACK, HUB = C('1E1F22'), C('7A8088')
PAPER, PAPER_L, PAPER_D, PAPER_DD = C('E8E6DF'), C('F7F5EE'), C('C9C6BC'), C('9A968C')
SEAL, SEAL_D = C('B83A3A'), C('7A2424')

class Cell:
    def __init__(self): self.c = [[None] * F for _ in range(F)]
    def px(self, x, y, col):
        if 0 <= x < F and 0 <= y < F: self.c[y][x] = col
    def rect(self, x, y, w, h, col):
        for yy in range(y, y + h):
            for xx in range(x, x + w): self.px(xx, yy, col)
    def disc(self, cx, cy, r, col):
        for yy in range(int(cy - r - 1), int(cy + r + 2)):
            for xx in range(int(cx - r - 1), int(cx + r + 2)):
                if (xx - cx) ** 2 + (yy - cy) ** 2 <= r * r + 0.3: self.px(xx, yy, col)
    def line(self, x0, y0, x1, y1, col, w=1):
        n = int(max(abs(x1 - x0), abs(y1 - y0)) * 2) + 1
        for i in range(n + 1):
            t = i / n; x = x0 + (x1 - x0) * t; y = y0 + (y1 - y0) * t
            for dy in range(w):
                for dx in range(w): self.px(int(round(x)) + dx, int(round(y)) + dy, col)
    def img(self):
        a = np.zeros((F, F, 4), np.uint8)
        for y in range(F):
            for x in range(F):
                if self.c[y][x] is not None: a[y, x, :3] = self.c[y][x]; a[y, x, 3] = 255
        # 외곽선을 한 번에 (칠해진 것 둘레의 빈칸)
        m = a[..., 3] > 0; o = np.zeros_like(m)
        for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
            o |= np.roll(np.roll(m, dy, 0), dx, 1)
        o &= ~m; a[o, :3] = OUT; a[o, 3] = 255
        return a

def chair(k):
    c = Cell(); b = 1 if k % 2 else 0          # 몸통이 1px 들썩
    # 등받이 (둥근 사각, 오른쪽을 본다: 등받이는 왼쪽)
    c.rect(6, 3 + b, 6, 13, NAVY); c.rect(7, 2 + b, 4, 1, NAVY); c.rect(7, 16 + b, 4, 1, NAVY)
    c.rect(7, 4 + b, 1, 10, NAVY_L); c.rect(11, 5 + b, 1, 10, NAVY_D)
    c.rect(6, 3 + b, 1, 13, RIM); c.rect(7, 2 + b, 4, 1, RIM); c.rect(10, 16 + b, 13, 1, RIM)   # 밝은 테두리 (어두운 벽에서도 보이게)
    c.rect(8, 8 + b, 2, 1, NAVY_D)            # 등받이 바느질 선
    # 등받이 지지대와 앉는 판
    c.rect(10, 15 + b, 2, 4, STEEL_D)
    c.rect(9, 17 + b, 15, 4, NAVY); c.rect(10, 16 + b, 13, 1, NAVY_L); c.rect(9, 20 + b, 15, 1, NAVY_D)
    # 팔걸이
    c.rect(13, 13 + b, 8, 2, BLACK); c.rect(19, 15 + b, 2, 2, BLACK)
    # 가스 실린더
    c.rect(16, 21 + b, 3, 4, STEEL); c.rect(16, 21 + b, 1, 4, STEEL_L); c.rect(18, 21 + b, 1, 4, STEEL_D)
    # 다섯 갈래 받침 (옆에서 세 갈래가 보인다)
    c.line(17, 25, 7, 27, STEEL_D, 2); c.line(17, 25, 27, 27, STEEL_D, 2); c.rect(16, 25, 3, 3, STEEL)
    # 바퀴 셋: 바퀴살(밝은 점)이 프레임마다 돈다
    ox, oy = [(0, -1), (1, 0), (0, 1), (-1, 0)][k]   # 바퀴살: 밝은 2x2 은색 덩어리가 프레임마다 위→오른쪽→아래→왼쪽으로 돈다
    for wx in (7, 17, 27):
        c.disc(wx, 28.5, 2.3, BLACK)
        c.rect(wx - 1 + ox, 28 + oy, 2, 2, STEEL_L)
    return c.img()

def envelope(k):
    """돌며 날아가는 편지 (윗변은 늘 같은 높이): 0 뒷면(접힌 날개·빨간 봉인), 1 날개가 들썩(봉인 그대로), 2 4분의 3 돌아간 모습, 3 앞면(우표·주소 줄)"""
    c = Cell(); W = [22, 22, 14, 22][k]; H = 14; x0 = (F - W) // 2; y0 = 9
    c.rect(x0, y0, W, H, PAPER); c.rect(x0, y0, W, 1, PAPER_L); c.rect(x0, y0 + H - 1, W, 1, PAPER_D); c.rect(x0 + W - 1, y0, 1, H, PAPER_D)
    if k == 2:   # 4분의 3: 좁고 오른쪽이 그늘, 날개 선이 비스듬히
        c.rect(x0 + W - 4, y0, 3, H, PAPER_D); c.line(x0, y0 + 1, x0 + W - 4, y0 + 6, PAPER_DD); return c.img()
    if k == 3:   # 앞면: 우표와 주소 줄
        c.rect(x0 + W - 7, y0 + 2, 5, 5, C('6E8FBF')); c.rect(x0 + W - 7, y0 + 2, 5, 1, C('9FB8DD')); c.px(x0 + W - 5, y0 + 4, C('E8E6DF'))
        for yy in (y0 + 8, y0 + 10): c.rect(x0 + 4, yy, 10, 1, PAPER_DD)
        return c.img()
    for i in range(W // 2):   # 뒷면: 아래 날개 V
        y = y0 + H - 1 - int(i * (H - 4) / (W / 2))
        c.px(x0 + i, y, PAPER_D); c.px(x0 + W - 1 - i, y, PAPER_D)
    apex = 6 if k == 0 else 3                     # 위 날개 끝: 들썩일 때는 위로 접힌다 (윗변은 그대로)
    for i in range(W // 2 + 1):
        y = y0 + int(i * apex / (W / 2))
        c.px(x0 + i, y, PAPER_DD); c.px(x0 + W - 1 - i, y, PAPER_DD)
    cx = x0 + W // 2; c.disc(cx - 0.5, y0 + apex, 2.2, SEAL); c.px(cx - 1, y0 + apex - 1, C('E06A5E')); c.px(cx, y0 + apex + 1, SEAL_D)   # 빨간 봉인
    return c.img()

sheet = np.zeros((F * 2, F * 4, 4), np.uint8)
for k in range(4):
    sheet[0:F, k * F:(k + 1) * F] = chair(k); sheet[F:2 * F, k * F:(k + 1) * F] = envelope(k)
Image.fromarray(sheet).save(f"{A}/obstacles.png")
def upd(d):
    d['sprites']['obstacles'] = {'file': 'obstacles.png', 'frame_w': 32, 'frame_h': 32, 'variants': {'chair': 0, 'envelope': 1},
                                 'animations': {'roll': {'frames': [0, 1, 2, 3], 'fps': 12}, 'fly': {'frames': [0, 1, 2, 3], 'fps': 10}}}
jp = f"{A}/sprite_definition.json"; d = json.load(open(jp, encoding='utf-8')); upd(d); open(jp, 'w', encoding='utf-8').write(json.dumps(d, ensure_ascii=False, indent=2))
js = f"{A}/sprite_definition.js"; t = open(js, encoding='utf-8').read(); pre = t[:t.index('{')]; d2 = json.loads(t[t.index('{'):].rstrip().rstrip(';')); upd(d2)
open(js, 'w', encoding='utf-8').write(pre + json.dumps(d2, ensure_ascii=False) + ';\n')
print('obstacles.png 4x2')
