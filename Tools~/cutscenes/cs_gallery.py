"""컷신 QA 갤러리: 컷신마다 정해진 간격으로 멈춘 화면을 찍어 한 장(contact sheet)으로 모은다 (?cs=..&cst=..&csfreeze=1).
사용: python cs_gallery.py <게임 폴더(index.html)> <출력 폴더> [간격초=0.4]"""
import sys, re
from pathlib import Path
from playwright.sync_api import sync_playwright
from PIL import Image, ImageDraw

web, out = Path(sys.argv[1]).resolve(), Path(sys.argv[2]); out.mkdir(parents=True, exist_ok=True)
step = float(sys.argv[3]) if len(sys.argv) > 3 else 0.4
src = (web / 'game.js').read_text(encoding='utf-8')
url = (web / 'index.html').as_uri()
with sync_playwright() as pw:
    b = pw.chromium.launch(); pg = b.new_context(viewport={'width': 1280, 'height': 720}).new_page()
    errs = []; pg.on('pageerror', lambda e: errs.append(str(e)))
    pg.goto(url + '?mute=1&nointro=1'); pg.wait_for_function('() => window.__stairs && window.__stairs.ready', timeout=20000)
    for cid in ('intro', 'runstart', 'ending'):
        # 길이: 대본의 d 합 (화면에서 cutsceneT로 확인)
        pg.goto(url + f'?mute=1&cs={cid}&cst=0&csfreeze=1'); pg.wait_for_function('() => window.__stairs && window.__stairs.ready', timeout=20000)
        total = pg.evaluate('() => window.__stairs.cutsceneLen')
        shots = []; t = 0.05
        while t < total:
            pg.goto(url + f'?mute=1&cs={cid}&cst={t:.2f}&csfreeze=1'); pg.wait_for_function('() => window.__stairs && window.__stairs.ready', timeout=20000); pg.wait_for_timeout(120)
            if pg.evaluate('() => window.__stairs.screen') != 'cutscene': break
            p = out / f'{cid}_{t:05.2f}.png'; pg.locator('canvas').screenshot(path=str(p)); shots.append((t, p)); t += step
        cols = 6; w, h = 320, 180; rows = (len(shots) + cols - 1) // cols
        sheet = Image.new('RGB', (cols * w, rows * (h + 12)), 'black'); d = ImageDraw.Draw(sheet)
        for i, (tt, p) in enumerate(shots):
            im = Image.open(p).convert('RGB').resize((w, h)); x, y = (i % cols) * w, (i // cols) * (h + 12)
            sheet.paste(im, (x, y + 12)); d.text((x + 3, y), f'{cid} {tt:.2f}s', fill='white')
        sheet.save(out / f'sheet_{cid}.png'); print(cid, len(shots), 'frames', total, 's')
    print('errors', errs); b.close()
