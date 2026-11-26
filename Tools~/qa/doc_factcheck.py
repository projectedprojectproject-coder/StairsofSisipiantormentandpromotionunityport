"""README·안내 문서 사실 확인: 문서에 적힌 것이 지금 빌드의 코드와 맞는지 기계적으로 본다.

- 장애물이 나오는 층 ("편지 3연발 (25층~)", "젖은 계단 30층부터") ↔ game.js CONFIG.*FromFloor
- 규칙 수치 (D-30, 1일 0.33초, +1일·+1.5일, 하트 3, 막기 0.2초, 금 간 계단 1.5초, 전기 3초마다, 사라지는 칸 5초·1초,
  점수 10·50·100점, 연속 3회·×10, "!"가 사라지는 층, 상사의 눈 5초, 마지막 층, 장애물 보기 장면 수) ↔ CONFIG·코드
- 주소 매개변수 (?seed= …) ↔ game.js params.get/has, Unity 명령줄 (-seed …) ↔ C# 코드
- 파일·폴더 경로, 명령 (python xxx.py, python -m stairs_agents <명령>) ↔ 실제로 있는지
- 따옴표로 쓴 화면 글자 ("업무 시작", '로비로' …) ↔ game.js에 그 글자가 있는지
- 지금 빌드가 아닌 기록·결정 문구 (결정(날짜), 기획서 미정, PoC 범위, 승급 심사 …) → 경고: README는 지금 빌드만 쓴다
틀린 것(FAIL)이 있으면 종료 코드 1. 경고(WARN)는 사람이(또는 fact checker 에이전트가) 판단한다.
사용: python tools/qa/doc_factcheck.py [문서 ...]   (기본: README.md README.txt HOWTOPLAY.txt, Unity 판 README)"""
import re, sys, json, subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
UNITY = ROOT / 'unity' / 'StairsOfPromotion'
if not (ROOT / 'demo_api').exists() and (ROOT / 'WebReference').exists():   # Unity 판 저장소에서 (Tools~/qa)
    UNITY = ROOT
GAME = next((p for p in (ROOT / 'demo_api' / 'game.js', ROOT / 'WebReference' / 'game.js', UNITY / 'WebReference' / 'game.js') if p.exists()), None)
args = sys.argv[1:]
docs = [Path(a) for a in args] or list(dict.fromkeys(p.resolve() for p in (ROOT / 'README.md', ROOT / 'README.txt', ROOT / 'HOWTOPLAY.txt', UNITY / 'README.md') if p.exists()))
src = GAME.read_text(encoding='utf-8') if GAME else ''
cs_src = '\n'.join(p.read_text(encoding='utf-8', errors='ignore') for p in UNITY.glob('Assets/**/*.cs')) if UNITY.exists() else ''

# CONFIG 값 (숫자·배열)
cfg = {}
m = re.search(r'const CONFIG = \{(.*?)\n\};', src, re.S)
for k, v in re.findall(r'^\s+(\w+):\s*([^,/\n]+)', m.group(1) if m else '', re.M):
    try: cfg[k] = float(v)
    except ValueError: pass
demos = len(re.findall(r"^\s+\{ id: '\w+', name:", src, re.M))
marks = re.search(r'function showMarks\(\).*?run\.floor <= (\d+)', src)
btn_labels = set(re.findall(r"btn\('\w+', '([^']+)'", src))
strings = set(re.findall(r"'([^'\n]{1,40})'", src)) | set(re.findall(r'"([^"\n]{1,40})"', src))

FLOOR = [   # (문서에서 찾을 이름, CONFIG 키) — 긴 이름을 먼저 (서류가방 ⊃ 서류)
    ('서류가방', 'suitcaseFromFloor'), ('경쟁 사원', 'rivalFromFloor'), ('커피', 'coffeeFromFloor'), ('스테이플러', 'staplerFromFloor'),
    ('종이비행기', 'airplaneFromFloor'), ('모니터', 'monitorFromFloor'), ('편지', 'envelopeFromFloor'), ('의자', 'chairFromFloor'),
    ('전화기', 'phoneFromFloor'), ('서류', 'paperFromFloor'), ('포스트잇', 'stickyFromFloor'), ('에스컬레이터', 'escalatorFromFloor'),
    ('금 간', 'crackFromFloor'), ('젖은', 'wetFromFloor'), ('거꾸로', 'reverseFromFloor'), ('사라지는', 'vanishFromFloor'),
    ('상사의 눈', 'gazeFromFloor'), ('카메라 플래시', 'flashFromFloor'), ('결재판', None),
]
RULES = [   # (문서 정규식, 기대값 함수, 설명). 정규식의 첫 묶음이 문서의 숫자
    (r'D-(\d+)에서 시작', lambda: cfg.get('startDays'), '시작 남은 일수'),
    (r'1일이 약 ([\d.]+)초', lambda: cfg.get('dayDuration'), '1일 길이(초)'),
    (r'한 칸 오르면 \+([\d.]+)일', lambda: cfg.get('stepDays'), '계단 +일'),
    (r'막기에 성공하면 \+([\d.]+)일|패링에 성공하면 \+([\d.]+)일', lambda: cfg.get('parryDays'), '막기 +일'),
    (r'최대 (\d+)일', lambda: cfg.get('maxDays'), '최대 일수'),
    (r'하트 \(.*?(\d+)개\)', lambda: cfg.get('maxHearts'), '하트 수'),
    (r'직전 ([\d.]+)초 안에', lambda: cfg.get('parryWindow'), '막기 판정(초)'),
    (r'"!"가 뜨면 ([\d.]+)초 뒤', lambda: cfg.get('warnTime'), '예고(초)'),
    (r'([\d.]+)초 넘게 서 있으면', lambda: cfg.get('crackTime'), '금 간 계단(초)'),
    (r'([\d.]+)초마다 전기', lambda: cfg.get('shockPeriod'), '전기 주기(초)'),
    (r'([\d.]+)초 있다가', lambda: cfg.get('vanishShow'), '사라지는 칸 있는 시간'),
    (r'있다가 ([\d.]+)초 없어', lambda: cfg.get('vanishHide'), '사라지는 칸 없는 시간'),
    (r'계단 한 칸 (\d+)점', lambda: cfg.get('stepPoints'), '계단 점수'),
    (r'(?:막기|패링) 한 번 (\d+)점', lambda: cfg.get('parryPoints'), '막기 점수'),
    (r'새 층 (\d+)점', lambda: cfg.get('floorPoints'), '층 점수'),
    (r'연속 (\d+)회마다', lambda: cfg.get('comboPerMult'), '배수가 오르는 연속 수'),
    (r'최대 ×(\d+)', lambda: cfg.get('maxMult'), '최대 배수'),
    (r'(\d+)층 위에서는 "!" 없이', lambda: float(marks.group(1)) if marks else None, '"!" 표시가 사라지는 층'),
    (r'(\d+)초 뒤 레이저', lambda: cfg.get('gazeWarn'), '상사의 눈 예고(초)'),
    (r'(\d+)층까지', lambda: cfg.get('finalFloor'), '마지막 층'),
    (r'(\d{2,})장면', lambda: float(demos), '장애물 보기 장면 수'),   # 두 자리 이상 = 전체 수 ("…2장면 포함" 같은 일부는 빼고)
    (r'한 층 = 계단 (\d+)칸', lambda: cfg.get('stairsPerFloor'), '한 층 계단 수'),
]
HISTORY = [r'결정\s*\(?20\d\d', r'\(20\d\d-\d\d-\d\d', r'기획서 미정', r'PoC 범위', r'범위 밖', r'승급 심사', r'플레이테스트 반영',
           r'이전 버전', r'예전', r'원래는', r'바꾸었습니다', r'더했습니다']

GDD = (ROOT / 'docs' / 'GDD.md').read_text(encoding='utf-8') if (ROOT / 'docs' / 'GDD.md').exists() else ''
_names = None
def find_name(n):
    global _names
    if _names is None:
        _names = {q.name for q in ROOT.rglob('*') if q.is_file() and not any(x in q.parts for x in ('Library', 'node_modules', '.git', 'Temp', 'Logs'))}
    return n in _names
fails, warns, oks = [], [], 0
def fail(doc, ln, msg): fails.append(f'FAIL {doc}:{ln}  {msg}')
def warn(doc, ln, msg): warns.append(f'WARN {doc}:{ln}  {msg}')

for doc in docs:
    rel = doc.relative_to(ROOT) if doc.is_relative_to(ROOT) else doc
    base = doc.parent
    for ln, line in enumerate(doc.read_text(encoding='utf-8').splitlines(), 1):
        # 1) 장애물이 나오는 층
        taken = []
        for name, key in FLOOR:
            for mm in re.finditer(re.escape(name), line):
                if any(a <= mm.start() < b for a, b in taken): continue
                tail = line[mm.end():mm.end() + 24]
                f = re.match(r'[^()|]{0,14}?\(?(\d+)층(?:~|부터)', tail)   # 이름 뒤의 '3연발' 같은 숫자는 '층'이 붙지 않으니 지나간다
                if not f: continue
                taken.append((mm.start(), mm.end() + f.end()))
                want = 1 if key is None else cfg.get(key)
                got = int(f.group(1))
                if want is None: warn(rel, ln, f'{name}: CONFIG.{key} 를 찾지 못함')
                elif got != int(want): fail(rel, ln, f'{name}은 {int(want)}층부터인데 문서는 {got}층 (CONFIG.{key})')
                else: oks += 1
        # 2) 규칙 수치
        for rx, fn, desc in RULES:
            for mm in re.finditer(rx, line):
                got = next(g for g in mm.groups() if g); want = fn()
                if want is None: warn(rel, ln, f'{desc}: 코드에서 값을 찾지 못함'); continue
                if abs(float(got) - float(want)) > 1e-9: fail(rel, ln, f'{desc}: 코드는 {want:g}, 문서는 {got}')
                else: oks += 1
        # 3) 주소 매개변수와 명령줄
        for p in re.findall(r'[?&](\w+)=', line):
            if p in ('id', 'cst', 'csfreeze') and f"params.get('{p}')" in src: oks += 1; continue
            if f"params.get('{p}')" in src or f"params.has('{p}')" in src: oks += 1
            else: fail(rel, ln, f'주소 매개변수 ?{p}= 가 game.js에 없음')
        for f_ in re.findall(r'`(-[a-zA-Z]\w+)', line):
            if not cs_src: continue
            if f'"{f_}"' in cs_src: oks += 1
            else: fail(rel, ln, f'명령줄 {f_} 가 Unity 코드에 없음')
        # 4) 경로와 명령
        for tok in re.findall(r'`([^`\s]+)`', line) + re.findall(r'^\s*([\w~.-]+/)\s', line):
            t = tok.rstrip('/').split('?')[0]
            if not re.search(r'/|\.(py|js|md|txt|json|html|exe|cs|png)$', t) or t.startswith(('http', '-', 'Builds/', 'shots/', 'output')) or '<' in t or '*' in t: continue
            if any((b / t).exists() for b in (base, ROOT, UNITY)): oks += 1
            elif '/' not in t and find_name(t): oks += 1                    # 표의 폴더 칸에 딸린 파일 이름 (`index.html`)
            elif 'Unity' in line or 'unity' in line or t.startswith(('PlayNow', 'Assets/', 'Tools~')): warn(rel, ln, f'Unity 판 경로 {t} — 이 브랜치에 없음 (unity 브랜치에서 확인)')
            else: fail(rel, ln, f'경로가 없음: {t}')
        for script in re.findall(r'python3? (?:-I )?([\w~./-]+\.py)', line):
            if any((b / script).exists() for b in (base, ROOT, UNITY)): oks += 1
            else: fail(rel, ln, f'스크립트가 없음: {script}')
        for sub in re.findall(r'python -m stairs_agents (\w[\w-]*)', line):
            main = ROOT / 'stairs_agents' / '__main__.py'
            if not main.exists(): continue
            if f'"{sub}"' in main.read_text(encoding='utf-8'): oks += 1
            else: fail(rel, ln, f'stairs_agents 명령이 없음: {sub}')
        # 5) 따옴표로 쓴 화면 글자
        for q in re.findall(r'"([가-힣][가-힣 ·!?.…]{1,11})"', line) + re.findall(r"'([가-힣][가-힣 ·!?.…]{1,11})'", line):
            if q in strings or q in btn_labels or q in src or q in GDD: oks += 1   # 화면 글자이거나 기획서의 용어
            elif re.search(r'[딱헤똑펑착벨]', q): oks += 1   # 소리 묘사 ("딱… 딱!", "헤이!")는 화면 글자가 아니다
            else: warn(rel, ln, f'화면 글자 "{q}" 가 game.js에 없음 (화면에 없는 이름이면 고치기)')
        # 6) 지금 빌드가 아닌 기록·결정 문구
        for rx in HISTORY:
            if re.search(rx, line): warn(rel, ln, f'기록·결정 문구 ({rx}) — README·안내는 지금 빌드만 쓴다: {line.strip()[:70]}')
        if re.search(r'승급(?!의 계단)', line): warn(rel, ln, f"'승급' — 게임 이름 말고 승급 내용을 쓰려면 지금 빌드와 요청된 내용인지 확인: {line.strip()[:70]}")

print(f'문서 {len(docs)}개: ' + ', '.join(str(d.relative_to(ROOT) if d.is_relative_to(ROOT) else d) for d in docs))
print(f'확인한 사실 {oks}개, 틀림 {len(fails)}개, 경고 {len(warns)}개')
for x in fails + warns: print(x)
sys.exit(1 if fails else 0)
