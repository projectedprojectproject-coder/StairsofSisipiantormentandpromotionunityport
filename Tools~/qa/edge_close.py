"""프레임 테두리에서 잘린 곳 닫기: 손·발·소품이 프레임 가장자리에 닿아 외곽선 없이 잘려 보이는 픽셀을
안쪽 색의 어두운 색(셀아웃과 같은 0.42배)으로 바꿔 외곽선으로 닫는다. 이미 어두운 픽셀은 그대로 (여러 번 돌려도 같다).
사람 시트의 맨 아래 줄(발 기준선)과 계단·HUD 시트는 건드리지 않는다. upgrade16.py 다음에 돈다.
사용: python edge_close.py <assets 폴더> [시트 id ...]"""
import sys, json
import numpy as np
from PIL import Image

A = sys.argv[1]
d = json.load(open(f"{A}/sprite_definition.json", encoding='utf-8'))
SKIP = {'stairs', 'hud_icons', 'warning'}
PEOPLE = {'player', 'rival', 'staff', 'commuter'}
GROUND = PEOPLE | {'props'}


def is_closed(px, inner):
    """테두리 픽셀이 외곽선 구실을 하는가: 아주 어둡거나 안쪽 이웃보다 충분히 어둡다"""
    l, li = px[:3].astype(int).sum(), inner[:3].astype(int).sum() if inner[3] else 0
    return l < 3 * 64 or (inner[3] and l <= 0.62 * li)


def close_frame(f, bottom_ok):
    h, w = f.shape[:2]; n = 0
    edges = [(0, x, 1, x) for x in range(w)] + [(y, 0, y, 1) for y in range(h)] + [(y, w - 1, y, w - 2) for y in range(h)]
    if not bottom_ok: edges += [(h - 1, x, h - 2, x) for x in range(w)]
    for y, x, iy, ix in edges:
        if f[y, x, 3] and not is_closed(f[y, x], f[iy, ix]):
            base = f[iy, ix, :3] if f[iy, ix, 3] else f[y, x, :3]
            f[y, x, :3] = (base.astype(float) * 0.42).astype(np.uint8); n += 1
    return n


if __name__ == '__main__':
    ids = sys.argv[2:] or [k for k in d['sprites'] if k not in SKIP]
    for sid in ids:
        sp = d['sprites'][sid]; p = f"{A}/{sp['file']}"
        img = np.array(Image.open(p).convert('RGBA')); fw, fh = sp['frame_w'], sp['frame_h']; total = 0
        for y in range(0, img.shape[0] - fh + 1, fh):
            for x in range(0, img.shape[1] - fw + 1, fw):
                total += close_frame(img[y:y + fh, x:x + fw], sid in GROUND)
        if total: Image.fromarray(img).save(p)
        print('edge', sid, total, 'px')
