"""모든 QA 검사를 한 번에 돌린다 (지금 게임 demo_api를 대상으로).

1. 움직임·상호작용  qa_physics.js
2. 스프라이트       sprite_qa.py
3. 애니메이션       anim_qa.py
4. 단위 테스트      pytest tests/
5. 파이프라인 QA 에이전트를 지금 게임에: Developer QA, Designer QA (규칙 검사. 재생 응답이 없으면 Claude 검토는 건너뜀),
   Publisher 동작 확인(Playwright 9항목), Screens QA(컨셉 화면 대조)
   — 파이프라인 결과물 자리(output/dev_out, design_out)에 demo_api를 옮겨 놓고 돌린다 (소리는 ffmpeg로 WAV로 바꿔 둔다)

6. (Unity 판에서) Unity EditMode 테스트 — UNITY_EXE 환경 변수로 Unity 경로를 바꿀 수 있다

사용: python tools/qa/run_all.py [게임 폴더=demo_api, Unity 판은 WebReference] [--fast]   (--fast: Publisher·Screens QA·Unity 테스트 생략)
문제가 하나라도 있으면 종료 코드 1."""
import json, os, shutil, subprocess, sys, tempfile, time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent
args = [a for a in sys.argv[1:] if not a.startswith('--')]
FAST = '--fast' in sys.argv
GAME = (ROOT / (args[0] if args else ('demo_api' if (ROOT / 'demo_api').exists() else 'WebReference'))).resolve()   # Unity 판에서는 WebReference
ENV = dict(os.environ, PYTHONIOENCODING='utf-8', STAIRS_TEST_TIMEOUT='900')   # 봇이 100층까지 오르는 데 4분 넘게 걸린다
results = []


def run(name, cmd, ok_text=None, cwd=ROOT):
    t0 = time.time()
    p = subprocess.run(cmd, cwd=cwd, env=ENV, capture_output=True, text=True, encoding='utf-8', errors='replace')
    out = (p.stdout + p.stderr).strip()
    ok = p.returncode == 0 and (ok_text is None or ok_text in out)
    tail = '\n'.join(out.splitlines()[-6:])
    results.append((name, ok, time.time() - t0, tail))
    print(f"{'통과' if ok else '문제'}  {name}  ({time.time() - t0:.0f}초)")
    if not ok: print('    ' + tail.replace('\n', '\n    '))
    return ok


run('움직임·상호작용 (qa_physics.js)', ['node', str(HERE / 'qa_physics.js'), str(GAME / 'game.js')], 'problems: 0')
run('스프라이트 (sprite_qa.py)', [sys.executable, str(HERE / 'sprite_qa.py'), str(GAME / 'assets')], '문제 0개')
run('애니메이션 (anim_qa.py)', [sys.executable, str(HERE / 'anim_qa.py'), str(GAME / 'assets')], '문제 0개')
if (ROOT / 'tests').exists():
    run('단위 테스트 (pytest tests/)', [sys.executable, '-m', 'pytest', '-q', 'tests'])

if (ROOT / 'stairs_agents').exists():
    out = Path(tempfile.mkdtemp(prefix='stairs_qa_'))
    dev, des = out / 'dev_out', out / 'design_out'
    dev.mkdir(parents=True)
    for f in ('index.html', 'game.js'): shutil.copy(GAME / f, dev / f)
    man = json.loads((GAME / 'assets' / 'sprite_definition.json').read_text(encoding='utf-8'))
    for s in man.get('sounds', {}).values(): s.setdefault('loop', False)   # 나중에 더한 효과음은 loop를 적지 않았다 (기본 false)
    (dev / 'sprite_definition.json').write_text(json.dumps(man, ensure_ascii=False, indent=1), encoding='utf-8')
    shutil.copytree(GAME / 'assets', des)
    for sid, s in man.get('sounds', {}).items():   # Designer QA는 Designer가 내는 WAV를 본다
        src = des / (s.get('files', {}).get('ogg') or s.get('files', {}).get('mp3') or '')
        if src.is_file():
            subprocess.run(['ffmpeg', '-v', 'error', '-y', '-i', str(src), str(des / f'{sid}.wav')], check=False)
            s['file'] = f'{sid}.wav'
    (des / 'design_manifest.json').write_text(json.dumps(man, ensure_ascii=False), encoding='utf-8')
    q = [sys.executable, '-m', 'stairs_agents']
    common = ['--replay', 'replay', '--out', str(out)]
    run('Developer QA 에이전트 (규칙 검사)', q + ['qa-developer'] + common)
    run('Designer QA 에이전트 (규칙 검사)', q + ['qa-designer'] + common)
    if not FAST:
        if run('Publisher 동작 확인 (Playwright)', q + ['publisher', '--out', str(out)]):
            run('Screens QA (컨셉 화면 대조)', q + ['qa-screens'] + common)
    print(f"    파이프라인 QA 보고서: {out / 'qa_out'}")

UNITY = os.environ.get('UNITY_EXE', r'C:/Program Files/Unity/Hub/Editor/6000.6.10f1/Editor/Unity.exe')
if (ROOT / 'Assets').exists() and Path(UNITY).exists() and not FAST:   # Unity 판: EditMode 테스트 (웹 판과 프레임 단위로 같은지)
    xml = Path(tempfile.mkdtemp(prefix='stairs_unity_')) / 'editmode.xml'
    run('Unity EditMode 테스트 (ParityTests)', [UNITY, '-batchmode', '-nographics', '-projectPath', str(ROOT), '-runTests', '-testPlatform', 'EditMode',
                                              '-testResults', str(xml), '-logFile', str(xml.with_suffix('.log'))])
    if xml.exists(): print('    ' + ' '.join(t for t in __import__('re').findall(r'(?:total|passed|failed)="\d+"', xml.read_text(encoding='utf-8'))[:3]))

bad = [r for r in results if not r[1]]
print(f"\n검사 {len(results)}개, 통과 {len(results) - len(bad)}개, 문제 {len(bad)}개")
sys.exit(1 if bad else 0)
