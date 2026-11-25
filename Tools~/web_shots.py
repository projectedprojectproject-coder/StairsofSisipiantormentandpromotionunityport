"""웹 판(demo_api/index.html)을 Unity PlaytestDriver와 같은 순서·같은 seed로 찍는다. 웹 파일은 읽기만 한다.

사용: python unity/StairsOfPromotion/Tools~/web_shots.py demo_api <스크린샷 폴더>
Unity 쪽: Stairs.exe -seed 1 -screen-width 1280 -screen-height 720 -stairsPlaytest <폴더>
같은 이름의 PNG끼리 맞대어 보면 된다 (계단 수로 맞춰 찍으므로 웹 쪽은 1~2프레임 늦을 수 있다).
"""
import sys
from pathlib import Path

from playwright.sync_api import sync_playwright

web = Path(sys.argv[1]).resolve()
out = Path(sys.argv[2]).resolve()
out.mkdir(parents=True, exist_ok=True)
url = (web / "index.html").as_uri()


def shot(page, name):
    page.locator("canvas").screenshot(path=str(out / (name + ".png")))


def wait(page, cond, timeout=150_000):
    page.wait_for_function("() => { const S = window.__stairs; return " + cond + "; }", timeout=timeout, polling="raf")


def key(page, k, n=1):
    for _ in range(n):
        page.keyboard.press(k)
        page.wait_for_timeout(30)


with sync_playwright() as pw:
    browser = pw.chromium.launch()
    ctx = browser.new_context(viewport={"width": 1280, "height": 720}, accept_downloads=True)

    # 1) 타이틀 · 설정 · 조작법 · 로비 · 보통 모드 봇 (seed 1)
    page = ctx.new_page()
    page.goto(url + "?seed=1&bot=1&mute=1")
    page.wait_for_timeout(1500); shot(page, "01_title")
    key(page, "ArrowRight"); key(page, "Enter"); page.wait_for_timeout(300); shot(page, "02_settings")    # 톱니바퀴 → 설정
    key(page, "ArrowDown", 4); key(page, "Enter"); page.wait_for_timeout(300); shot(page, "03_help")        # 조작법 "보기"
    key(page, "Escape"); key(page, "Escape")
    key(page, "Enter"); page.wait_for_timeout(500); shot(page, "04_lobby")                                # 시작하기 → 로비
    key(page, "Enter")                                                                                    # 업무 시작
    for n, name in [(20, "05_play_steps20"), (70, "06_play_steps70"), (180, "07_play_steps180")]:
        wait(page, f"S.steps >= {n}"); shot(page, name)
    wait(page, "S.steps >= 200"); key(page, "Escape"); page.wait_for_timeout(300); shot(page, "08_pause"); key(page, "Escape")
    for n, name in [(390, "09_play_steps390"), (450, "10_play_steps450")]:
        wait(page, f"S.steps >= {n}"); shot(page, name)
    wait(page, "S.screen === 'result'")
    page.wait_for_timeout(250); shot(page, "11_result_promoted_stamp")
    page.wait_for_timeout(1500); shot(page, "12_result_promoted")
    with page.expect_download() as dl:
        key(page, "ArrowRight"); key(page, "Enter")                                                       # 기록 공유
    dl.value.save_as(str(out / "stairs_result_web.png"))
    page.wait_for_timeout(300); shot(page, "13_result_share_toast")
    page.close()

    # 2) 가만히 있는 판 (seed 2, 봇 없음)
    page = ctx.new_page()
    page.goto(url + "?seed=2&mute=1")
    page.wait_for_timeout(800); key(page, "Enter"); page.wait_for_timeout(300); key(page, "Enter")
    wait(page, "S.screen === 'result'", 40_000)
    page.wait_for_timeout(1500); shot(page, "14_result_fired")
    page.close()

    # 3) 연습 (seed 3), 무한 (seed 4), 봇
    page = ctx.new_page()
    page.goto(url + "?seed=3&bot=1&mute=1")
    page.wait_for_timeout(800); key(page, "Enter"); page.wait_for_timeout(300); key(page, "ArrowLeft"); key(page, "Enter")
    page.wait_for_timeout(3000); shot(page, "15_practice")
    page.close()
    page = ctx.new_page()
    page.goto(url + "?seed=4&bot=1&mute=1")
    page.wait_for_timeout(800); key(page, "Enter"); page.wait_for_timeout(300); key(page, "ArrowRight"); key(page, "Enter")
    wait(page, "S.floor >= 25", 60_000); shot(page, "16_endless_25F")
    page.close()
    browser.close()
print("web shots ->", out)
