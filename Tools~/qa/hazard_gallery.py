"""즉사 장애물 갤러리: 장애물 보기 장면(?demo=<id>)을 정해진 순간에 찍어 한 장으로 — 위험한 순간에 또렷하게 보이는지 눈으로 본다.
사용: python hazard_gallery.py <게임 폴더> <출력 접두어> shock:1.75,2.25,2.5 cracked:1.0,1.4 vanish:1.0,1.5"""
import sys
from pathlib import Path
from playwright.sync_api import sync_playwright
from PIL import Image, ImageDraw
web, out = Path(sys.argv[1]).resolve(), sys.argv[2]
plan = [(d, [float(x) for x in ts.split(',')]) for d, ts in (a.split(':') for a in sys.argv[3:])]
url = (web / 'index.html').as_uri(); tiles = []
with sync_playwright() as pw:
    b = pw.chromium.launch(); pg = b.new_context(viewport={'width': 1280, 'height': 720}).new_page()
    for did, ts in plan:
        pg.goto(url + f'?mute=1&nointro=1&demo={did}'); pg.wait_for_function('() => window.__stairs && window.__stairs.ready', timeout=20000)
        t0 = 0
        for t in ts:
            pg.wait_for_timeout(int((t - t0) * 1000)); t0 = t
            p = f'{out}_{did}_{t}.png'; pg.locator('canvas').screenshot(path=p); tiles.append((f'{did} {t}s', p))
    b.close()
cols = 3; o = Image.new('RGB', (cols * 640, ((len(tiles) + cols - 1) // cols) * 372), 'black'); d = ImageDraw.Draw(o)
for i, (lab, p) in enumerate(tiles):
    x, y = (i % cols) * 640, (i // cols) * 372; o.paste(Image.open(p).convert('RGB').resize((640, 360)), (x, y + 12)); d.text((x + 3, y), lab, fill='white')
o.save(out + '_sheet.png'); print(len(tiles))
