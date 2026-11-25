"""16비트(2배 밀도) 에셋을 32비트 시대 느낌(4배 밀도)으로: 픽셀이 두 배 촘촘해지고 명암이 부드러워진다.

1. Scale2x(EPX): 프레임마다 따로 2배로 키운다. 같은 색이 이어지는 대각선은 계단 대신 매끈한 선이 된다 (흐림 없이 원래 색만 쓴다)
2. 외곽선 가늘게: 2배로 두꺼워진 바깥 외곽선의 안쪽 줄을 옆 몸 색의 어두운 색으로 바꿔 1px 외곽선으로 (32비트 시대 스프라이트처럼 또렷하게)
3. 부피 명암: 실루엣을 흐려 구한 면 방향으로 왼쪽 위 빛을 6단계로 더하고, 오른쪽 아래 가장자리에 차가운 반사광(림 라이트)을 1px
4. 계단·HUD·경고 표시는 1~2만, 배경 이미지는 1만 (그림이 바뀌지 않게)
sprite_definition.json의 frame_w/frame_h·이미지 w/h를 2배로, pixel_scale을 4로 바꾼다.

사용: python upgrade32.py <16비트 에셋 폴더(원본, 읽기만)> <출력 assets 폴더>, 그다음 python tools/qa/edge_close.py <출력 assets 폴더> (프레임 끝에서 잘린 곳을 다시 1px 외곽선으로)"""
import sys, json, shutil
from pathlib import Path
import numpy as np
from PIL import Image

SRC, DST = Path(sys.argv[1]), Path(sys.argv[2])
d = json.loads((SRC / 'sprite_definition.json').read_text(encoding='utf-8'))
FLAT = {'stairs', 'hud_icons', 'warning'}          # 명암을 더하지 않는 시트 (타일·아이콘)
LIGHT = np.array([-0.6, -0.8])                       # 빛: 왼쪽 위에서 (x, y)


def scale2x(f, edge=False):
    """EPX/Scale2x. f: (h, w, 4) uint8 → (2h, 2w, 4). 프레임 밖은 투명으로 본다 (edge=True: 배경 이미지는 가장자리 픽셀을 늘여 본다)."""
    h, w = f.shape[:2]
    if edge: p = np.pad(f, ((1, 1), (1, 1), (0, 0)), mode='edge')
    else: p = np.zeros((h + 2, w + 2, 4), np.uint8); p[1:-1, 1:-1] = f
    E = p[1:-1, 1:-1]; B = p[:-2, 1:-1]; H = p[2:, 1:-1]; D = p[1:-1, :-2]; F = p[1:-1, 2:]
    eq = lambda a, b: np.all(a == b, axis=-1)
    BD, BF, DH, HF = eq(B, D), eq(B, F), eq(D, H), eq(H, F)
    o = np.zeros((h * 2, w * 2, 4), np.uint8)
    e0 = np.where((BD & ~BF & ~DH)[..., None], D, E)
    e1 = np.where((BF & ~BD & ~HF)[..., None], F, E)
    e2 = np.where((DH & ~BD & ~HF)[..., None], D, E)
    e3 = np.where((HF & ~DH & ~BF)[..., None], F, E)
    o[0::2, 0::2], o[0::2, 1::2], o[1::2, 0::2], o[1::2, 1::2] = e0, e1, e2, e3
    return o


def nb(m, dy, dx):
    r = np.zeros_like(m); h, w = m.shape[:2]
    ys, ye, xs, xe = max(0, -dy), h - max(0, dy), max(0, -dx), w - max(0, dx)
    r[ys:ye, xs:xe] = m[ys + dy:ye + dy, xs + dx:xe + dx]; return r


def blur(a, r):
    """상자 흐림 r번 (가우스 근사), 가장자리는 0으로"""
    a = a.astype(float)
    for _ in range(3):
        p = np.pad(a, r)
        c = np.cumsum(np.cumsum(p, 0), 1); c = np.pad(c, ((1, 0), (1, 0)))
        k = 2 * r + 1; h, w = a.shape
        a = (c[k:k + h, k:k + w] - c[:h, k:k + w] - c[k:k + h, :w] + c[:h, :w]) / (k * k)
    return a


def thin_outline(f):
    op = f[..., 3] > 0; rgb = f[..., :3].astype(int)
    dark = op & (rgb.sum(-1) < 3 * 70)
    empty = ~op
    touch_empty = op & (nb(empty, -1, 0) | nb(empty, 1, 0) | nb(empty, 0, -1) | nb(empty, 0, 1))
    outer = dark & touch_empty                                   # 바깥 외곽선 줄
    near_outer = nb(outer, -1, 0) | nb(outer, 1, 0) | nb(outer, 0, -1) | nb(outer, 0, 1)
    body = op & ~dark
    inner = dark & ~touch_empty & near_outer & (nb(body, -1, 0) | nb(body, 1, 0) | nb(body, 0, -1) | nb(body, 0, 1))
    out = f.copy()
    ys, xs = np.nonzero(inner)
    for y, x in zip(ys, xs):                                     # 옆 몸 색의 어두운 색 (셀아웃)
        for dy, dx in ((0, 1), (0, -1), (1, 0), (-1, 0)):
            yy, xx = y + dy, x + dx
            if 0 <= yy < f.shape[0] and 0 <= xx < f.shape[1] and body[yy, xx]:
                out[y, x, :3] = (f[yy, xx, :3].astype(float) * 0.5).astype(np.uint8); break
    return out


def volume(f, amp=1.0):
    op = f[..., 3] > 0
    if not op.any(): return f
    rgb = f[..., :3].astype(float)
    dark = rgb.sum(-1) < 3 * 70
    a = blur(op, 3)
    gy, gx = np.gradient(a)
    n = np.stack([-gx, -gy], -1); ln = np.linalg.norm(n, axis=-1) + 1e-6
    facing = -(n[..., 0] * LIGHT[0] + n[..., 1] * LIGHT[1]) / ln * np.clip(ln * 12, 0, 1)   # +면 빛 쪽
    yy = np.linspace(1, -1, f.shape[0])[:, None] * np.ones((1, f.shape[1]))                   # 위가 밝다
    s = np.clip(0.55 * facing + 0.25 * yy * (a > 0.2), -1, 1)
    s = np.round(s * 4) / 4                                          # 8단계 (조각나 보이지 않게 얕게)
    k = 1 + 0.09 * amp * s
    lit = np.where(k[..., None] > 1, rgb + (255 - rgb) * (k[..., None] - 1) * 0.8, rgb * k[..., None])
    # 림 라이트: 빛 반대쪽(오른쪽 아래) 바깥 외곽선 바로 안쪽 1px을 차갑게 밝게
    empty = ~op
    outer = op & (nb(empty, -1, 0) | nb(empty, 1, 0) | nb(empty, 0, -1) | nb(empty, 0, 1))
    ring = op & ~outer & (nb(outer, 0, 1) | nb(outer, 1, 0)) & ~dark
    back = facing < -0.35
    rim = ring & back
    lit[rim] = lit[rim] * 0.7 + np.array([170, 200, 235]) * 0.3
    keep = dark | ~op
    out = f.copy(); out[..., :3] = np.where(keep[..., None], rgb, np.clip(lit, 0, 255)).astype(np.uint8)
    return out


PEOPLE = {'player', 'rival', 'staff', 'commuter'}
HAIR = np.array([20, 21, 24])


def people_detail(f):
    """사람 시트 32비트 디테일: 머리카락 윤기(한 줄 + 밝은 점), 턱 밑 목 그늘 (얼굴은 컨셉대로 비워 둔다)"""
    op = f[..., 3] > 0
    if not op.any(): return f
    out = f.copy(); rgb = f[..., :3].astype(int)
    hair = op & (np.abs(rgb - HAIR).sum(-1) < 30)
    skin = op & (rgb[..., 0] > 150) & (rgb[..., 0] > rgb[..., 2] + 40) & (rgb[..., 1] > 110)
    ys, xs = np.nonzero(hair)
    if len(ys) > 20:
        top = ys.min(); h = ys.max() - top + 1
        # 윤기: 머리 위쪽 1/3, 왼쪽(빛 쪽) 절반에 짧은 두 줄 (밝은 남색 → 더 밝은 하이라이트)
        # 윤기: 정수리 아래 두 줄, 빛 쪽(왼쪽) 가운데 짧게 — 어두운 남색 한 줄 + 밝은 점 둘
        cols = np.arange(f.shape[1])
        inner = hair & nb(hair, -1, 0) & nb(hair, 1, 0) & nb(hair, 0, -1) & nb(hair, 0, 1)
        x0, x1 = xs.min(), xs.max(); w = x1 - x0
        for y, xa, xb, c in ((top + 2, x0 + 0.22 * w, x0 + 0.55 * w, [0x34, 0x3a, 0x48]), (top + 3, x0 + 0.28 * w, x0 + 0.40 * w, [0x5c, 0x66, 0x7a])):
            if 0 <= y < f.shape[0]:
                m = inner[y] & (cols >= xa) & (cols <= xb); out[y, m, :3] = c
    if skin.any():
        # 목 그늘: 피부 픽셀 중 바로 위가 피부이고 2칸 아래가 피부가 아닌 맨 아래 줄 (턱 밑)
        below2 = nb(skin, 2, 0)
        chin = skin & nb(skin, -1, 0) & ~below2
        sy, sx = np.nonzero(skin); head_bottom = sy.min() + int((sy.max() - sy.min()) * 0.75) if len(sy) else 0
        out[chin, :3] = (rgb[chin] * 0.82).astype(np.uint8)
    return out


def upgrade_sheet(sid, sp):
    img = np.array(Image.open(SRC / sp['file']).convert('RGBA')); fw, fh = sp['frame_w'], sp['frame_h']
    rows, cols = img.shape[0] // fh, img.shape[1] // fw
    out = np.zeros((rows * fh * 2, cols * fw * 2, 4), np.uint8)
    for r in range(rows):
        for c in range(cols):
            f = scale2x(img[r * fh:(r + 1) * fh, c * fw:(c + 1) * fw])
            if sid not in ('warning',): f = thin_outline(f)
            if sid not in FLAT: f = volume(f, 0.6 if sid in ('props', 'sticky') else 1.0)
            if sid in PEOPLE: f = people_detail(f)
            out[r * fh * 2:(r + 1) * fh * 2, c * fw * 2:(c + 1) * fw * 2] = f
    Image.fromarray(out).save(DST / sp['file'])
    sp['frame_w'], sp['frame_h'] = fw * 2, fh * 2
    print('32bit', sid, f'{fw}x{fh} → {fw * 2}x{fh * 2}', f'{cols}x{rows}')


if __name__ == '__main__':
    DST.mkdir(parents=True, exist_ok=True)
    assert d.get('pixel_scale', 2) == 2, '원본은 2배 밀도여야 한다'
    for sid, sp in d['sprites'].items(): upgrade_sheet(sid, sp)
    for iid, im in d['images'].items():
        a = np.array(Image.open(SRC / im['file']).convert('RGBA'))
        Image.fromarray(scale2x(a, edge=True)).save(DST / im['file'])
        im['w'], im['h'] = im['w'] * 2, im['h'] * 2
        print('32bit image', iid, im['w'], 'x', im['h'])
    d['pixel_scale'] = 4
    d['pixel_note'] = '에셋은 논리 해상도(320x180)의 4배 밀도(32비트 시대 느낌). 게임은 PX = pixel_scale로 나눠 그린다. tools/sprites/upgrade32.py가 16비트(2배) 원본에서 만든다'
    for f in SRC.iterdir():                                       # 소리 등 나머지 파일은 그대로
        if f.suffix.lower() in ('.ogg', '.mp3', '.wav') and not (DST / f.name).exists(): shutil.copy(f, DST / f.name)
    js = json.dumps(d, ensure_ascii=False, indent=2)
    (DST / 'sprite_definition.json').write_text(js, encoding='utf-8')
    (DST / 'sprite_definition.js').write_text('window.SPRITE_DEF = ' + js + ';\n', encoding='utf-8')
    print('pixel_scale 4')
